using System.Diagnostics;

namespace ThePlot.AppHost.Postgres;

internal static class SchemaMigrationsCommands
{
    public static async Task<ExecuteCommandResult> ExecuteRebuildSchemaAsync(
        ExecuteCommandContext context,
        string schemaMigrationsDir,
        IResourceWithConnectionString postgresDb)
    {
        var connectionString = await postgresDb
            .GetConnectionStringAsync(context.CancellationToken)
            ?? throw new InvalidOperationException("Connection string theplot-db not found. Ensure the application is running.");

        var buildInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build",
            WorkingDirectory = schemaMigrationsDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                ["ConnectionStrings__theplot-db"] = connectionString
            }
        };

        var buildProcess = Process.Start(buildInfo);
        if (buildProcess is null)
        {
            return CommandResults.Failure("Failed to start build process.");
        }

        await buildProcess.WaitForExitAsync(context.CancellationToken);
        if (buildProcess.ExitCode != 0)
        {
            var stderr = await buildProcess.StandardError.ReadToEndAsync(context.CancellationToken);
            return CommandResults.Failure($"Build failed with exit code {buildProcess.ExitCode}: {stderr}");
        }

        var runInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --no-build --rebuild-schema",
            WorkingDirectory = schemaMigrationsDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                ["ConnectionStrings__theplot-db"] = connectionString
            }
        };

        var runProcess = Process.Start(runInfo);
        if (runProcess is null)
        {
            return CommandResults.Failure("Failed to start schema migrations process.");
        }

        await runProcess.WaitForExitAsync(context.CancellationToken);
        if (runProcess.ExitCode != 0)
        {
            var stderr = await runProcess.StandardError.ReadToEndAsync(context.CancellationToken);
            return CommandResults.Failure($"Schema rebuild failed with exit code {runProcess.ExitCode}: {stderr}");
        }

        return CommandResults.Success();
    }
}
