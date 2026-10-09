using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Precheck.Agent;

namespace Precheck.Host.Agents
{
    // Builds the chatbot agent (Azure OpenAI + system prompt + tools). Scoped, because ChatbotTools is scoped
    // (it reads the current user from the HTTP context), so every request gets its own agent.
    public class ChatbotAgentFactory
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
            "Never invent data that wasn't returned by a tool. " +
            "You can also CREATE IR and MSN numbers using create_ir_number/create_msn_number (Manufacturing Item, " +
            "by Production Order number) or create_standard_ir_number/create_standard_msn_number (Purchase Item, by " +
            "Purchase Order number, drawing/part number, and Production Series). Ask for required fields one at a " +
            "time if missing (Production/Purchase Order number first, then ID number(s), then Stage; Quantity/Build " +
            "No./Operation Number/Remark are optional for the Manufacturing Item flow). After a Production Order " +
            "number is given, the Part Number, Item Code, Production Series and Project No. are auto-filled - do not " +
            "ask the user for them. ALWAYS call the relevant create_* tool with confirmed=false first, show the user " +
            "the full preview, and only call it again with confirmed=true after they explicitly confirm (e.g. 'yes', " +
            "'go ahead', 'confirm') in their most recent message - never set confirmed=true otherwise. Never fabricate " +
            "a generated IR/MSN number yourself - only report the number a tool actually returned. " +
            "You can also GENERATE QR CODES using create_qr_code, for Manufacturing Item components identified by " +
            "Production Order number and Component Type (ID, FIM, SI, or BATCH). For 'ID' type, ask for one or more " +
            "ID numbers; for 'FIM'/'SI'/'BATCH', ask for a quantity instead. Same confirm-before-write protocol as " +
            "IR/MSN creation: call with confirmed=false first to preview, only call again with confirmed=true after " +
            "explicit user confirmation. If QR generation is blocked because Precheck isn't complete for a component, " +
            "tell the user exactly which component(s) still need precheck - never fabricate a QR code number yourself. " +
            "You can also MAKE PRECHECK (scan and verify one component against a target assembly's BOM) using " +
            "make_precheck. The target assembly is identified by Production Order number + Production Series + its " +
            "own assembly ID number + its own assembly drawing number; the component being scanned has its own, " +
            "separate component drawing number and QR code - do not confuse the two drawing numbers. Prefer calling " +
            "get_precheck_by_po_series_id first to see which component drawing(s) are still Pending for that " +
            "assembly. Same confirm-before-write protocol: confirmed=false to preview, confirmed=true only after " +
            "explicit user confirmation. make_precheck only ACCEPTS a scanned component - it cannot reject a " +
            "non-conforming one; if the user wants to reject/replace a component, tell them that capability isn't " +
            "available yet rather than attempting it. " +
            "EXCEL FILE ATTACHMENTS: a user message may contain an attachment note like [Attached file \"x.xlsx\" - " +
            "fileId: <id>]. When you see a NEW attachment, do NOT call any tool and do NOT guess what the file is for - " +
            "reply by asking what they want to do with it, offering these options: (1) import new Production Orders; " +
            "(2) update MIN/Status of existing Production Orders; (3) make Precheck in bulk (BOM sheet with QR codes); " +
            "(4) bulk Store In QR codes; (5) generate standard QR codes; (6) import QR codes with IR/MSN numbers; " +
            "(7) upload master data (needs TWO files: a drawing-assembly file and a drawing file). Once they answer, call " +
            "exactly the matching tool (import_production_orders_from_excel, update_production_order_min_status_from_excel, " +
            "make_precheck_from_excel, bulk_store_in_from_excel, run_standard_qr_generation_from_excel, " +
            "run_qr_code_import_from_excel, run_master_data_upload) with that file's fileId - no extra confirmation is " +
            "needed because their answer is the instruction. If the answer is ambiguous, ask again instead of choosing. " +
            "For master data, if only one file has been attached ask for the other, and if it is unclear which is the " +
            "drawing-assembly file and which is the drawing file, ask. Only ever use a fileId that appears in an " +
            "attachment note - never invent one. If a tool says the file was not found or expired, ask them to attach it " +
            "again. Report the counts the tool returned and show EVERY failure/error message it returned to the user, as a clear list, exactly as worded (they already say what is wrong and in which row); if rows failed, tell them to correct those rows and attach the file again; never claim success for rows " +
            "or scripts that failed, and never invent rows. " +
            "FOLLOW-UP SUGGESTIONS: at the very end of EVERY final reply (after any tool calls, and only once), " +
            "append exactly one fenced block in this form, on its own lines after your answer:\n" +
            "```suggestions\n[\"first request\", \"second request\", \"third request\"]\n```\n" +
            "It must be a JSON array of up to 3 short strings, based on what your reply just showed (e.g. after " +
            "listing pending production orders, suggest looking at their precheck status or QR codes). Each string " +
            "is sent as the user's own next message when clicked, so write it from the user's point of view as a " +
            "direct, self-contained request you can answer immediately. Use simple everyday words and VARY the " +
            "wording - the 3 suggestions must start with different words, and must not all begin with \"Show\". " +
            "Mix styles such as \"Give me ...\", \"List ...\", \"What is ...\", \"How many ...\", \"Tell me ...\", " +
            "\"Find ...\", \"Get ...\" (e.g. \"Give me the precheck records for PO-2026-0026\", \"How many QR codes " +
            "are available?\", \"List the pending prechecks\"). Never address the user (no \"Do you want...\" / \"Would you like...\"), never suggest " +
            "something that needs an identifier the user hasn't given, and never mention this block in your text. " +
            "Omit the block when you are asking the user for missing information or asking them to confirm an action.";

        private readonly ChatbotTools _tools;
        private readonly ChatbotFileTools _fileTools;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatbotAgentFactory> _logger;

        public ChatbotAgentFactory(ChatbotTools tools, ChatbotFileTools fileTools, IConfiguration configuration, ILogger<ChatbotAgentFactory> logger)
        {
            _tools = tools;
            _fileTools = fileTools;
            _configuration = configuration;
            _logger = logger;
        }

        public ChatClient BuildChatClient()
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

        public AIAgent BuildAgent(string? name = null)
        {
            var chatClient = BuildChatClient();

            IList<AITool> tools = new List<AITool>
            {
                new TrimmingAIFunction(AIFunctionFactory.Create(_tools.GetProductionOrderStatusAsync, name: "get_production_order_status"), _logger),
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
                AIFunctionFactory.Create(_tools.CreateIrNumberAsync, name: "create_ir_number"),
                AIFunctionFactory.Create(_tools.CreateMsnNumberAsync, name: "create_msn_number"),
                AIFunctionFactory.Create(_tools.CreateStandardIrNumberAsync, name: "create_standard_ir_number"),
                AIFunctionFactory.Create(_tools.CreateStandardMsnNumberAsync, name: "create_standard_msn_number"),
                AIFunctionFactory.Create(_tools.CreateQrCodeAsync, name: "create_qr_code"),
                AIFunctionFactory.Create(_tools.MakePrecheckAsync, name: "make_precheck"),
                AIFunctionFactory.Create(_fileTools.ImportProductionOrdersFromExcelAsync, name: "import_production_orders_from_excel"),
                AIFunctionFactory.Create(_fileTools.UpdateProductionOrderMinStatusFromExcelAsync, name: "update_production_order_min_status_from_excel"),
                AIFunctionFactory.Create(_fileTools.MakePrecheckFromExcelAsync, name: "make_precheck_from_excel"),
                AIFunctionFactory.Create(_fileTools.BulkStoreInFromExcelAsync, name: "bulk_store_in_from_excel"),
                AIFunctionFactory.Create(_fileTools.RunStandardQrGenerationAsync, name: "run_standard_qr_generation_from_excel"),
                AIFunctionFactory.Create(_fileTools.RunQrCodeImportAsync, name: "run_qr_code_import_from_excel"),
                AIFunctionFactory.Create(_fileTools.RunMasterDataAsync, name: "run_master_data_upload"),
            };

            AgentFastAnswer.Enabled = !bool.TryParse(_configuration["AgentSettings:FastAnswers"], out var fastAnswers) || fastAnswers;

            // Own function-invocation layer so plain count answers can end the loop early (see AgentFastAnswer).
            IChatClient client = chatClient
                .AsIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(configure: invoker => invoker.FunctionInvoker = AgentFastAnswer.InvokeAsync)
                .Build();

            return client.AsAIAgent(instructions: SystemPrompt, name: name, tools: tools);
        }
    }
}
