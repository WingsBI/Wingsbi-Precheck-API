using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Precheck.Agent
{
    // Wraps every AG-UI agent run:
    //  1. Context: ignores whatever history the browser sent and builds the model's input from the saved
    //     session (last N turns) plus the new question, so token use stays flat however long the chat is.
    //  2. Usage: sums the model's token usage across all its calls in the run (tool calls cause several).
    //  3. Save: stores the finished turn in tbl_agent_chat_sessions once the reply is complete.
    //  4. Suggestions: after the save, appends up to 3 role-based follow-up questions as a ```suggestions block.
    public static class AgentRunMiddleware
    {
        public static async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            AIAgent innerAgent,
            AgentContextBuilder contextBuilder,
            AgentTurnRecorder recorder,
            ISuggestedQuestionsProvider suggestedQuestions,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            long input = 0, output = 0;
            string? lastMessageId = null;
            var received = messages.ToList();
            var reply = new StringBuilder();
            var toolCalls = new List<FunctionCallContent>();

            var context = await contextBuilder.BuildAsync(received);

            await foreach (var update in innerAgent.RunStreamingAsync(context, session, options, cancellationToken))
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case UsageContent usage:
                            input += usage.Details.InputTokenCount ?? 0;
                            output += usage.Details.OutputTokenCount ?? 0;
                            break;
                        case TextContent text:
                            reply.Append(text.Text);
                            break;
                        case FunctionCallContent call:
                            toolCalls.Add(call);
                            break;
                    }
                }

                if (update.Contents.Any(c => c is TextContent) && !string.IsNullOrEmpty(update.MessageId))
                {
                    lastMessageId = update.MessageId;
                }

                yield return update;
            }

            // The reply is complete here: save the turn first, so the suggestions block below is never stored.
            await recorder.SaveAsync(received, reply.ToString(), toolCalls, input, output);

            // Follow-up questions: appended to the reply as a fenced ```suggestions block the chat UI turns into chips.
            var question = received.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
            if (lastMessageId is not null && reply.Length > 0 && !string.IsNullOrWhiteSpace(question))
            {
                var questions = await suggestedQuestions.GetAsync(question, reply.ToString());
                if (questions.Count > 0)
                {
                    var json = JsonSerializer.Serialize(questions);
                    yield return new AgentResponseUpdate(ChatRole.Assistant, $"\n\n```suggestions\n{json}\n```")
                    {
                        MessageId = lastMessageId,
                    };
                }
            }
        }
    }
}
