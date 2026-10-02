using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Godrej.Precheck.Host.Agents;
using Godrej.Precheck.Models.DTOs.Chatbot;
using Godrej.Precheck.Service.Service.ChatbotService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace Godrej.Precheck.Host.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private const string SystemPrompt =
            "You are a data assistant for the Precheck quality-verification system (a pre-assembly gate: " +
            "every component gets a QR identity, is matched against its Production Order's BOM, and must " +
            "pass a scan-and-verify 'Precheck' before it can move downstream). Use the available tools to " +
            "answer questions about: Production Orders and their precheck status; Component Types, Drawing " +
            "Numbers, Assemblies, and LN Item Codes; QR codes and IR/MSN number tracking; pending prechecks " +
            "and available/consumed components; and downstream traceability - Stored In Components (verified " +
            "stock on hand), Material Requisitions (stock issued into production), and Swapping Details " +
            "(components swapped between drawing numbers/production orders). In this system, \"Part Number\" " +
            "and \"Drawing Number\" refer to the same thing - treat them as interchangeable in any question " +
            "or tool call. Map casual terms like \"in progress\"/\"open\" to the appropriate status value. " +
            "If a question is outside these topics, " +
            "do not call a tool - reply in plain text that you can only answer questions about these areas. " +
            "Never invent data that wasn't returned by a tool.";

        private readonly ILogger<ChatbotController> _logger;
        private readonly ChatbotTools _tools;
        private readonly IChatbotService _chatbotService;
        private readonly IConfiguration _configuration;

        public ChatbotController(
            ILogger<ChatbotController> logger,
            ChatbotTools tools,
            IChatbotService chatbotService,
            IConfiguration configuration)
        {
            _logger = logger;
            _tools = tools;
            _chatbotService = chatbotService;
            _configuration = configuration;
        }

        [HttpPost("Ask")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Ask([FromBody] ChatRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Message is required." });
            }

            try
            {
                var userId = Convert.ToInt32(User.FindFirst("id")?.Value);
                var roleId = Convert.ToInt32(User.FindFirst("roleid")?.Value ?? "0");

                _logger.LogInformation("Request for ChatbotController:Ask: {Message}", request.Message);

                var agent = BuildAgent();
                var (sessionId, session) = await LoadOrCreateSessionAsync(agent, request, userId);
                if (session == null)
                {
                    _logger.LogWarning("User {UserId} attempted to access ChatSession {SessionId} they do not own", userId, sessionId);
                    return Forbid();
                }

                var response = await agent.RunAsync(request.Message, session);

                var toolCalled = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .Select(c => c.Name)
                    .FirstOrDefault();

                object? data = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionResultContent>()
                    .Select(c => c.Result)
                    .FirstOrDefault();

                var serializedState = await agent.SerializeSessionAsync(session);
                await _chatbotService.UpdateSessionStateAsync(sessionId, serializedState.GetRawText());

                await _chatbotService.AddMessageAsync(sessionId, "user", request.Message, null);
                await _chatbotService.AddMessageAsync(sessionId, "assistant", response.Text, toolCalled);

                var suggestedQuestions = await GetSuggestedQuestionsAsync(roleId, request.Message, response.Text);

                var result = new ChatResponseDto
                {
                    SessionId = sessionId,
                    Answer = response.Text,
                    ToolCalled = toolCalled,
                    Data = data,
                    SuggestedQuestions = suggestedQuestions
                };

                _logger.LogInformation("Response for ChatbotController:Ask: SessionId={SessionId}, ToolCalled={ToolCalled}", sessionId, toolCalled);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Error for ChatbotController:Ask");
                return StatusCode(502, new ChatResponseDto
                {
                    Answer = "I couldn't reach the assistant right now, please try again shortly.",
                    Data = null
                });
            }
        }

        [HttpPost("AskStream")]
        [Authorize]
        public async Task AskStream([FromBody] ChatRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var userId = Convert.ToInt32(User.FindFirst("id")?.Value);
            var roleId = Convert.ToInt32(User.FindFirst("roleid")?.Value ?? "0");

            _logger.LogInformation("Request for ChatbotController:AskStream: {Message}", request.Message);

            var agent = BuildAgent();
            var (sessionId, session) = await LoadOrCreateSessionAsync(agent, request, userId);
            if (session == null)
            {
                _logger.LogWarning("User {UserId} attempted to access ChatSession {SessionId} they do not own", userId, sessionId);
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var updates = new List<AgentResponseUpdate>();
            try
            {
                await foreach (var update in agent.RunStreamingAsync(request.Message, session, cancellationToken: HttpContext.RequestAborted))
                {
                    updates.Add(update);
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        var payload = JsonSerializer.Serialize(new { text = update.Text });
                        await Response.WriteAsync($"data: {payload}\n\n", HttpContext.RequestAborted);
                        await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    }
                }

                var response = updates.ToAgentResponse();

                var toolCalled = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .Select(c => c.Name)
                    .FirstOrDefault();

                object? data = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionResultContent>()
                    .Select(c => c.Result)
                    .FirstOrDefault();

                var serializedState = await agent.SerializeSessionAsync(session);
                await _chatbotService.UpdateSessionStateAsync(sessionId, serializedState.GetRawText());

                await _chatbotService.AddMessageAsync(sessionId, "user", request.Message, null);
                await _chatbotService.AddMessageAsync(sessionId, "assistant", response.Text, toolCalled);

                var suggestedQuestions = await GetSuggestedQuestionsAsync(roleId, request.Message, response.Text);

                var donePayload = JsonSerializer.Serialize(new { sessionId, toolCalled, data, suggestedQuestions });
                await Response.WriteAsync($"event: done\ndata: {donePayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);

                _logger.LogInformation("Response for ChatbotController:AskStream: SessionId={SessionId}, ToolCalled={ToolCalled}", sessionId, toolCalled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Error for ChatbotController:AskStream");

                // Headers (200, text/event-stream) are already sent by this point, so an HTTP error
                // status is no longer possible - surface the failure as an SSE "error" event instead,
                // and skip persistence so a broken/partial turn doesn't get saved into session history.
                var errorPayload = JsonSerializer.Serialize(new { message = "I couldn't reach the assistant right now, please try again shortly." });
                await Response.WriteAsync($"event: error\ndata: {errorPayload}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }

        private async Task<(int SessionId, AgentSession? Session)> LoadOrCreateSessionAsync(AIAgent agent, ChatRequestDto request, int userId)
        {
            if (request.SessionId == null)
            {
                var title = request.Message.Length > 200 ? request.Message[..200] : request.Message;
                var newSessionId = await _chatbotService.CreateSessionAsync(userId, title);
                var newSession = await agent.CreateSessionAsync();
                return (newSessionId, newSession);
            }

            var sessionId = request.SessionId.Value;
            var belongsToUser = await _chatbotService.SessionBelongsToUserAsync(sessionId, userId);
            if (!belongsToUser)
            {
                return (sessionId, null);
            }

            var existingState = await _chatbotService.GetSessionStateAsync(sessionId);
            var session = existingState == null
                ? await agent.CreateSessionAsync()
                : await agent.DeserializeSessionAsync(JsonDocument.Parse(existingState).RootElement);

            return (sessionId, session);
        }

        private ChatClient BuildChatClient()
        {
            var endpoint = _configuration["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"]
                ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is not configured.");
            var apiKey = _configuration["AzureOpenAI:ApiKey"]
                ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured. Set it via dotnet user-secrets.");

            var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            return client.GetChatClient(deploymentName);
        }

        private AIAgent BuildAgent()
        {
            var chatClient = BuildChatClient();

            IList<AITool> tools = new List<AITool>
            {
                AIFunctionFactory.Create(_tools.GetProductionOrderStatusAsync, name: "get_production_order_status"),
                AIFunctionFactory.Create(_tools.GetProductionOrderDetailsAsync, name: "get_production_order_details"),
                AIFunctionFactory.Create(_tools.GetComponentTypesAsync, name: "get_component_types"),
                AIFunctionFactory.Create(_tools.GetComponentTypeByNameAsync, name: "get_component_type_by_name"),
                AIFunctionFactory.Create(_tools.GetDrawingNumbersAsync, name: "get_drawing_numbers"),
                AIFunctionFactory.Create(_tools.GetAssembliesAsync, name: "get_assemblies"),
                AIFunctionFactory.Create(_tools.GetAssemblyDrawingMappingsAsync, name: "get_assembly_drawing_mappings"),
                AIFunctionFactory.Create(_tools.SearchLnItemCodeAsync, name: "search_ln_item_code"),
                AIFunctionFactory.Create(_tools.GetQrCodeDetailsAsync, name: "get_qr_code_details"),
                AIFunctionFactory.Create(_tools.GetStandardQrCodeDetailsAsync, name: "get_standard_qr_code_details"),
                AIFunctionFactory.Create(_tools.SearchQrCodesAsync, name: "search_qr_codes"),
                AIFunctionFactory.Create(_tools.GetIrNumbersAsync, name: "get_ir_numbers"),
                AIFunctionFactory.Create(_tools.GetMsnNumbersAsync, name: "get_msn_numbers"),
                AIFunctionFactory.Create(_tools.GetPendingPrechecksAsync, name: "get_pending_prechecks"),
                AIFunctionFactory.Create(_tools.GetConsumedInComponentsAsync, name: "get_consumed_in_components"),
                AIFunctionFactory.Create(_tools.GetAvailableComponentsAsync, name: "get_available_components"),
                AIFunctionFactory.Create(_tools.GetStoredInComponentsAsync, name: "get_stored_in_components"),
                AIFunctionFactory.Create(_tools.GetMaterialRequisitionsAsync, name: "get_material_requisitions"),
                AIFunctionFactory.Create(_tools.GetSwappingDetailsAsync, name: "get_swapping_details"),
                AIFunctionFactory.Create(_tools.GetAvailableQrCodesAsync, name: "get_available_qr_codes"),
                AIFunctionFactory.Create(_tools.GetPrecheckByPoSeriesIdAsync, name: "get_precheck_by_po_series_id"),
                AIFunctionFactory.Create(_tools.SearchPrecheckRecordsAsync, name: "search_precheck_records"),
                AIFunctionFactory.Create(_tools.GetAvailableComponentsByFilterAsync, name: "get_available_components_by_filter"),
                AIFunctionFactory.Create(_tools.GetPrecheckAssemblyTemplateAsync, name: "get_precheck_assembly_template"),
            };

            return chatClient.AsAIAgent(instructions: SystemPrompt, tools: tools);
        }

        private async Task<List<string>> GetSuggestedQuestionsAsync(int roleId, string userMessage, string answerText)
        {
            try
            {
                var domainDescription = ChatbotRoles.GetDomainDescription(roleId);
                IChatClient chatClient = BuildChatClient().AsIChatClient();

                var messages = new List<Microsoft.Extensions.AI.ChatMessage>
                {
                    new(ChatRole.System,
                        $"The user's role domain is: {domainDescription}. Given the exchange below, suggest up to 3 " +
                        "short, relevant follow-up questions specific to that role's domain. Return only the structured result."),
                    new(ChatRole.User, $"Question: {userMessage}\nAnswer: {answerText}")
                };

                var result = await chatClient.GetResponseAsync<SuggestedQuestionsResult>(messages);
                return result.Result.Questions.Take(3).ToList();
            }
            catch (Exception ex)
            {
                // A failure here (e.g. rate limit) should never break the main answer that already
                // succeeded - fall back to no suggestions rather than failing the whole request.
                _logger.LogError(ex, "Exception Error for ChatbotController:GetSuggestedQuestionsAsync");
                return new List<string>();
            }
        }
    }
}
