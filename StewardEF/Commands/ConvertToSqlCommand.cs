namespace StewardEF.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.Text.RegularExpressions;

internal class ConvertToSqlCommand : Command<ConvertToSqlCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[MigrationsDirectory]")]
        public string? MigrationsDirectory { get; set; }

        [CommandOption("-p|--project")]
        public string? ProjectPath { get; set; }

        [CommandOption("-m|--migration")]
        public string? MigrationName { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var directory = settings.MigrationsDirectory
                        ?? AnsiConsole.Ask<string>("[green]Enter the migrations directory path:[/]");

        if (string.IsNullOrWhiteSpace(directory))
        {
            AnsiConsole.MarkupLine("[red]The specified directory is invalid.[/]");
            return 1;
        }

        if (!Directory.Exists(directory))
        {
            AnsiConsole.MarkupLine("[red]The specified directory does not exist.[/]");
            return 1;
        }

        ConvertMigrationToSql(directory, settings.ProjectPath, settings.MigrationName);
        return 0;
    }

    static void ConvertMigrationToSql(string directory, string? projectPath, string? migrationName)
    {
        // Find the project file
        var projectFile = FindProjectFile(directory, projectPath);
        if (projectFile == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find a .csproj file. Use --project to specify the path.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Using project: {Path.GetFileName(projectFile)}[/]");

        // Find the migration file to convert
        string? migrationFile;
        string? designerFile;

        if (!string.IsNullOrWhiteSpace(migrationName))
        {
            // Find specific migration by name
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
            migrationFile = files.FirstOrDefault(f =>
                Path.GetFileName(f).Contains(migrationName, StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase));

            if (migrationFile == null)
            {
                AnsiConsole.MarkupLine($"[red]Could not find migration: {migrationName}[/]");
                return;
            }

            designerFile = migrationFile.Replace(".cs", ".Designer.cs");
        }
        else
        {
            // Find the most recent migration (excluding snapshot)
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
                .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f)
                .ToList();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No migration files found in the directory.[/]");
                return;
            }

            migrationFile = files.First();
            designerFile = migrationFile.Replace(".cs", ".Designer.cs");
        }

        if (!File.Exists(designerFile))
        {
            AnsiConsole.MarkupLine($"[red]Designer file not found: {Path.GetFileName(designerFile)}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Converting migration to SQL: {Path.GetFileName(migrationFile)}[/]");

        // Extract migration ID from designer file
        var migrationId = ExtractMigrationId(designerFile);
        if (migrationId == null)
        {
            AnsiConsole.MarkupLine("[red]Could not extract migration ID from Designer file.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[dim]Generating SQL scripts...[/]");

        // Generate SQL scripts
        var upSql = ExecuteEfScript("0", migrationId, projectFile);
        if (upSql == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate Up SQL script.[/]");
            return;
        }

        var downSql = ExecuteEfScript(migrationId, "0", projectFile);
        if (downSql == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate Down SQL script.[/]");
            return;
        }

        // Replace migration content with SQL
        ReplaceWithSqlScript(migrationFile, upSql, downSql);

        AnsiConsole.MarkupLine($"[green]Migration converted to SQL successfully! {Emoji.Known.Sparkles}[/]");
    }

    private static string? FindProjectFile(string migrationsDirectory, string? explicitProjectPath = null)
    {
        // If explicitly provided, validate and return
        if (!string.IsNullOrWhiteSpace(explicitProjectPath))
        {
            if (File.Exists(explicitProjectPath) && explicitProjectPath.EndsWith(".csproj"))
            {
                return Path.GetFullPath(explicitProjectPath);
            }
            AnsiConsole.MarkupLine($"[yellow]Warning: Specified project file not found: {explicitProjectPath}[/]");
        }

        // Search up the directory tree for a .csproj file
        var currentDir = new DirectoryInfo(Path.GetFullPath(migrationsDirectory));

        while (currentDir != null)
        {
            var projectFiles = currentDir.GetFiles("*.csproj");
            if (projectFiles.Length > 0)
            {
                // If multiple .csproj files, try to pick the most likely one
                var projectFile = projectFiles.Length == 1
                    ? projectFiles[0]
                    : projectFiles.FirstOrDefault(f => !f.Name.Contains(".Tests")) ?? projectFiles[0];

                return projectFile.FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    private static string? ExtractMigrationId(string designerFilePath)
    {
        if (!File.Exists(designerFilePath))
            return null;

        var content = File.ReadAllText(designerFilePath);

        // Extract migration ID from [Migration("20231201123456_MigrationName")] attribute
        var match = Regex.Match(content, @"\[Migration\(""([^""]+)""\)\]");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    private static string? ExecuteEfScript(string fromMigration, string toMigration, string projectPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"ef migrations script {fromMigration} {toMigration} --project \"{projectPath}\" --no-build",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to start dotnet ef process[/]");
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                AnsiConsole.MarkupLine($"[red]dotnet ef script failed: {error}[/]");
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error executing dotnet ef: {ex.Message}[/]");
            return null;
        }
    }

    private static void ReplaceWithSqlScript(string migrationFilePath, string upSql, string downSql)
    {
        var lines = File.ReadAllLines(migrationFilePath).ToList();

        // Sanitize SQL to remove statements that conflict with EF Core's runtime behavior
        upSql = SquashMigrationsCommand.SanitizeEfGeneratedSql(upSql);
        downSql = SquashMigrationsCommand.SanitizeEfGeneratedSql(downSql);

        // Replace Up method with SQL
        var upSqlContent = $@"        migrationBuilder.Sql(@""
{upSql.Replace("\"", "\"\"")}        "");";

        ReplaceMethodContent(lines, "Up", upSqlContent);

        // Replace Down method with SQL
        var downSqlContent = $@"        migrationBuilder.Sql(@""
{downSql.Replace("\"", "\"\"")}        "");";

        ReplaceMethodContent(lines, "Down", downSqlContent);

        File.WriteAllLines(migrationFilePath, lines);
    }

    private static void ReplaceMethodContent(List<string> lines, string methodName, string newContent)
    {
        var methodSignature = $"protected override void {methodName}(MigrationBuilder migrationBuilder)";
        var methodStartIndex = -1;

        // Find the line index where the method starts
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(methodSignature))
            {
                methodStartIndex = i;
                break;
            }
        }

        if (methodStartIndex == -1)
        {
            // Method not found
            return;
        }

        var indentation = GetIndentation(lines[methodStartIndex]);

        var braceLevel = 0;
        var methodStarted = false;
        var methodEndIndex = -1;

        // Start from the method signature line
        for (var i = methodStartIndex; i < lines.Count; i++)
        {
            var line = lines[i];

            if (!methodStarted)
            {
                // Look for the opening brace
                if (line.Contains("{"))
                {
                    methodStarted = true;
                    braceLevel += line.Count(c => c == '{') - line.Count(c => c == '}');
                }
            }
            else
            {
                braceLevel += line.Count(c => c == '{') - line.Count(c => c == '}');

                if (braceLevel == 0)
                {
                    methodEndIndex = i;
                    break;
                }
            }
        }

        if (methodEndIndex == -1) return;

        // Replace the method content
        var newMethodContent = new List<string>();

        // Add method signature and opening brace with appropriate indentation
        newMethodContent.Add(lines[methodStartIndex]);

        // Ensure opening brace is on its own line with correct indentation
        if (!lines[methodStartIndex].Trim().EndsWith("{"))
        {
            newMethodContent.Add(indentation + "{");
        }

        // Add the new content with proper indentation
        var contentIndentation = GetIndentation(lines[methodStartIndex]);
        var indentedContent = IndentContent(newContent, contentIndentation + "    ");
        newMethodContent.AddRange(indentedContent);

        // Add closing brace
        newMethodContent.Add(indentation + "}");

        // Replace the old method lines with the new ones
        lines.RemoveRange(methodStartIndex, methodEndIndex - methodStartIndex + 1);
        lines.InsertRange(methodStartIndex, newMethodContent);
    }

    private static string GetIndentation(string line)
    {
        var match = Regex.Match(line, @"^\s*");
        return match.Value;
    }

    private static List<string> IndentContent(string content, string indentation)
    {
        var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var indentedLines = lines.Select(line => indentation + line).ToList();
        return indentedLines;
    }
}
