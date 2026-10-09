namespace Precheck.Agent
{
    // Produces follow-up questions for the answer the assistant just gave. Implemented in the host
    // (it needs the chat client and the role definitions); the middleware only depends on this interface.
    public interface ISuggestedQuestionsProvider
    {
        // Up to 3 questions; an empty list when none could be produced. Never throws.
        Task<IReadOnlyList<string>> GetAsync(string question, string answer);
    }
}
