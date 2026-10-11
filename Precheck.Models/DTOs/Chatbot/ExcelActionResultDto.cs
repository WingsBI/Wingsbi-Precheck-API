namespace Precheck.Models.DTOs.Chatbot
{
    // Chat-sized result of a row-by-row Excel import: counts plus only the rows that failed, capped so a
    // large file with many errors can't blow the model's token budget.
    public class ExcelImportSummaryDto
    {
        public string Operation { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Failures { get; set; } = new();
        public int FailuresNotShown { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // Result of process_attached_excel_file: what the file was detected as and what was done with it.
    // When NeedsUserChoice is true (or the file can't be used yet) nothing was run - Message says why and
    // Options lists what the user can pick from.
    public class AutoProcessResultDto
    {
        public string DetectedType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool NeedsUserChoice { get; set; }
        public List<string> Options { get; set; } = new();
        public ExcelImportSummaryDto? ImportResult { get; set; }
        public ScriptRunSummaryDto? ScriptResult { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // Chat-sized result of running an external Excel script (success is decided by its exit code).
    public class ScriptRunSummaryDto
    {
        public string Operation { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
