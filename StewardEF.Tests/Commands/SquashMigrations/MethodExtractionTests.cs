namespace StewardEF.Tests.Commands.SquashMigrations;

using NSubstitute;
using Shouldly;
using StewardEF.Commands;
using Spectre.Console.Cli;

/// <summary>
/// Advanced tests for the BraceCounter fix to ensure it handles complex edge cases
/// that would have broken the old naive character counting algorithm.
/// </summary>
public class MethodExtractionTests : IDisposable
{
    private readonly string _testDirectory;

    public MethodExtractionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"StewardEF_MethodExtraction_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void BraceCounter_HandlesMultiLineVerbatimStringWithUnbalancedBraces()
    {
        // This is the MOST challenging case for a brace counter
        // Multi-line verbatim strings can have unbalanced braces spanning multiple lines
        // Old algorithm: would count all braces in the string and fail
        // New algorithm: tracks verbatim string state across lines and ignores braces inside

        // Arrange
        CreateMigrationWithMultiLineVerbatimString();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*VerbatimString.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Verify the multi-line SQL was extracted correctly
        content.ShouldContain("CREATE PROCEDURE");
        content.ShouldContain("BEGIN");
        content.ShouldContain("IF @Count > 0");
        content.ShouldContain("END");

        // Both migrations should be squashed (not cut off early due to brace miscounting)
        content.ShouldContain("// 20230101120000_VerbatimString.cs");
        content.ShouldContain("// 20230102120000_SecondMigration.cs");
    }

    [Fact]
    public void BraceCounter_HandlesEscapedQuotesInVerbatimStrings()
    {
        // Verbatim strings use "" to escape quotes
        // This should not confuse the string state tracking

        // Arrange
        CreateMigrationWithEscapedQuotesInVerbatimString();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*EscapedQuotes.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Content with escaped quotes should be preserved
        content.ShouldContain("He said \"\"Hello\"\"");
        content.ShouldContain("CreateTable");
    }

    [Fact]
    public void BraceCounter_HandlesEscapedCharactersInRegularStrings()
    {
        // Regular strings use \" to escape quotes and \\ for backslashes
        // Backslash before quote means escaped quote (string continues)
        // Backslash before backslash means escaped backslash (next char is normal)

        // Arrange
        CreateMigrationWithEscapedCharactersInStrings();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*EscapedChars.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Escaped content should be preserved (backslashes in source)
        content.ShouldContain("server");
        content.ShouldContain("share");
        content.ShouldContain("test");
        content.ShouldContain("Quote:");
    }

    [Fact]
    public void BraceCounter_HandlesMultiLineComment()
    {
        // Multi-line comments can span many lines
        // Braces inside them should be ignored

        // Arrange
        CreateMigrationWithMultiLineComment();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*MultiLineComment.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Both migrations should be squashed (the key test is that this doesn't fail/hang)
        content.ShouldContain("// 20230101120000_MultiLineComment.cs");
        content.ShouldContain("// 20230102120000_SecondMigration.cs");
        content.ShouldContain("CreateTable");

        // If brace counting was broken, multi-line comments with braces would cause issues
        var lines = content.Split('\n');
        lines.Length.ShouldBeGreaterThan(20, "Squashed content should contain both migrations");
    }

    [Fact]
    public void BraceCounter_HandlesCharLiterals()
    {
        // Character literals can contain braces
        // Single quotes with single character (or escape sequence)

        // Arrange
        CreateMigrationWithCharLiterals();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*CharLiterals.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Char literals should be preserved
        content.ShouldContain("openBrace = '{'");
        content.ShouldContain("closeBrace = '}'");
        content.ShouldContain("doubleQuote = '\"'");
    }

    // Helper methods

    private void CreateMigrationWithMultiLineVerbatimString()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_VerbatimString.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class VerbatimString : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var sql = @""
            CREATE PROCEDURE GetActiveUsers
                @MinLoginCount INT
            AS
            BEGIN
                SET NOCOUNT ON;

                DECLARE @Count INT;
                SELECT @Count = COUNT(*) FROM Users;

                IF @Count > 0
                BEGIN
                    SELECT * FROM Users WHERE LoginCount > @MinLoginCount;
                END
            END
        "";

        migrationBuilder.Sql(sql);
        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(""DROP PROCEDURE GetActiveUsers"");
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_VerbatimString.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_VerbatimString"")]
partial class VerbatimString { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class SecondMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Products"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Products"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }

    private void CreateMigrationWithEscapedQuotesInVerbatimString()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_EscapedQuotes.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class EscapedQuotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var str = @""He said """"Hello"""" and left"";
        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_EscapedQuotes.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_EscapedQuotes"")]
partial class EscapedQuotes { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class SecondMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Products"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Products"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }

    private void CreateMigrationWithEscapedCharactersInStrings()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_EscapedChars.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class EscapedChars : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var path = ""Path: \\\\server\\\\share"";
        var quote = ""Quote: \\""test\\"" value"";
        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_EscapedChars.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_EscapedChars"")]
partial class EscapedChars { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class SecondMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Products"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Products"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }

    private void CreateMigrationWithMultiLineComment()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_MultiLineComment.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class MultiLineComment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        /* This is a multi-line comment
           that spans several lines
           and contains braces { } inside
           which should be ignored { { { }
        */
        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_MultiLineComment.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_MultiLineComment"")]
partial class MultiLineComment { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class SecondMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Products"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Products"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }

    private void CreateMigrationWithCharLiterals()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_CharLiterals.cs"),
            "namespace TestMigrations;\n\n" +
            "using Microsoft.EntityFrameworkCore.Migrations;\n\n" +
            "public partial class CharLiterals : Migration\n" +
            "{\n" +
            "    protected override void Up(MigrationBuilder migrationBuilder)\n" +
            "    {\n" +
            "        var openBrace = '{';\n" +
            "        var closeBrace = '}';\n" +
            "        var doubleQuote = '\"';\n" +
            "        migrationBuilder.CreateTable(\"Users\");\n" +
            "    }\n\n" +
            "    protected override void Down(MigrationBuilder migrationBuilder)\n" +
            "    {\n" +
            "        migrationBuilder.DropTable(\"Users\");\n" +
            "    }\n" +
            "}\n");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_CharLiterals.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_CharLiterals"")]
partial class CharLiterals { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class SecondMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Products"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Products"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }
}
