namespace StewardEF.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

internal class SquashMigrationsCommand : Command<SquashMigrationsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[MigrationsDirectory]")]
        public string? MigrationsDirectory { get; set; }

        [CommandOption("-y|--year")]
        public int? Year { get; set; }

        [CommandOption("-t|--target")]
        public string? TargetMigration { get; set; }

        [CommandOption("--skip-sql")]
        [Description("Skip automatic SQL conversion for migrations with rename operations")]
        public bool SkipSqlConversion { get; set; }
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

        SquashMigrations(directory, settings.Year, settings.TargetMigration, settings.SkipSqlConversion);
        return 0;
    }

    static void SquashMigrations(string directory, int? year, string? targetMigration, bool skipSqlConversion)
    {
        // Get all .cs files including Designer.cs files
        var files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();

        // Filter out the snapshot file
        var migrationFiles = files
            .Where(f => !Path.GetFileName(f).EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (year.HasValue)
        {
            migrationFiles = migrationFiles
                .Where(f => ParseYearFromFileName(f) == year.Value)
                .ToList();
        }

        if (targetMigration != null)
        {
            migrationFiles = migrationFiles
                .TakeWhile(f => !Path.GetFileName(f).Contains(targetMigration, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (migrationFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No migration files found to squash.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Squashing {migrationFiles.Count} migration files...[/]");

        // Get the first migration file
        var firstMigrationFile = migrationFiles.First();

        // STEP 1: Always do standard C# code squashing first
        var upResult = GetAggregatedMethodContent(migrationFiles, "Up", collectUsings: true);
        var downResult = GetAggregatedMethodContent(migrationFiles.AsEnumerable().Reverse(), "Down", collectUsings: true);

        var firstMigrationLines = File.ReadAllLines(firstMigrationFile).ToList();

        ReplaceMethodContent(firstMigrationLines, "Up", upResult.AggregatedContent);
        ReplaceMethodContent(firstMigrationLines, "Down", downResult.AggregatedContent);

        var allUsingStatements = new HashSet<string>(upResult.UsingStatements);
        foreach (var usingStmt in downResult.UsingStatements)
        {
            allUsingStatements.Add(usingStmt);
        }
        UpdateUsingStatements(firstMigrationLines, allUsingStatements);

        File.WriteAllLines(firstMigrationFile, firstMigrationLines);

        // STEP 2: Update designer file
        var latestDesignerFile = migrationFiles
            .Where(f => Path.GetFileName(f).EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        var newDesignerFileName = Path.Combine(directory, Path.GetFileNameWithoutExtension(firstMigrationFile) + ".Designer.cs");

        if (latestDesignerFile != null)
        {
            // Only move if the designer file is different
            if (!string.Equals(latestDesignerFile, newDesignerFileName, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(newDesignerFileName))
                {
                    File.Delete(newDesignerFileName);
                }
                File.Move(latestDesignerFile, newDesignerFileName);
            }

            // Update the class name and migration attribute in the designer file
            UpdateDesignerFile(newDesignerFileName, firstMigrationFile);
        }

        // STEP 3: Delete other migration files
        var filesToDelete = migrationFiles.Skip(2).ToList();
        foreach (var file in filesToDelete)
        {
            File.Delete(file);
        }

        AnsiConsole.MarkupLine($"[green]Migrations squashed successfully! {Emoji.Known.Sparkles}[/]");

        // STEP 4: Check if the squashed migration has rename operations
        if (HasProblematicRenameOperations(new[] { firstMigrationFile }))
        {
            if (skipSqlConversion)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Warning: Detected rename operations (RenameColumn, RenameTable, or RenameIndex) in squashed migration[/]");
                AnsiConsole.MarkupLine("[yellow]  SQL conversion was skipped due to --skip-sql flag[/]");
                AnsiConsole.MarkupLine("[yellow]  The squashed migration may have issues when applied to a fresh database[/]");
                return;
            }

            AnsiConsole.MarkupLine("[yellow]⚠ Detected rename operations in squashed migration[/]");
            AnsiConsole.MarkupLine("[yellow]Converting to SQL script format...[/]");

            // Try to find the project file
            var projectPath = FindProjectFile(directory);
            if (projectPath != null)
            {
                AnsiConsole.MarkupLine($"[dim]Found project: {Path.GetFileName(projectPath)}[/]");

                // Generate SQL from the squashed migration
                var migrationId = ExtractMigrationId(newDesignerFileName);
                if (migrationId != null)
                {
                    var upSql = ExecuteEfScript("0", migrationId, projectPath);
                    var downSql = ExecuteEfScript(migrationId, "0", projectPath);

                    if (upSql != null && downSql != null)
                    {
                        ReplaceWithSqlScript(firstMigrationFile, upSql, downSql);
                        AnsiConsole.MarkupLine("[green]✓ Successfully converted to SQL format[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠ Failed to generate SQL scripts[/]");
                        AnsiConsole.MarkupLine("[yellow]Squashed migration kept in C# format (may have issues on fresh database)[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Could not extract migration ID[/]");
                    AnsiConsole.MarkupLine("[yellow]Squashed migration kept in C# format (may have issues on fresh database)[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Could not find project file for SQL generation[/]");
                AnsiConsole.MarkupLine("[yellow]Squashed migration kept in C# format (may have issues on fresh database)[/]");
            }
        }
    }


    private class AggregatedMethodResult
    {
        public string AggregatedContent { get; set; } = null!;
        public HashSet<string> UsingStatements { get; set; } = [];
    }

    private static AggregatedMethodResult GetAggregatedMethodContent(
        IEnumerable<string> files,
        string methodName,
        bool collectUsings = false)
    {
        var result = new AggregatedMethodResult();
        var aggregatedContent = new StringBuilder();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var migrationLines = File.ReadAllLines(file);

            if (collectUsings)
            {
                var usings = ExtractUsingStatements(migrationLines);
                foreach (var u in usings)
                {
                    result.UsingStatements.Add(u);
                }
            }

            var methodContent = ExtractMethodContent(migrationLines, methodName);
            if (!string.IsNullOrEmpty(methodContent))
            {
                aggregatedContent.AppendLine("{");
                aggregatedContent.AppendLine($"// {fileName}");
                aggregatedContent.AppendLine(methodContent);
                aggregatedContent.AppendLine("}");
                aggregatedContent.AppendLine();
            }
        }

        result.AggregatedContent = aggregatedContent.ToString();
        return result;
    }

    private static string ExtractMethodContent(string[] lines, string methodName)
    {
        var methodSignature = $"protected override void {methodName}(MigrationBuilder migrationBuilder)";
        int methodStartIndex = -1;

        // Find the line index where the method starts
        for (int i = 0; i < lines.Length; i++)
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
            return string.Empty;
        }

        var methodContent = new StringBuilder();
        int braceLevel = 0;
        bool methodStarted = false;
        var braceCounter = new BraceCounter();

        // Start from the method signature line
        for (int i = methodStartIndex; i < lines.Length; i++)
        {
            var line = lines[i];

            if (!methodStarted)
            {
                // Look for the opening brace
                if (line.Contains("{"))
                {
                    methodStarted = true;
                    braceLevel += braceCounter.CountBraces(line);

                    // Capture the content after the opening brace on the same line
                    int braceIndex = line.IndexOf('{');
                    if (braceIndex + 1 < line.Length)
                    {
                        var afterBrace = line.Substring(braceIndex + 1);
                        if (!string.IsNullOrWhiteSpace(afterBrace))
                        {
                            methodContent.AppendLine(afterBrace);
                        }
                    }
                }
            }
            else
            {
                braceLevel += braceCounter.CountBraces(line);

                if (braceLevel == 0)
                {
                    // End of method
                    break;
                }
                else
                {
                    methodContent.AppendLine(line);
                }
            }
        }

        return methodContent.ToString().Trim();
    }

    /// <summary>
    /// Helper class to count braces while respecting C# syntax contexts
    /// (strings, comments, character literals). Maintains state across multiple lines
    /// for multi-line strings and comments.
    /// </summary>
    private class BraceCounter
    {
        private bool _inVerbatimString = false;
        private bool _inMultiLineComment = false;

        public int CountBraces(string line)
        {
            int count = 0;
            bool inString = false;
            bool inChar = false;
            bool inSingleLineComment = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';
                char prev = i > 0 ? line[i - 1] : '\0';

                // Handle multi-line comment state (carries across lines)
                if (_inMultiLineComment)
                {
                    if (c == '*' && next == '/')
                    {
                        _inMultiLineComment = false;
                        i++; // Skip the '/'
                    }
                    continue;
                }

                // Handle verbatim string state (carries across lines)
                if (_inVerbatimString)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote in verbatim string ("")
                        if (next == '"')
                        {
                            i++; // Skip the escaped quote
                            continue;
                        }
                        else
                        {
                            _inVerbatimString = false;
                        }
                    }
                    continue;
                }

                // Check for multi-line comment start
                if (!inString && !inChar && !inSingleLineComment && c == '/' && next == '*')
                {
                    _inMultiLineComment = true;
                    i++; // Skip the '*'
                    continue;
                }

                // Check for single-line comment start
                if (!inString && !inChar && c == '/' && next == '/')
                {
                    inSingleLineComment = true;
                    break; // Rest of line is comment
                }

                // Check for verbatim string start
                if (!inString && !inChar && !inSingleLineComment && c == '@' && next == '"')
                {
                    _inVerbatimString = true;
                    i++; // Skip the '"'
                    continue;
                }

                // Check for regular string start/end
                if (!inChar && !inSingleLineComment && c == '"' && prev != '\\')
                {
                    inString = !inString;
                    continue;
                }

                // Check for char literal start/end
                if (!inString && !inSingleLineComment && c == '\'' && prev != '\\')
                {
                    inChar = !inChar;
                    continue;
                }

                // Count braces only when in normal code (not in strings, chars, or comments)
                if (!inString && !inChar && !inSingleLineComment && !_inVerbatimString && !_inMultiLineComment)
                {
                    if (c == '{')
                    {
                        count++;
                    }
                    else if (c == '}')
                    {
                        count--;
                    }
                }
            }

            return count;
        }
    }

    private static IEnumerable<string> ExtractUsingStatements(string[] lines)
    {
        return lines.Where(line => line.TrimStart().StartsWith("using ")).Select(line => line.Trim());
    }

    private static void UpdateUsingStatements(List<string> lines, HashSet<string> usingStatements)
    {
        var namespaceIndex = lines.FindIndex(line => line.TrimStart().StartsWith("namespace "));
        if (namespaceIndex == -1)
        {
            AnsiConsole.MarkupLine("[red]No namespace declaration found in the file.[/]");
            return;
        }

        var namespaceLine = lines[namespaceIndex];
        var isScopedNamespace = namespaceLine.TrimEnd().EndsWith(";");
        var insertIndex = isScopedNamespace ? namespaceIndex + 1 : namespaceIndex + 2;

        // Remove any existing using statements immediately after the namespace declaration
        while (insertIndex < lines.Count && lines[insertIndex].TrimStart().StartsWith("using "))
        {
            lines.RemoveAt(insertIndex);
        }

        // Sort and prepare the usings
        var sortedUsings = usingStatements.OrderBy(u => u).ToList();
        if (isScopedNamespace)
        {
            // Insert an empty line after the namespace for readability in scoped namespaces
            sortedUsings.Insert(0, "");
            sortedUsings.Add(Environment.NewLine);
            lines.InsertRange(insertIndex, sortedUsings);
            return;
        }

        var indentation = GetIndentation(lines[namespaceIndex]) + "    ";
        var indentedUsings = sortedUsings.Select(u => !string.IsNullOrEmpty(u) ? indentation + u : u).ToList();
        indentedUsings.Add(Environment.NewLine);
        lines.InsertRange(insertIndex, indentedUsings);
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

    private static int ParseYearFromFileName(string fileName)
    {
        var match = Regex.Match(Path.GetFileName(fileName), @"^\d{4}");
        return match.Success ? int.Parse(match.Value) : int.MaxValue;
    }
    private static void UpdateDesignerFile(string designerFilePath, string firstMigrationFilePath)
    {
        var designerContent = File.ReadAllText(designerFilePath);
        var firstMigrationFileName = Path.GetFileNameWithoutExtension(firstMigrationFilePath);

        // Extract class name from migration filename
        // Format: YYYYMMDDHHMMSS_MigrationName
        // Extract everything after the 14-digit timestamp and underscore
        var className = ExtractMigrationClassName(firstMigrationFileName);

        // Replace the Migration attribute (allow optional whitespace)
        designerContent = Regex.Replace(designerContent, @"\[Migration\(\s*""[^""]*""\s*\)\]", $"[Migration(\"{firstMigrationFileName}\")]");

        // Replace the class name - need to handle names with underscores
        designerContent = Regex.Replace(designerContent, @"partial class [\w_]+", $"partial class {className}");


        File.WriteAllText(designerFilePath, designerContent);
    }

    private static string ExtractMigrationClassName(string migrationFileName)
    {
        // EF migration format: YYYYMMDDHHMMSS_MigrationName
        // The timestamp is always 14 digits followed by underscore
        // Migration name can contain underscores (e.g., Add_User_Table)
        var match = Regex.Match(migrationFileName, @"^\d{14}_(.+)$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Fallback to old behavior if format doesn't match
        return migrationFileName.Split('_').Last().Replace(" ", string.Empty);
    }

    /// <summary>
    /// Detects problematic rename operations where a renamed entity is subsequently dropped.
    /// This pattern causes issues in squashed migrations because the drop references an entity
    /// by its new name, but in a fresh database context, the rename history doesn't exist.
    /// </summary>
    internal static bool HasProblematicRenameOperations(IEnumerable<string> migrationFiles)
    {
        foreach (var file in migrationFiles)
        {
            // Skip Designer files
            if (file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = File.ReadAllText(file);

            if (HasRenamedEntitySubsequentlyDropped(content))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if any renamed entity (column, table, or index) is subsequently dropped.
    /// </summary>
    private static bool HasRenamedEntitySubsequentlyDropped(string content)
    {
        // Check columns: RenameColumn followed by DropColumn for the same table.newName
        var renamedColumns = FindRenamedColumns(content);
        var droppedColumns = FindDroppedColumns(content);

        foreach (var (table, newName, renamePos) in renamedColumns)
        {
            if (droppedColumns.Any(d =>
                d.position > renamePos &&
                string.Equals(d.table, table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Check tables: RenameTable followed by DropTable for the same newName
        var renamedTables = FindRenamedTables(content);
        var droppedTables = FindDroppedTables(content);

        foreach (var (newName, renamePos) in renamedTables)
        {
            if (droppedTables.Any(d =>
                d.position > renamePos &&
                string.Equals(d.name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Check indexes: RenameIndex followed by DropIndex for the same newName
        var renamedIndexes = FindRenamedIndexes(content);
        var droppedIndexes = FindDroppedIndexes(content);

        foreach (var (newName, renamePos) in renamedIndexes)
        {
            if (droppedIndexes.Any(d =>
                d.position > renamePos &&
                string.Equals(d.name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds all RenameColumn operations and extracts (table, newName, position).
    /// </summary>
    private static List<(string table, string newName, int position)> FindRenamedColumns(string content)
    {
        var results = new List<(string table, string newName, int position)>();
        var pattern = @"\.RenameColumn\s*\(([\s\S]*?)\);";

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var args = match.Groups[1].Value;
            var newNameMatch = Regex.Match(args, @"newName:\s*""([^""]+)""");
            var tableMatch = Regex.Match(args, @"table:\s*""([^""]+)""");

            if (newNameMatch.Success && tableMatch.Success)
            {
                results.Add((tableMatch.Groups[1].Value, newNameMatch.Groups[1].Value, match.Index));
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all DropColumn operations and extracts (table, name, position).
    /// </summary>
    private static List<(string table, string name, int position)> FindDroppedColumns(string content)
    {
        var results = new List<(string table, string name, int position)>();
        var pattern = @"\.DropColumn\s*\(([\s\S]*?)\);";

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var args = match.Groups[1].Value;
            var nameMatch = Regex.Match(args, @"name:\s*""([^""]+)""");
            var tableMatch = Regex.Match(args, @"table:\s*""([^""]+)""");

            if (nameMatch.Success && tableMatch.Success)
            {
                results.Add((tableMatch.Groups[1].Value, nameMatch.Groups[1].Value, match.Index));
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all RenameTable operations and extracts (newName, position).
    /// </summary>
    private static List<(string newName, int position)> FindRenamedTables(string content)
    {
        var results = new List<(string newName, int position)>();
        var pattern = @"\.RenameTable\s*\(([\s\S]*?)\);";

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var args = match.Groups[1].Value;
            var newNameMatch = Regex.Match(args, @"newName:\s*""([^""]+)""");

            if (newNameMatch.Success)
            {
                results.Add((newNameMatch.Groups[1].Value, match.Index));
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all DropTable operations and extracts (name, position).
    /// </summary>
    private static List<(string name, int position)> FindDroppedTables(string content)
    {
        var results = new List<(string name, int position)>();
        var pattern = @"\.DropTable\s*\(([\s\S]*?)\);";

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var args = match.Groups[1].Value;
            var nameMatch = Regex.Match(args, @"name:\s*""([^""]+)""");

            if (nameMatch.Success)
            {
                results.Add((nameMatch.Groups[1].Value, match.Index));
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all RenameIndex operations and extracts (newName, position).
    /// </summary>
    private static List<(string newName, int position)> FindRenamedIndexes(string content)
    {
        var results = new List<(string newName, int position)>();
        var pattern = @"\.RenameIndex\s*\(([\s\S]*?)\);";

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var args = match.Groups[1].Value;
            var newNameMatch = Regex.Match(args, @"newName:\s*""([^""]+)""");

            if (newNameMatch.Success)
            {
                results.Add((newNameMatch.Groups[1].Value, match.Index));
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all DropIndex operations and extracts (name, position).
    /// </summary>
    private static List<(string name, int position)> FindDroppedIndexes(string content)
    {
        var results = new List<(string name, int position)>();
        var pattern = @"\.DropIndex\s*\(([\s\S]*?)\);";

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var args = match.Groups[1].Value;
            var nameMatch = Regex.Match(args, @"name:\s*""([^""]+)""");

            if (nameMatch.Success)
            {
                results.Add((nameMatch.Groups[1].Value, match.Index));
            }
        }

        return results;
    }

    internal static string? FindProjectFile(string migrationsDirectory, string? explicitProjectPath = null)
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

    internal static string? ExtractMigrationId(string designerFilePath)
    {
        if (!File.Exists(designerFilePath))
            return null;

        var content = File.ReadAllText(designerFilePath);

        // Extract migration ID from [Migration("20231201123456_MigrationName")] attribute
        // Allow optional whitespace around the string parameter
        var match = Regex.Match(content, @"\[Migration\(\s*""([^""]+)""\s*\)\]");
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

        // Find the Up method and replace it
        var upMethodSignature = "protected override void Up(MigrationBuilder migrationBuilder)";
        var upMethodIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(upMethodSignature))
            {
                upMethodIndex = i;
                break;
            }
        }

        if (upMethodIndex != -1)
        {
            var indentation = GetIndentation(lines[upMethodIndex]);
            var upSqlContent = $@"        migrationBuilder.Sql(@""
{upSql.Replace("\"", "\"\"")}        "");";

            ReplaceMethodContent(lines, "Up", upSqlContent);
        }

        // Find the Down method and replace it
        var downSqlContent = $@"        migrationBuilder.Sql(@""
{downSql.Replace("\"", "\"\"")}        "");";

        ReplaceMethodContent(lines, "Down", downSqlContent);

        File.WriteAllLines(migrationFilePath, lines);
    }
}
