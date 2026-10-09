using ClosedXML.Excel;

namespace Precheck.Host.Helpers
{
    public enum ExcelFileKind
    {
        Unreadable,
        Unknown,
        ProductionOrderSheet,   // Production Order template - used for BOTH importing POs and updating MIN/Status
        Precheck,
        BulkStoreIn,
        QrScript,               // QR code sample layout - standard QR generation and QR import share it
        MasterDataAssembly,
        MasterDataDrawing
    }

    public record ExcelFileDetection(ExcelFileKind Kind, List<(string Po, string StartId)> ProductionOrderRows);

    // Recognises which of the supported upload templates a workbook is, from its header row(s) only - nothing is
    // imported or changed. Header comparison ignores case, spaces, underscores and punctuation. The Production
    // Order template is used for two different actions, so for that kind its (PO, Start ID) rows are returned and the
    // caller decides between them.
    public static class ExcelFileDetector
    {
        public static ExcelFileDetection Detect(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null || ws.LastRowUsed() == null) return new(ExcelFileKind.Unknown, new());

                var headers = new HashSet<string>();
                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                for (var col = 1; col <= lastCol; col++)
                {
                    var name = Norm(ws.Cell(1, col).GetString());
                    if (name.Length > 0) headers.Add(name);
                }

                // Order matters: the QR sample also has a "qrcodenumber" column in B, so it is checked before Store In.
                if (headers.Overlaps(new[] { "productionseriesid", "productionseries" }) && headers.Contains("componenttypeid"))
                    return new(ExcelFileKind.QrScript, new());

                if (Norm(ws.Cell(3, 9).GetString()) == "productionordernumber" &&
                    Norm(ws.Cell(3, 10).GetString()) == "idnumber" &&
                    Norm(ws.Cell(3, 11).GetString()) == "qrcodenumber")
                    return new(ExcelFileKind.Precheck, new());

                if (headers.Contains("assemblylnitemcode") && headers.Contains("childpartitemcode"))
                    return new(ExcelFileKind.MasterDataAssembly, new());

                if (headers.Contains("drawingnumber") && headers.Contains("lnitemcode") && headers.Contains("components"))
                    return new(ExcelFileKind.MasterDataDrawing, new());

                if (Norm(ws.Cell(1, 2).GetString()) == "qrcodenumber" && lastCol <= 3)
                    return new(ExcelFileKind.BulkStoreIn, new());

                if (Norm(ws.Cell(1, 6).GetString()) == "startidnumber" && Norm(ws.Cell(1, 7).GetString()) == "quantity")
                {
                    var lastRow = ws.LastRowUsed()!.RowNumber();
                    var poRows = new List<(string Po, string StartId)>();
                    for (var row = 2; row <= lastRow; row++)
                    {
                        var po = ws.Cell(row, 1).GetString().Trim();
                        if (po.Length > 0) poRows.Add((po, ws.Cell(row, 6).GetString().Trim()));
                    }
                    return new(ExcelFileKind.ProductionOrderSheet, poRows);
                }

                return new(ExcelFileKind.Unknown, new());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Corrupt, password-protected, or an old .xls (this reader only opens .xlsx).
                return new(ExcelFileKind.Unreadable, new());
            }
        }

        private static string Norm(string text)
            => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
