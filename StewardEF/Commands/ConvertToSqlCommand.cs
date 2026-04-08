namespace StewardEF.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

internal class ConvertToSqlCommand : Command<ConvertToSqlCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[MigrationsDirectory]")]
        public string? MigrationsDirectory { get; set; }

        [CommandOption("-p|--project")]
        [Description("Explicit path to the target migrations project (.csproj)")]
        public string? ProjectPath { get; set; }

        [CommandOption("-s|--startup-project")]
        [Description("Explicit path to the startup project (.csproj) used by dotnet ef")]
        public string? StartupProjectPath { get; set; }

        [CommandOption("-c|--context")]
        [Description("Explicit DbContext name to use when the startup project exposes multiple contexts")]
        public string? DbContextName { get; set; }

        [CommandOption("--configuration")]
        [Description("Build configuration to use with dotnet ef (defaults to the EF Core default)")]
        public string? Configuration { get; set; }

        [CommandOption("-m|--migration")]
        [Description("Specific migration name or ID to convert")]
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

        ConvertMigrationToSql(
            directory,
            settings.ProjectPath,
            settings.MigrationName,
            settings.StartupProjectPath,
            settings.DbContextName,
            settings.Configuration);
        return 0;
    }

    static void ConvertMigrationToSql(
        string directory,
        string? projectPath,
        string? migrationName,
        string? startupProjectPath,
        string? dbContextName,
        string? configuration)
    {
        // Find the project file
        var projectFile = EfMigrationTooling.FindProjectFile(directory, projectPath);
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
        var migrationId = EfMigrationTooling.ExtractMigrationId(designerFile);
        if (migrationId == null)
        {
            AnsiConsole.MarkupLine("[red]Could not extract migration ID from Designer file.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[dim]Generating SQL scripts...[/]");

        var options = new EfScriptOptions(projectFile, startupProjectPath, dbContextName, configuration);

        // Generate SQL scripts
        var (upSql, upError) = EfMigrationTooling.ExecuteEfScript("0", migrationId, options);
        if (upSql == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate Up SQL script.[/]");
            if (!string.IsNullOrWhiteSpace(upError))
            {
                EfMigrationTooling.WriteEfScriptError(upError);
            }
            return;
        }

        var (downSql, downError) = EfMigrationTooling.ExecuteEfScript(migrationId, "0", options);
        if (downSql == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate Down SQL script.[/]");
            if (!string.IsNullOrWhiteSpace(downError))
            {
                EfMigrationTooling.WriteEfScriptError(downError);
            }
            return;
        }

        // Replace migration content with SQL
        ReplaceWithSqlScript(migrationFile, upSql, downSql);

        AnsiConsole.MarkupLine($"[green]Migration converted to SQL successfully! {Emoji.Known.Sparkles}[/]");
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
