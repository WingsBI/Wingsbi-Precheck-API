using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Precheck.Host.Agents
{
    // Trims a tool's result to only the fields the user's question names, before the model sees it.
    // Example: "give me pending production order numbers" -> each row keeps just ProductionOrderNumber.
    // It only trims when a field is named explicitly; any other question (or any problem) sends the full result.
    public static class ToolResponseTrimmer
    {
        // tool name -> field name -> words in a question that mean this field.
        private static readonly Dictionary<string, Dictionary<string, string[]>> Registry = new()
        {
            ["get_production_order_status"] = new()
            {
                ["ProductionOrderNumber"] = new[] { "order number", "po number", "order no", "po no", "po id" },
                ["Status"] = new[] { "status" },
                ["LnItemCode"] = new[] { "ln item", "item code" },
                ["DrawingNumber"] = new[] { "drawing", "part number" },
                ["Quantity"] = new[] { "quantity", "qty" },
                ["CreatedDate"] = new[] { "created", "date" },
            },
        };

        // Fields always kept so the model can still identify each row.
        private static readonly Dictionary<string, string[]> IdentityFields = new()
        {
            ["get_production_order_status"] = new[] { "ProductionOrderNumber" },
        };

        // Returns the trimmed result, or the original result unchanged if nothing can be trimmed.
        public static object? Trim(string toolName, object? result, string? question, ILogger? logger = null)
        {
            try
            {
                if (result is null || string.IsNullOrWhiteSpace(question)
                    || !Registry.TryGetValue(toolName, out var fields))
                    return result;

                var q = question.ToLowerInvariant();
                var wanted = fields.Where(f => f.Value.Any(alias => q.Contains(alias))).Select(f => f.Key).ToList();
                if (wanted.Count == 0) return result;

                if (IdentityFields.TryGetValue(toolName, out var identity))
                    foreach (var id in identity)
                        if (!wanted.Contains(id)) wanted.Add(id);

                var json = result is JsonElement el ? JsonNode.Parse(el.GetRawText()) : JsonSerializer.SerializeToNode(result);
                if (json is not JsonArray rows) return result;

                var before = json.ToJsonString().Length;
                var trimmed = new JsonArray();
                foreach (var row in rows)
                {
                    if (row is not JsonObject obj) return result;
                    var kept = new JsonObject();
                    foreach (var prop in obj)
                        if (wanted.Any(w => string.Equals(w, prop.Key, StringComparison.OrdinalIgnoreCase)))
                            kept[prop.Key] = prop.Value?.DeepClone();
                    trimmed.Add(kept);
                }

                var after = trimmed.ToJsonString().Length;
                logger?.LogInformation(
                    "ToolResponseTrimmer: {Tool} kept [{Fields}] for {Rows} rows; ~{Before} -> ~{After} tokens",
                    toolName, string.Join(", ", wanted), rows.Count, before / 4, after / 4);

                return JsonSerializer.SerializeToElement(trimmed);
            }
            catch (Exception ex)
            {
                // Never lose accuracy because trimming failed: send the full result.
                logger?.LogWarning(ex, "ToolResponseTrimmer failed for {Tool}; sending the full result.", toolName);
                return result;
            }
        }
    }

    // Wraps a tool so its result goes through ToolResponseTrimmer using the user's latest message.
    public sealed class TrimmingAIFunction : DelegatingAIFunction
    {
        private readonly ILogger? _logger;

        public TrimmingAIFunction(AIFunction inner, ILogger? logger = null) : base(inner)
        {
            _logger = logger;
        }

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);

            var question = FunctionInvokingChatClient.CurrentContext?.Messages
                .LastOrDefault(m => m.Role == ChatRole.User)?.Text;

            return ToolResponseTrimmer.Trim(Name, result, question, _logger);
        }
    }
}
