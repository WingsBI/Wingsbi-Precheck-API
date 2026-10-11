using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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
        private static readonly Regex SuggestionsBlock =
            new(@"\s*```suggestions\s*\[.*?\]\s*```\s*$", RegexOptions.Singleline | RegexOptions.Compiled);

        private static bool EndsWithQuestion(string reply)
        {
            var lastLine = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (lastLine is null) return false;
            return lastLine.EndsWith('?') || AsksForInput.IsMatch(lastLine);
        }

        // "Please provide ...", "Reply with ...": a request for input even without a question mark.
        private static readonly Regex AsksForInput = new(
            @"\b(please (provide|reply|tell|choose|confirm|pick|specify|give)|reply with|let me know)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A bare "yes / confirm / go ahead" - the user answering a confirmation prompt.
        private static readonly Regex Confirmation = new(
            @"^\s*(yes|y|yep|yeah|ok|okay|sure|confirm|confirmed|go ahead|proceed|do it|please do)\b[\s.!]*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Wording that reports a finished action. If this appears while no tool ran, the model made it up.
        private static readonly Regex SuccessClaim = new(
            @"\b(successfully|has been (created|generated|completed|updated|saved|consumed|stored)|was (created|generated|completed|updated|consumed)|precheck completed|generated \d+ new)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public const string NoActionWarning =
            "\n\n⚠️ **Nothing was changed.** No action was actually carried out for this message, so please ignore any " +
            "claim of success above. Send **confirm** again and I will run it.";

        // The user said "confirm" to a message that asked for confirmation. The model must answer by calling the tool,
        // not by writing a reply: the first model call is forced to use a tool (the framework relaxes this to automatic
        // after the first tool round, so the final answer is still written normally).
        private static AgentRunOptions ForceToolCall(AgentRunOptions? options)
        {
            if (options is ChatClientAgentRunOptions chatOptions)
            {
                chatOptions.ChatOptions ??= new ChatOptions();
                chatOptions.ChatOptions.ToolMode = ChatToolMode.RequireAny;
                return chatOptions;
            }
            return new ChatClientAgentRunOptions(new ChatOptions { ToolMode = ChatToolMode.RequireAny });
        }

        public static async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            AIAgent innerAgent,
            AgentContextBuilder contextBuilder,
            AgentTurnRecorder recorder,
            ISuggestedQuestionsProvider suggestedQuestions,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            ILogger? logger = null,
            int roleId = 0)
        {
            long input = 0, output = 0, cached = 0;
            int modelCalls = 0;
            string? lastMessageId = null;
            var received = messages.ToList();
            var reply = new StringBuilder();
            var toolCalls = new List<FunctionCallContent>();
            var toolResults = new Dictionary<string, object?>();

            var context = await contextBuilder.BuildAsync(received);

            var newestText = received.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
            var previousAssistantText = context.Where(m => m.Role == ChatRole.Assistant).LastOrDefault()?.Text
                                        ?? received.Where(m => m.Role == ChatRole.Assistant).LastOrDefault()?.Text;
            var isConfirmation = !string.IsNullOrWhiteSpace(newestText) && Confirmation.IsMatch(newestText) &&
                                 previousAssistantText?.Contains("confirm", StringComparison.OrdinalIgnoreCase) == true;
            if (isConfirmation)
            {
                options = ForceToolCall(options);
                logger?.LogInformation("Confirmation turn: the model is required to call a tool.");
            }

            await foreach (var update in innerAgent.RunStreamingAsync(context, session, options, cancellationToken))
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case UsageContent usage:
                            modelCalls++;
                            input += usage.Details.InputTokenCount ?? 0;
                            output += usage.Details.OutputTokenCount ?? 0;
                            cached += usage.Details.CachedInputTokenCount ?? 0;
                            break;
                        case TextContent text:
                            reply.Append(text.Text);
                            break;
                        case FunctionCallContent call:
                            toolCalls.Add(call);
                            break;
                        case FunctionResultContent resultContent:
                            toolResults[resultContent.CallId] = resultContent.Result;
                            break;
                    }
                }

                if (update.Contents.Any(c => c is TextContent) && !string.IsNullOrEmpty(update.MessageId))
                {
                    lastMessageId = update.MessageId;
                }

                yield return update;
            }

            // Safety net: a reply that says something was done, in a run where no tool executed at all, is invented
            // (this is how "Precheck completed successfully" once appeared with nothing written). Say so in the same message.
            if (toolCalls.Count == 0 && reply.Length > 0 && SuccessClaim.IsMatch(reply.ToString()))
            {
                logger?.LogWarning("Reply claims success but no tool ran in this turn - appending a warning.");
                reply.Append(NoActionWarning);
                lastMessageId ??= Guid.NewGuid().ToString("N");
                yield return new AgentResponseUpdate(ChatRole.Assistant, NoActionWarning) { MessageId = lastMessageId };
            }

            // Plain count question: the loop ended right after the tool (see AgentFastAnswer), so write the sentence
            // here, with its follow-up chips, instead of paying for a second model call.
            if (reply.Length == 0 && toolCalls.Count == 1 && toolResults.TryGetValue(toolCalls[0].CallId, out var onlyResult) &&
                AgentFastAnswer.TryBuild(toolCalls[0].Name, toolCalls[0].Arguments, onlyResult, AgentFastAnswer.LastUserText(received), out var fast, roleId))
            {
                var fastText = $"{fast.Text}\n\n```suggestions\n{JsonSerializer.Serialize(fast.FollowUps)}\n```";
                lastMessageId = Guid.NewGuid().ToString("N");
                reply.Append(fastText);
                yield return new AgentResponseUpdate(ChatRole.Assistant, fastText) { MessageId = lastMessageId };
            }

            // Azure OpenAI caches the repeated prompt prefix (>= 1024 tokens) automatically; this shows the hit rate.
            logger?.LogInformation(
                "Agent run tokens: calls={Calls}, input={Input} (cached={Cached}, {Percent}%), output={Output}",
                modelCalls, input, cached, input > 0 ? cached * 100 / input : 0, output);

            // The model writes its own ```suggestions block at the end of the reply (see the system prompt), so
            // the chips come from the same generation that produced the answer. Strip it before saving, so
            // it is never stored or fed back into later turns.
            var fullReply = reply.ToString();
            var match = SuggestionsBlock.Match(fullReply);
            var cleanReply = match.Success ? fullReply.Remove(match.Index).TrimEnd() : fullReply;
            await recorder.SaveAsync(received, cleanReply, toolCalls, input, output);

            // Fallback: if the model left the block out (and the reply wasn't a clarifying question), ask a
            // separate call for follow-ups. A second reply-time call is only made when needed.
            var question = received.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
            // A reply that ends by asking the user something (an ID number, a confirmation ...) gets no generic chips:
            // unrelated suggestions next to a question the user must answer are confusing.
            if (!match.Success && lastMessageId is not null && cleanReply.Length > 0 && !string.IsNullOrWhiteSpace(question) &&
                !EndsWithQuestion(cleanReply))
            {
                var questions = await suggestedQuestions.GetAsync(question, cleanReply);
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
