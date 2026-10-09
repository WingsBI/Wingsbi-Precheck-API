namespace Precheck.Host.Helpers
{
    // Shared check for every Excel upload endpoint: the file must be an Excel workbook (by extension AND by its
    // first bytes, so a renamed .exe/.pdf is refused) and must not be larger than 10 MB.
    public static class ExcelFileValidator
    {
        public const long MaxFileSizeBytes = 10 * 1024 * 1024;
        public const int MaxFileNameLength = 100;

        // .xlsx / .xlsm are zip packages ("PK\x03\x04"); .xls is an OLE2 compound file (D0 CF 11 E0 A1 B1 1A E1).
        private static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 };
        private static readonly byte[] OleSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        // Returns null when the file is acceptable, otherwise the message to send back with a 400.
        // allowXls: true for endpoints that read both .xlsx and .xls, false for .xlsx-only endpoints.
        // label: prefix for endpoints with several files (e.g. "file1").
        public static string? Validate(IFormFile file, bool allowXls = true, string? label = null)
        {
            var prefix = string.IsNullOrEmpty(label) ? string.Empty : $"{label}: ";

            if (!IsSafeFileName(file.FileName))
            {
                return $"{prefix}Invalid file name. Use up to {MaxFileNameLength} letters, digits, spaces, '-', '_', '.', '(' or ')'.";
            }

            var extension = Path.GetExtension(file.FileName);
            var isXlsx = string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
            var isXls = string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);

            if (!isXlsx && !(allowXls && isXls))
            {
                return allowXls
                    ? $"{prefix}Invalid file format. Please upload an Excel file (.xlsx or .xls)"
                    : $"{prefix}Only .xlsx files are accepted.";
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return $"{prefix}File size must not exceed 10 MB.";
            }

            if (!HasExcelSignature(file, isXlsx))
            {
                return $"{prefix}The file is not a valid Excel file.";
            }

            return null;
        }

        // The client's name is only ever checked and logged, never used as a path: a name with path parts
        // ("../x.xlsx", "a/b.xlsx", "a\b.xlsx"), control characters or an unusual character set is refused.
        private static bool IsSafeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxFileNameLength) return false;
            if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)) return false;
            if (name.Contains("..", StringComparison.Ordinal)) return false;
            return name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' or '(' or ')');
        }

        // For log messages: the name with anything outside the safe set removed (stops log injection via line breaks).
        public static string SafeNameForLog(string? name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' or '(' or ')').ToArray());
            return cleaned.Length > MaxFileNameLength ? cleaned[..MaxFileNameLength] : cleaned;
        }

        private static bool HasExcelSignature(IFormFile file, bool expectZip)
        {
            var expected = expectZip ? ZipSignature : OleSignature;
            var header = new byte[expected.Length];

            using var stream = file.OpenReadStream();
            var read = 0;
            while (read < header.Length)
            {
                var n = stream.Read(header, read, header.Length - read);
                if (n == 0) return false;
                read += n;
            }
            return header.AsSpan().SequenceEqual(expected);
        }
    }
}
