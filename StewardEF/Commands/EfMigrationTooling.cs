namespace StewardEF.Commands;

using Spectre.Console;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

internal sealed record EfScriptOptions(
    string ProjectPath,
    string? StartupProjectPath = null,
    string? DbContextName = null,
    string? Configuration = null);

internal static class EfMigrationTooling
{
    internal static string? FindProjectFile(string migrationsDirectory, string? explicitProjectPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitProjectPath))
        {
            if (File.Exists(explicitProjectPath) && explicitProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(explicitProjectPath);
            }

            AnsiConsole.MarkupLine($"[yellow]Warning: Specified project file not found: {explicitProjectPath}[/]");
        }

        var currentDir = new DirectoryInfo(Path.GetFullPath(migrationsDirectory));

        while (currentDir != null)
        {
            var projectFiles = currentDir.GetFiles("*.csproj");
            if (projectFiles.Length > 0)
            {
                var projectFile = projectFiles.Length == 1
                    ? projectFiles[0]
                    : projectFiles.FirstOrDefault(f => !f.Name.Contains(".Tests", StringComparison.OrdinalIgnoreCase)) ?? projectFiles[0];

                return projectFile.FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    internal static string? ExtractMigrationId(string designerFilePath)
    {
        if (!File.Exists(designerFilePath))
        {
            return null;
        }

        var content = File.ReadAllText(designerFilePath);
        var match = Regex.Match(content, @"\[Migration\(\s*""([^""]+)""\s*\)\]");
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static ProcessStartInfo CreateEfScriptStartInfo(string fromMigration, string toMigration, EfScriptOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("ef");
        startInfo.ArgumentList.Add("migrations");
        startInfo.ArgumentList.Add("script");
        startInfo.ArgumentList.Add(fromMigration);
        startInfo.ArgumentList.Add(toMigration);
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.GetFullPath(options.ProjectPath));

        if (!string.IsNullOrWhiteSpace(options.StartupProjectPath))
        {
            startInfo.ArgumentList.Add("--startup-project");
            startInfo.ArgumentList.Add(Path.GetFullPath(options.StartupProjectPath));
        }

        if (!string.IsNullOrWhiteSpace(options.DbContextName))
        {
            startInfo.ArgumentList.Add("--context");
            startInfo.ArgumentList.Add(options.DbContextName);
        }

        if (!string.IsNullOrWhiteSpace(options.Configuration))
        {
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(options.Configuration);
        }

        startInfo.ArgumentList.Add("--no-build");
        return startInfo;
    }

    internal static (string? Output, string? Error) ExecuteEfScript(string fromMigration, string toMigration, EfScriptOptions options)
    {
        try
        {
            var startInfo = CreateEfScriptStartInfo(fromMigration, toMigration, options);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (null, "Failed to start dotnet ef process.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(error)
                    ? string.IsNullOrWhiteSpace(output) ? "dotnet ef script failed." : output.Trim()
                    : error.Trim();

                return (null, message);
            }

            return (output, null);
        }
        catch (Exception ex)
        {
            return (null, $"Error executing dotnet ef: {ex.Message}");
        }
    }

    internal static void WriteEfScriptError(string message)
    {
        AnsiConsole.MarkupLine("[red]dotnet ef script failed:[/]");

        foreach (var line in SplitLines(message))
        {
            AnsiConsole.WriteLine(line);
        }
    }

    private static IEnumerable<string> SplitLines(string message)
    {
        using var reader = new StringReader(message);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
