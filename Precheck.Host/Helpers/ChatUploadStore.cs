namespace Precheck.Host.Helpers
{
    public record ChatUploadInfo(string FileId, string FileName, long SizeBytes);

    // Holds Excel files a user attached in the chatbot until the agent uses them in a tool. The model never sees
    // file bytes or paths: the upload endpoint hands back a fileId, the chat message carries that id, and the tools
    // resolve it here. Files live under {UploadPath}\chat\{userId}\ so a user can only ever resolve their own ids.
    public class ChatUploadStore
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromHours(2);
        private static readonly string[] Extensions = { ".xlsx", ".xls" };

        private readonly string _root;

        public ChatUploadStore(IConfiguration config)
        {
            _root = Path.Combine(config["UploadPath"]!, "chat");
        }

        // The caller has already run ExcelFileValidator.Validate, so the extension is .xlsx or .xls.
        public async Task<ChatUploadInfo> SaveAsync(IFormFile file, int userId)
        {
            var dir = UserDirectory(userId);
            Directory.CreateDirectory(dir);
            PurgeExpired(dir);

            var id = Guid.NewGuid().ToString("N");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var path = Path.Combine(dir, id + extension);

            using (var stream = new FileStream(path, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            return new ChatUploadInfo(id, Path.GetFileName(file.FileName), file.Length);
        }

        // Returns the full path of the user's uploaded file, or null with a message the model can relay.
        public string? Resolve(string? fileId, int userId, bool xlsxOnly, out string? error)
        {
            error = null;

            if (userId <= 0)
            {
                error = "Could not identify the logged-in user.";
                return null;
            }
            if (!Guid.TryParse(fileId, out var id))
            {
                error = "Invalid fileId. Use the fileId from the attached-file note in the user's message.";
                return null;
            }

            var dir = UserDirectory(userId);
            foreach (var extension in Extensions)
            {
                var candidate = Path.Combine(dir, id.ToString("N") + extension);
                if (!File.Exists(candidate)) continue;

                if (xlsxOnly && extension != ".xlsx")
                {
                    error = "This action only accepts .xlsx files. Ask the user to attach an .xlsx version.";
                    return null;
                }
                return candidate;
            }

            error = "The attached file was not found - it may have expired. Ask the user to attach it again.";
            return null;
        }

        public void Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort - PurgeExpired removes it later.
            }
        }

        private string UserDirectory(int userId) => Path.Combine(_root, userId.ToString());

        private static void PurgeExpired(string dir)
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                try
                {
                    if (DateTime.UtcNow - File.GetCreationTimeUtc(file) > MaxAge) File.Delete(file);
                }
                catch (IOException)
                {
                    // In use or already gone - skip.
                }
            }
        }
    }
}
