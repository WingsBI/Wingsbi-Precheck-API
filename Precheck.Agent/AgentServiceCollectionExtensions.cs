using Microsoft.Extensions.DependencyInjection;
using Precheck.Repository.Repository.AgentChatRepository;
using Precheck.Service.Service.AgentChatService;

namespace Precheck.Agent
{
    public static class AgentServiceCollectionExtensions
    {
        // Registers everything the AG-UI / Copilot endpoint needs besides the agent itself (which RequestScopedAgent
        // builds per request): chat history storage, the history/save middleware helpers and the follow-up question
        // provider. TSuggestedQuestions is supplied by the host because it needs the host's chat client and roles.
        // Scoped, because they read the current user and session from the HTTP request.
        public static IServiceCollection AddCopilotAgent<TSuggestedQuestions>(this IServiceCollection services)
            where TSuggestedQuestions : class, ISuggestedQuestionsProvider
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IAgentChatRepository, AgentChatRepository>();
            services.AddScoped<IAgentChatService, AgentChatService>();
            services.AddScoped<AgentContextBuilder>();
            services.AddScoped<AgentTurnRecorder>();
            services.AddScoped<ISuggestedQuestionsProvider, TSuggestedQuestions>();

            return services;
        }
    }
}
