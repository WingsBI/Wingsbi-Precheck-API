using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Precheck.Agent
{
    // Wraps every AG-UI agent run:
    //  1. Context: ignores whatever history the browser sent and builds the model's input from the saved
    //     session (last N turns) plus the new question, so token use stays flat however long the chat is.
    //  2. Usage: sums the model's token usage across all its calls in the run (tool calls cause several).
    //  3. Save: stores the finished turn in tbl_agent_chat_sessions once the reply is complete.
    public static class AgentRunMiddleware
    {
        public static async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            AIAgent innerAgent,
            AgentContextBuilder contextBuilder,
            AgentTurnRecorder recorder,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            long input = 0, output = 0;
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

                yield return update;
            }

            // The reply is complete here.
            await recorder.SaveAsync(received, reply.ToString(), toolCalls, input, output);
        }
    }
}
