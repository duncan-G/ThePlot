using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThePlot.AppHost;

internal static class AzureFunctionsCoreTools
{
    /// <summary>
    /// Ensures Azure Functions Core Tools (func) is available before starting the Functions project.
    /// On Ubuntu/Debian, runs infra/install-azure-functions-core-tools.sh (requires passwordless sudo).
    /// See https://learn.microsoft.com/azure/azure-functions/functions-run-local
    /// </summary>
    public static async Task EnsureAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        var baseDir = AppContext.BaseDirectory;
        var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        var repoRoot = Path.GetFullPath(Path.Combine(projectDir, "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "infra", "install-azure-functions-core-tools.sh");

        if (!File.Exists(scriptPath))
            return;

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "bash",
            ArgumentList = { scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Azure Functions Core Tools installation failed: {stderr.Trim()}. See https://aka.ms/azfunc-dotnet-run-error");
        }

        var toolsPath = stdout.Trim();
        if (!string.IsNullOrEmpty(toolsPath))
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", $"{toolsPath}:{path}", EnvironmentVariableTarget.Process);
        }
    }
}
