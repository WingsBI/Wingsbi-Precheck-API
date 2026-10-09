using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Precheck.Agent
{
    // Skips the second model call for plain count and list questions. Normally the model picks a tool, then is called
    // AGAIN (re-sending the whole system prompt and every tool schema) only to say "there are N" or to re-type the rows.
    // For the list tools in Rules, when exactly one tool ran and the question is a plain "how many ..." or
    // "list/show/give me ...", the function loop is stopped right after the tool (InvokeAsync) and this class writes
    // the answer from the tool result instead (AgentRunMiddleware): a sentence for counts, a Markdown table with every
    // row for lists. Anything else (several tools, an error message, a question that needs reasoning, a result that is
    // not a flat list) is left to the model.
    //
    // Tools that are only a lookup step for another tool (get_drawing_numbers -> get_available_components, ...) and
    // all write tools are deliberately NOT in Rules.
    // To cover another list tool: add one line to Rules.
    public static class AgentFastAnswer
    {
        public sealed record Answer(string Text, string[] FollowUps);

        private sealed record Rule(string Noun, string[] FollowUps);

        private const int MaxColumns = 10;

        private static readonly Dictionary<string, Rule> Rules = new()
        {
            ["get_production_order_status"] = new("production orders", new[]
                { "How many production orders are pending?", "Give me the pending prechecks", "How many QR codes are available?" }),
            ["get_pending_prechecks"] = new("production orders with pending prechecks", new[]
                { "How many production orders are pending?", "List the production orders", "How many QR codes are available?" }),
            ["get_ir_numbers"] = new("IR numbers", new[]
                { "List the MSN numbers", "How many production orders are pending?", "Give me the pending prechecks" }),
            ["get_msn_numbers"] = new("MSN numbers", new[]
                { "List the IR numbers", "How many production orders are pending?", "Give me the pending prechecks" }),
            ["search_qr_codes"] = new("QR codes", new[]
                { "How many QR codes are available?", "List the stored in components", "How many production orders are pending?" }),
            ["get_stored_in_components"] = new("stored-in components", new[]
                { "List the material requisitions", "How many QR codes are available?", "Give me the swapping details" }),
            ["get_material_requisitions"] = new("material requisitions", new[]
                { "List the stored in components", "Give me the swapping details", "How many production orders are pending?" }),
            ["get_swapping_details"] = new("swapping records", new[]
                { "List the material requisitions", "List the stored in components", "How many production orders are pending?" }),
            ["get_available_qr_codes"] = new("available QR code groups", new[]
                { "How many QR codes are available?", "List the stored in components", "Give me the pending prechecks" }),
            ["search_precheck_records"] = new("precheck records", new[]
                { "Give me the pending prechecks", "How many production orders are pending?", "How many QR codes are available?" }),
            ["get_available_components_by_filter"] = new("available components", new[]
                { "Give me the pending prechecks", "How many QR codes are available?", "List the stored in components" }),
        };

        // A plain "how many" request.
        private static readonly Regex CountQuestion = new(
            @"\b(how many|number of|count|total)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A plain request for the rows.
        private static readonly Regex ListQuestion = new(
            @"\b(list|show|give|display|get|fetch|all)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Questions that need the model's judgement, or that do something other than read: never answered from a template.
        private static readonly Regex NeedsModel = new(
            @"\b(which|what are|why|explain|compare|versus|vs|trend|should|recommend\w*|summar\w*|insight|analy\w*|and|" +
            @"highest|lowest|most|least|best|worst|create|generate|make|add|swap|scan|verify|confirm|reject|replace)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Words in a column name that read better in capitals.
        private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
            { "ln", "qr", "ir", "msn", "po", "id" };

        // Set from AgentSettings:FastAnswers (default on). Turn off to get the model's own wording everywhere.
        public static bool Enabled { get; set; } = true;

        // Function-invocation hook: runs the tool, and ends the model loop after it when a rule will answer.
        public static async ValueTask<object?> InvokeAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
        {
            var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);

            if (Enabled && context.Iteration == 0 && context.FunctionCount == 1 &&
                TryBuild(context.Function.Name, context.Arguments, result, LastUserText(context.Messages), out _))
            {
                context.Terminate = true;
            }
            return result;
        }

        public static string? LastUserText(IEnumerable<ChatMessage>? messages) =>
            messages?.LastOrDefault(m => m.Role == ChatRole.User)?.Text;

        public static bool TryBuild(string toolName, IDictionary<string, object?>? args, object? rawResult, string? question, out Answer answer)
        {
            answer = null!;
            if (!Enabled || string.IsNullOrWhiteSpace(question) || !Rules.TryGetValue(toolName, out var rule)) return false;
            if (NeedsModel.IsMatch(question)) return false;

            var isCount = CountQuestion.IsMatch(question);
            if (!isCount && !ListQuestion.IsMatch(question)) return false;

            try
            {
                var element = rawResult switch
                {
                    null => (JsonElement?)null,
                    JsonElement e => e,
                    _ => JsonSerializer.SerializeToElement(rawResult),
                };
                // Plain messages ("not found") and objects are not a list: leave them to the model.
                if (element is not { ValueKind: JsonValueKind.Array } rows) return false;

                var count = rows.GetArrayLength();
                var noun = rule.Noun;
                if (toolName == "get_production_order_status" && StatusLabel(args) is { } status)
                    noun = $"{status} {noun}";

                if (count == 0)
                {
                    answer = new Answer($"There are no {noun}.", rule.FollowUps);
                    return true;
                }

                if (isCount)
                {
                    answer = new Answer($"There {(count == 1 ? "is" : "are")} **{count}** {noun}.", rule.FollowUps);
                    return true;
                }

                var table = BuildTable(rows);
                if (table is null) return false;   // not a flat list of rows: the model writes it

                answer = new Answer($"Here {(count == 1 ? "is the" : "are the")} **{count}** {noun}:\n\n{table}", rule.FollowUps);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // Every row, every field. Returns null (so the model answers) if a row is not a flat object, or if the
        // rows are too wide to read as a table.
        private static string? BuildTable(JsonElement rows)
        {
            var columns = new List<string>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) return null;
                foreach (var prop in row.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) return null;
                    if (!columns.Contains(prop.Name)) columns.Add(prop.Name);
                }
            }
            if (columns.Count == 0 || columns.Count > MaxColumns) return null;

            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", columns.Select(Header))).AppendLine(" |");
            sb.Append("|").Append(string.Concat(columns.Select(_ => " --- |"))).AppendLine();
            foreach (var row in rows.EnumerateArray())
            {
                sb.Append("| ");
                sb.Append(string.Join(" | ", columns.Select(c => row.TryGetProperty(c, out var v) ? Cell(v) : "-")));
                sb.AppendLine(" |");
            }
            return sb.ToString().TrimEnd();
        }

        // "LnItemCode" / "lnItemCode" -> "LN Item Code"
        private static string Header(string name)
        {
            var words = Regex.Split(name, @"(?<=[a-z0-9])(?=[A-Z])|_").Where(w => w.Length > 0);
            return string.Join(" ", words.Select(w =>
                Acronyms.Contains(w) ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]));
        }

        private static string Cell(JsonElement v)
        {
            string text = v.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "-",
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Number => v.GetRawText(),
                _ => FormatText(v.GetString() ?? string.Empty),
            };
            // A pipe or line break would break the table row.
            return text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string FormatText(string s)
        {
            if (s.Length == 0) return "-";
            if (s.Length >= 10 && char.IsDigit(s[0]) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
                return date.TimeOfDay == TimeSpan.Zero ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                                                       : date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return s;
        }

        // precheckStatus: 1=Pending, 2=Partial, 3=Completed, 4=Pending-Planner.
        private static string? StatusLabel(IDictionary<string, object?>? args)
        {
            if (args is null || !args.TryGetValue("precheckStatus", out var v) || v is null) return null;
            var number = v switch
            {
                JsonElement { ValueKind: JsonValueKind.Number } e when e.TryGetInt32(out var n) => n,
                int i => i,
                _ => 0,
            };
            return number switch
            {
                1 => "pending",
                2 => "partial",
                3 => "completed",
                4 => "pending-planner",
                _ => null,
            };
        }
    }
}
