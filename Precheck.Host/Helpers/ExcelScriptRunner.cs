using System.Diagnostics;
using System.Text;

namespace Precheck.Host.Helpers
{
    public record ScriptRunResult(bool Success, int ExitCode, string Output, string Message);

    // Runs the external Excel scripts configured under ScriptPaths (the same executables ScriptController runs),
    // for callers that already hold the files on disk. The script's own exit code decides success.
    public class ExcelScriptRunner
    {
        public const string StdQrGeneration = "STDQRGeneration";
        public const string QrCodeImport = "QRCodeImport";
        public const string MasterData = "MasterData";

        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

        private readonly IConfiguration _config;
        private readonly ILogger<ExcelScriptRunner> _logger;

        public ExcelScriptRunner(IConfiguration config, ILogger<ExcelScriptRunner> logger)
        {
            _config = config;
            _logger = logger;
        }

        public Task<ScriptRunResult> RunAsync(string scriptKey, string excelPath, int userId)
            => ExecuteAsync(scriptKey, new[] { excelPath }, userId);

        // The master-data script looks for its input files in its own folder, so both are copied there first
        // (and removed afterwards), exactly as ScriptController.RunMasterData does.
        public async Task<ScriptRunResult> RunMasterDataAsync(string excelPath1, string excelPath2, int userId)
        {
            var scriptPath = _config[$"ScriptPaths:{MasterData}"];
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                return new ScriptRunResult(false, -1, string.Empty, "Script not found on server.");

            var scriptDir = Path.GetDirectoryName(scriptPath)!;
            var copy1 = Path.Combine(scriptDir, $"{Guid.NewGuid()}_masterdata1.xlsx");
            var copy2 = Path.Combine(scriptDir, $"{Guid.NewGuid()}_masterdata2.xlsx");

            try
            {
                File.Copy(excelPath1, copy1, overwrite: true);
                File.Copy(excelPath2, copy2, overwrite: true);
                return await ExecuteAsync(MasterData, new[] { copy1, copy2 }, userId);
            }
            finally
            {
                TryDelete(copy1);
                TryDelete(copy2);
            }
        }

        private async Task<ScriptRunResult> ExecuteAsync(string scriptKey, string[] excelPaths, int userId)
        {
            var scriptPath = _config[$"ScriptPaths:{scriptKey}"];
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                return new ScriptRunResult(false, -1, string.Empty, "Script not found on server.");

            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var path in excelPaths) psi.ArgumentList.Add(path);
            psi.ArgumentList.Add(userId.ToString());
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };

            _logger.LogInformation("[{Script}] Starting {Path} with {Count} file(s)", scriptKey, scriptPath, excelPaths.Length);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(Timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogWarning("[{Script}] Timed out after {Minutes} minutes and was killed", scriptKey, Timeout.TotalMinutes);
                return new ScriptRunResult(false, -1, Combine(stdout, stderr), $"The script did not finish within {Timeout.TotalMinutes} minutes and was stopped.");
            }
            // Flush any buffered output events once the process has exited.
            process.WaitForExit();

            var output = Combine(stdout, stderr);
            _logger.LogInformation("[{Script}] Exited with {ExitCode}", scriptKey, process.ExitCode);

            return process.ExitCode == 0
                ? new ScriptRunResult(true, 0, output, "Script executed successfully.")
                : new ScriptRunResult(false, process.ExitCode, output, "Script execution completed with errors.");
        }

        private static string Combine(StringBuilder stdout, StringBuilder stderr)
        {
            lock (stdout) lock (stderr)
            {
                return stderr.Length == 0
                    ? stdout.ToString()
                    : stdout + "\n--- STDERR ---\n" + stderr;
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete script working copy {Path}", path);
            }
        }
    }
}
