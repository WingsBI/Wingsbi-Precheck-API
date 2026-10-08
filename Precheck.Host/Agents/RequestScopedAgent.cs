using Precheck.Agent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Precheck.Host.Agents
{
    // MapAGUIServer needs a single agent instance at startup, but the chatbot agent must be built per request
    // (ChatbotTools is scoped and reads the current user). This agent builds the real one from the current
    // request's services on first use and forwards every call to it.
    public sealed class RequestScopedAgent : AIAgent
    {
        private const string ItemKey = "Precheck.RequestScopedAgent.Inner";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _name;

        public RequestScopedAgent(IHttpContextAccessor httpContextAccessor, string name)
        {
            _httpContextAccessor = httpContextAccessor;
            _name = name;
        }

        public override string? Name => _name;

        private AIAgent Inner()
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("RequestScopedAgent can only be used inside an HTTP request.");

            if (context.Items[ItemKey] is AIAgent existing)
                return existing;

            var services = context.RequestServices;
            var contextBuilder = services.GetRequiredService<AgentContextBuilder>();
            var recorder = services.GetRequiredService<AgentTurnRecorder>();
            var suggestedQuestions = services.GetRequiredService<ISuggestedQuestionsProvider>();

            // Only the AG-UI path goes through here, so the history/save middleware never touches AskStream/AskLite.
            var agent = services.GetRequiredService<ChatbotAgentFactory>().BuildAgent(_name)
                .AsBuilder()
                .Use(runFunc: null, runStreamingFunc: (messages, session, options, inner, ct) =>
                    AgentRunMiddleware.RunStreamingAsync(messages, session, options, inner, contextBuilder, recorder, suggestedQuestions, ct))
                .Build();
            context.Items[ItemKey] = agent;
            return agent;
        }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
            => Inner().CreateSessionAsync(cancellationToken);

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
            => Inner().SerializeSessionAsync(session, jsonSerializerOptions, cancellationToken);

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
            => Inner().DeserializeSessionAsync(serializedState, jsonSerializerOptions, cancellationToken);

        protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
            => Inner().RunAsync(messages, session, options, cancellationToken);

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in Inner().RunStreamingAsync(messages, session, options, cancellationToken))
            {
                yield return update;
            }
        }
    }
}
