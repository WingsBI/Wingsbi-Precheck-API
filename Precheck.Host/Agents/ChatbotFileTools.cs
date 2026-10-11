using System.ComponentModel;
using Precheck.Host.Helpers;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Service.Service.PrecheckService;
using Precheck.Service.Service.ProductionOrderService;
using Precheck.Service.Service.QRCodeService;

namespace Precheck.Host.Agents
{
    // Tools that act on an Excel file the user attached in the chat. The file itself never reaches the model:
    // the chat message carries a fileId (see ChatbotController.UploadFile) and each tool resolves it for the
    // logged-in user. Each one runs the same code as the matching upload endpoint.
    public class ChatbotFileTools
    {
        private const int MaxListedFailures = 30;
        private const int MaxScriptOutputChars = 3000;

        private readonly IProductionOrderService _productionOrderService;
        private readonly IPrecheckService _precheckService;
        private readonly IQRCodeService _qrCodeService;
        private readonly ChatUploadStore _uploads;
        private readonly ExcelScriptRunner _scripts;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatbotFileTools(
            IProductionOrderService productionOrderService,
            IPrecheckService precheckService,
            IQRCodeService qrCodeService,
            ChatUploadStore uploads,
            ExcelScriptRunner scripts,
            IHttpContextAccessor httpContextAccessor)
        {
            _productionOrderService = productionOrderService;
            _precheckService = precheckService;
            _qrCodeService = qrCodeService;
            _uploads = uploads;
            _scripts = scripts;
            _httpContextAccessor = httpContextAccessor;
        }

        private int CurrentUserId()
        {
            int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("id")?.Value, out var userId);
            return userId;
        }

        private static ExcelImportSummaryDto Failure(string operation, string message)
            => new() { Operation = operation, Message = message };

        private static void AddFailures(ExcelImportSummaryDto summary, IEnumerable<string> failures)
        {
            var all = failures.ToList();
            summary.Failures = all.Take(MaxListedFailures).ToList();
            summary.FailuresNotShown = Math.Max(0, all.Count - MaxListedFailures);
        }

        // Resolves the file, runs the import over its stream, and always removes the file afterwards.
        private async Task<ExcelImportSummaryDto> RunImportAsync(
            string operation, string? fileId, Func<Stream, int, Task<ExcelImportSummaryDto>> import)
        {
            var userId = CurrentUserId();
            var path = _uploads.Resolve(fileId, userId, xlsxOnly: false, out var error);
            if (path == null) return Failure(operation, error!);

            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var summary = await import(stream, userId);
                summary.Operation = operation;
                return summary;
            }
            catch (ApplicationException ex)
            {
                return Failure(operation, ex.Message);
            }
            finally
            {
                _uploads.Delete(path);
            }
        }

        private async Task<ScriptRunSummaryDto> RunScriptAsync(
            string operation, string? fileId, Func<string, int, Task<ScriptRunResult>> run)
        {
            var userId = CurrentUserId();
            var path = _uploads.Resolve(fileId, userId, xlsxOnly: true, out var error);
            if (path == null) return new ScriptRunSummaryDto { Operation = operation, Message = error! };

            try
            {
                return ToSummary(operation, await run(path, userId));
            }
            finally
            {
                _uploads.Delete(path);
            }
        }

        private static ScriptRunSummaryDto ToSummary(string operation, ScriptRunResult result)
        {
            // Scripts can be chatty; the end of the output is where the summary and errors are.
            var output = result.Output.Length > MaxScriptOutputChars
                ? "..." + result.Output[^MaxScriptOutputChars..]
                : result.Output;
            return new ScriptRunSummaryDto
            {
                Operation = operation,
                Success = result.Success,
                ExitCode = result.ExitCode,
                Output = output,
                Message = result.Message
            };
        }

        [Description("Import NEW Production Orders from the Excel file the user attached. Only call this after the user " +
            "has said they want to import/create production orders from the file. Expected columns (row 1 = header): " +
            "Production Order No, Project Code, Project Description, Item Code, Item Description, Start ID Number " +
            "(e.g. GA0153), Quantity, MRIR No, MIN, Status, Build No, Snag Sheet No. Rows are imported independently; " +
            "existing POs and invalid rows are skipped and reported. Accepts .xlsx or .xls.")]
        public Task<ExcelImportSummaryDto> ImportProductionOrdersFromExcelAsync(
            [Description("The fileId from the attached-file note in the user's message.")] string fileId)
            => RunImportAsync("Import Production Orders", fileId, async (stream, userId) =>
            {
                var result = await _productionOrderService.UploadExcelAsync(stream, userId);
                var summary = new ExcelImportSummaryDto
                {
                    TotalRows = result.TotalRows,
                    SuccessCount = result.Imported,
                    FailedCount = result.Skipped,
                    Message = $"Imported {result.Imported} of {result.TotalRows} rows."
                };
                AddFailures(summary, result.Errors);
                return summary;
            });

        [Description("Update the MIN and Status of EXISTING Production Orders from the Excel file the user attached. Only " +
            "call this after the user has said they want to update MIN/status from the file. Same layout as the " +
            "production order import: Production Order No in column 1, MIN in column 9, Status in column 10 (row 1 = " +
            "header). Production orders that don't exist are reported, not created. Accepts .xlsx or .xls.")]
        public Task<ExcelImportSummaryDto> UpdateProductionOrderMinStatusFromExcelAsync(
            [Description("The fileId from the attached-file note in the user's message.")] string fileId)
            => RunImportAsync("Update Production Order MIN/Status", fileId, async (stream, userId) =>
            {
                var result = await _productionOrderService.UploadMinStatusExcelAsync(stream, userId);
                var summary = new ExcelImportSummaryDto
                {
                    TotalRows = result.TotalRows,
                    SuccessCount = result.UpdatedRows,
                    FailedCount = result.NotFoundProductionOrderNumbers.Count,
                    Message = $"Processed {result.TotalRows} rows. Updated {result.UpdatedRows}. " +
                              $"Not found: {result.NotFoundProductionOrderNumbers.Count}."
                };
                AddFailures(summary,
                    result.Errors.Concat(result.NotFoundProductionOrderNumbers.Select(po => $"Production Order '{po}' was not found.")));
                return summary;
            });

        [Description("Make Precheck in bulk: scan-and-verify many components against their assemblies' BOMs from the Excel " +
            "file the user attached. Only call this after the user has said they want to run precheck from the file. " +
            "Expected layout (from the Precheck BOM template): row 3 = headers, data from row 4; columns Drawing Number " +
            "(2), Qty (6), Parent Drawing (7), ProductionOrderNumber (9), IdNumber (10), QRCodeNumber (11). Each row is " +
            "processed independently; failed rows are reported with the reason. Accepts .xlsx or .xls.")]
        public Task<ExcelImportSummaryDto> MakePrecheckFromExcelAsync(
            [Description("The fileId from the attached-file note in the user's message.")] string fileId)
            => RunImportAsync("Make Precheck from Excel", fileId, async (stream, userId) =>
            {
                var result = await _precheckService.MakePrecheckFromExcelAsync(stream, userId);
                var summary = new ExcelImportSummaryDto
                {
                    TotalRows = result.TotalRows,
                    SuccessCount = result.SuccessCount,
                    FailedCount = result.FailedCount,
                    Message = $"{result.SuccessCount} of {result.TotalRows} QR codes prechecked."
                };
                AddFailures(summary, result.Results
                    .Where(r => !r.Success)
                    .Select(r => $"PO {r.ProductionOrderNumber}, ID {r.IdNumber}, QR {r.QrCodeNumber}: {r.Message}"));
                return summary;
            });

        [Description("Store In many QR-coded components at once from the Excel file the user attached - the bulk version " +
            "of storing in one QR code. Only call this after the user has said they want to bulk store-in from the " +
            "file. Expected layout: row 1 = header (Sr. No. | QrCodeNumber), QR code numbers in column 2 from row 2. " +
            "Each QR code is validated independently; failed ones are reported with the reason. Accepts .xlsx or .xls.")]
        public Task<ExcelImportSummaryDto> BulkStoreInFromExcelAsync(
            [Description("The fileId from the attached-file note in the user's message.")] string fileId)
            => RunImportAsync("Bulk Store In", fileId, async (stream, _) =>
            {
                var result = await _qrCodeService.BulkComponentStoreInFromExcelService(stream);
                var summary = new ExcelImportSummaryDto
                {
                    TotalRows = result.TotalCount,
                    SuccessCount = result.SuccessCount,
                    FailedCount = result.FailureCount,
                    Message = $"{result.SuccessCount} of {result.TotalCount} QR codes stored in."
                };
                AddFailures(summary, result.Results.Where(r => !r.Success).Select(r => $"QR {r.QrCodeNumber}: {r.Message}"));
                return summary;
            });

        [Description("Run the STANDARD QR code generation script on the Excel file the user attached. Only call this after " +
            "the user has said they want to generate standard QR codes from the file. Requires an .xlsx file in the QR " +
            "code sample template layout. Success is decided by the script's exit code; relay its output.")]
        public Task<ScriptRunSummaryDto> RunStandardQrGenerationAsync(
            [Description("The fileId from the attached-file note in the user's message (.xlsx only).")] string fileId)
            => RunScriptAsync("Standard QR Generation", fileId,
                (path, userId) => _scripts.RunAsync(ExcelScriptRunner.StdQrGeneration, path, userId));

        [Description("Run the QR code IMPORT script (imports existing/old QR codes with their IR/MSN numbers) on the Excel " +
            "file the user attached. Only call this after the user has said they want to import QR codes from the file. " +
            "Requires an .xlsx file in the QR code sample template layout. Success is decided by the script's exit code; " +
            "relay its output.")]
        public Task<ScriptRunSummaryDto> RunQrCodeImportAsync(
            [Description("The fileId from the attached-file note in the user's message (.xlsx only).")] string fileId)
            => RunScriptAsync("QR Code Import", fileId,
                (path, userId) => _scripts.RunAsync(ExcelScriptRunner.QrCodeImport, path, userId));

        [Description("Run the MASTER DATA script, which needs TWO Excel files: the drawing-assembly file (template " +
            "'masterdata-drawing-assembly') and the drawing file (template 'masterdata-drawing'). Only call this after " +
            "the user has said they want to upload master data AND two files are attached with it clear which is which " +
            "- if only one is attached, or it is unclear which is which, ask. Both must be .xlsx. Success is decided by " +
            "the script's exit code; relay its output.")]
        public async Task<ScriptRunSummaryDto> RunMasterDataAsync(
            [Description("fileId of the drawing-assembly file.")] string drawingAssemblyFileId,
            [Description("fileId of the drawing file.")] string drawingFileId)
        {
            const string operation = "Master Data Upload";
            var userId = CurrentUserId();

            if (string.Equals(drawingAssemblyFileId, drawingFileId, StringComparison.OrdinalIgnoreCase))
                return new ScriptRunSummaryDto { Operation = operation, Message = "The two files must be different." };

            var path1 = _uploads.Resolve(drawingAssemblyFileId, userId, xlsxOnly: true, out var error1);
            if (path1 == null) return new ScriptRunSummaryDto { Operation = operation, Message = $"Drawing-assembly file: {error1}" };

            var path2 = _uploads.Resolve(drawingFileId, userId, xlsxOnly: true, out var error2);
            if (path2 == null) return new ScriptRunSummaryDto { Operation = operation, Message = $"Drawing file: {error2}" };

            try
            {
                return ToSummary(operation, await _scripts.RunMasterDataAsync(path1, path2, userId));
            }
            finally
            {
                _uploads.Delete(path1);
                _uploads.Delete(path2);
            }
        }

        private const string KnownTemplates =
            "Production Order template (import POs / update MIN-Status), Precheck BOM template, Bulk Store In template, " +
            "QR code sample (standard QR / QR import), Master data drawing-assembly and drawing files.";

        [Description("PREFERRED first step for an attached Excel file: work out what the file is from its layout and run the " +
            "right action straight away - no need to ask the user what to do. Detects: Production Order sheet (imports new " +
            "POs, or updates MIN/Status when those POs already exist), Precheck BOM sheet (bulk precheck), Bulk Store In " +
            "sheet, QR code sample (QR import), and the two Master data files (needs both). If the result says " +
            "NeedsUserChoice or the file can't be used yet, nothing was run: relay its Message/Options and ask the user, " +
            "then call the specific tool for their choice.")]
        public async Task<AutoProcessResultDto> ProcessAttachedExcelFileAsync(
            [Description("The fileId from the attached-file note in the user's message.")] string fileId,
            [Description("Only for Master data, which needs TWO files: the fileId of the other master data file " +
                "(attached in the same message or earlier in this conversation). Omit otherwise.")] string? otherFileId)
        {
            var userId = CurrentUserId();
            var path = _uploads.Resolve(fileId, userId, xlsxOnly: false, out var error);
            if (path == null) return new AutoProcessResultDto { Message = error! };

            var detection = await Task.Run(() => ExcelFileDetector.Detect(path));

            switch (detection.Kind)
            {
                case ExcelFileKind.Unreadable:
                    _uploads.Delete(path);
                    return new AutoProcessResultDto
                    {
                        DetectedType = "Unreadable",
                        Message = "The file could not be read as an Excel workbook - it may be corrupt, password-protected or an old .xls file. " +
                                  "Ask the user to save it as .xlsx and attach it again."
                    };

                case ExcelFileKind.Unknown:
                    _uploads.Delete(path);
                    return new AutoProcessResultDto
                    {
                        DetectedType = "Unknown",
                        Message = $"The file doesn't match any supported template. Supported: {KnownTemplates} " +
                                  "Ask the user to use one of the templates and attach it again."
                    };

                case ExcelFileKind.Precheck:
                    return new AutoProcessResultDto
                    {
                        DetectedType = "Precheck BOM sheet",
                        Action = "Make Precheck from Excel",
                        ImportResult = await MakePrecheckFromExcelAsync(fileId)
                    };

                case ExcelFileKind.BulkStoreIn:
                    return new AutoProcessResultDto
                    {
                        DetectedType = "Bulk Store In sheet",
                        Action = "Bulk Store In",
                        ImportResult = await BulkStoreInFromExcelAsync(fileId)
                    };

                case ExcelFileKind.QrScript:
                    return new AutoProcessResultDto
                    {
                        DetectedType = "QR code sheet",
                        Action = "QR Code Import",
                        ScriptResult = await RunQrCodeImportAsync(fileId)
                    };

                case ExcelFileKind.ProductionOrderSheet:
                    return await ProcessProductionOrderSheetAsync(fileId, detection.ProductionOrderRows);

                default:
                    return await ProcessMasterDataFileAsync(fileId, otherFileId, detection.Kind, userId);
            }
        }

        // The same template is used to import new POs and to update MIN/Status of existing ones, so the data decides:
        // no PO number exists yet -> import; every row (PO + series + Start ID) already exists -> update; anything in
        // between could be either, so the user is asked.
        private async Task<AutoProcessResultDto> ProcessProductionOrderSheetAsync(string fileId, List<(string Po, string StartId)> rows)
        {
            const string detected = "Production Order sheet";
            if (rows.Count == 0)
            {
                return new AutoProcessResultDto
                {
                    DetectedType = detected,
                    Message = "The Production Order sheet has no data rows (Production Order numbers start at row 2)."
                };
            }

            var existingNumbers = await _productionOrderService.GetExistingProductionOrderNumbersAsync(rows.Select(r => r.Po));
            if (existingNumbers.Count == 0)
            {
                return new AutoProcessResultDto
                {
                    DetectedType = detected,
                    Action = "Import Production Orders (none of the POs exist yet)",
                    ImportResult = await ImportProductionOrdersFromExcelAsync(fileId)
                };
            }

            var exactMatches = await _productionOrderService.CountExistingProductionOrderRowsAsync(rows);
            if (exactMatches == rows.Count)
            {
                return new AutoProcessResultDto
                {
                    DetectedType = detected,
                    Action = "Update MIN/Status (every PO in the file already exists)",
                    ImportResult = await UpdateProductionOrderMinStatusFromExcelAsync(fileId)
                };
            }

            return new AutoProcessResultDto
            {
                DetectedType = detected,
                NeedsUserChoice = true,
                Options = new List<string> { "Import new Production Orders", "Update MIN/Status of existing Production Orders" },
                Message = $"This Production Order sheet could be either action: {existingNumbers.Count} of its " +
                          $"{rows.Select(r => r.Po).Distinct(StringComparer.OrdinalIgnoreCase).Count()} PO numbers already exist " +
                          $"({exactMatches} of {rows.Count} rows exist exactly), so it is a mix. Nothing was run. " +
                          "Ask the user which action they want, then call the matching tool with this fileId."
            };
        }

        // Master data needs both files. The first call (one file) only reports what is missing; once the other file
        // is supplied the pair is checked (one assembly + one drawing file) and the script is run.
        private async Task<AutoProcessResultDto> ProcessMasterDataFileAsync(string fileId, string? otherFileId, ExcelFileKind kind, int userId)
        {
            var thisIsAssembly = kind == ExcelFileKind.MasterDataAssembly;
            var thisName = thisIsAssembly ? "drawing-assembly" : "drawing";
            var missingName = thisIsAssembly ? "drawing" : "drawing-assembly";
            var detected = $"Master data ({thisName} file)";

            if (string.IsNullOrWhiteSpace(otherFileId))
            {
                return new AutoProcessResultDto
                {
                    DetectedType = detected,
                    Message = $"This is the master data {thisName} file. Master data needs TWO files and nothing was run yet - " +
                              $"ask the user to attach the {missingName} file, then call this tool again with this file's fileId " +
                              "and the new file's fileId as otherFileId."
                };
            }

            var otherPath = _uploads.Resolve(otherFileId, userId, xlsxOnly: false, out var otherError);
            if (otherPath == null)
                return new AutoProcessResultDto { DetectedType = detected, Message = $"The other file: {otherError}" };

            var other = await Task.Run(() => ExcelFileDetector.Detect(otherPath));
            var complementary = (kind == ExcelFileKind.MasterDataAssembly && other.Kind == ExcelFileKind.MasterDataDrawing) ||
                                (kind == ExcelFileKind.MasterDataDrawing && other.Kind == ExcelFileKind.MasterDataAssembly);
            if (!complementary)
            {
                return new AutoProcessResultDto
                {
                    DetectedType = detected,
                    Message = $"The first file is the master data {thisName} file, but the other file is not the {missingName} file " +
                              $"(it was detected as: {other.Kind}). Nothing was run. Ask the user to attach the correct {missingName} file."
                };
            }

            return new AutoProcessResultDto
            {
                DetectedType = "Master data (drawing-assembly + drawing files)",
                Action = "Master Data Upload",
                ScriptResult = await RunMasterDataAsync(
                    drawingAssemblyFileId: thisIsAssembly ? fileId : otherFileId,
                    drawingFileId: thisIsAssembly ? otherFileId : fileId)
            };
        }
    }
}
