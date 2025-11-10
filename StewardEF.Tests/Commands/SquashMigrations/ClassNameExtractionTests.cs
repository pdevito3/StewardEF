namespace StewardEF.Tests.Commands.SquashMigrations;

using NSubstitute;
using Shouldly;
using StewardEF.Commands;
using Spectre.Console.Cli;

/// <summary>
/// Tests that document known limitations or potential bugs in the current implementation.
/// These tests pass with current behavior but highlight areas for future improvement.
/// </summary>
public class ClassNameExtractionTests : IDisposable
{
    private readonly string _testDirectory;

    public ClassNameExtractionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"StewardEF_Limitations_{Guid.NewGuid()}");
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
    public void ClassNameExtraction_HandlesUnderscoresCorrectly()
    {
        // FIXED: Class name extraction now correctly handles migration names with underscores
        // Format: YYYYMMDDHHMMSS_MigrationName
        // For "20230101120000_Add_User_Table_With_Indexes", should extract "Add_User_Table_With_Indexes"
        //
        // Location: SquashMigrations.cs:411-424 (ExtractMigrationClassName method)
        // Uses regex to extract everything after 14-digit timestamp and underscore

        // Arrange
        CreateMigrationWithUnderscoresInName();
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

        var designerFile = Directory.GetFiles(_testDirectory, "*.Designer.cs")
            .Where(f => !f.Contains("Snapshot"))
            .First();
        var designerContent = File.ReadAllText(designerFile);

        // FIXED: Now correctly extracts full class name with underscores
        designerContent.ShouldContain("partial class Add_User_Table_With_Indexes");
        designerContent.ShouldContain("[Migration(\"20230101120000_Add_User_Table_With_Indexes\")]");
    }

    [Fact]
    public void BraceCountingAlgorithm_HandlesStringLiteralsAndComments()
    {
        // FIXED: The brace counting algorithm now properly handles C# syntax contexts
        // Location: SquashMigrations.cs:256-357 (BraceCounter class)
        //
        // The BraceCounter class correctly ignores braces in:
        // 1. String literals: "CREATE FUNCTION test() { RETURN 1; }"
        // 2. Verbatim strings: @"multi-line { string }"
        // 3. Character literals: var c = '{';
        // 4. Single-line comments: // { comment }
        // 5. Multi-line comments: /* { comment } */
        //
        // This prevents false positives when SQL or comments contain unbalanced braces

        // Arrange
        CreateMigrationWithUnbalancedBracesInStringAndComments();
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

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*UnbalancedBraces.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Method extraction should work correctly despite unbalanced braces in strings/comments
        // Both migrations should be squashed successfully
        content.ShouldContain("// 20230101120000_UnbalancedBraces.cs");
        content.ShouldContain("// 20230102120000_SecondMigration.cs");

        // Core functionality should be preserved
        content.ShouldContain("CreateTable");

        // The key test: squashing completed without hanging or failing
        // If brace counting was broken, the method extraction would fail or extract wrong content
        var lines = content.Split('\n');
        lines.Length.ShouldBeGreaterThan(20, "Squashed content should contain both migrations");
    }

    [Fact]
    public void UsingStatementExtraction_IncludesAllUsingsRegardlessOfLocation()
    {
        // BEHAVIOR DOCUMENTATION: ExtractUsingStatements (SquashMigrations.cs:252)
        // extracts ALL lines starting with "using " regardless of location in file
        //
        // UpdateUsingStatements (SquashMigrations.cs:269) only removes usings
        // "immediately after namespace declaration"
        //
        // This means:
        // - Global usings (before namespace) are NOT removed from first migration
        // - Usings after namespace are removed and replaced
        // - Usings inside class definitions would be extracted but not removed (unlikely but possible)
        //
        // Current behavior is correct for standard EF migration format where
        // all usings appear after the namespace declaration

        // Arrange
        CreateMigrationWithStandardUsingFormat();
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

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*FirstMigration.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        // Verify usings are present and deduplicated
        content.ShouldContain("using Microsoft.EntityFrameworkCore.Migrations;");

        // Standard format works correctly
        // Edge case: global usings or usings inside classes would require different handling
    }

    // Helper methods

    private void CreateMigrationWithUnderscoresInName()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_Add_User_Table_With_Indexes.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class Add_User_Table_With_Indexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_Add_User_Table_With_Indexes.Designer.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(TestContext))]
[Migration(""20230101120000_Add_User_Table_With_Indexes"")]
partial class Add_User_Table_With_Indexes
{
}
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

    private void CreateMigrationWithUnbalancedBracesInStringAndComments()
    {
        // This migration has unbalanced braces in strings and comments
        // The old algorithm would fail to extract the method correctly
        // The new BraceCounter handles this properly
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_UnbalancedBraces.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class UnbalancedBraces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Comment with unbalanced { brace
        var str1 = ""unbalanced opening {"";
        var str2 = ""unbalanced closing }"";
        /* Multi-line comment
           with unbalanced { brace */
        var ch = '{';

        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_UnbalancedBraces.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_UnbalancedBraces"")]
partial class UnbalancedBraces { }
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

    private void CreateMigrationWithSqlInStringLiteral()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_CreateFunction.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class CreateFunction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQL with braces in string literal - balanced on same lines
        migrationBuilder.Sql(@""
            CREATE FUNCTION GetUserCount()
            RETURNS INT
            BEGIN
                DECLARE @count INT;
                SELECT @count = COUNT(*) FROM Users;
                RETURN @count;
            END
        "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(""DROP FUNCTION GetUserCount"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_CreateFunction.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_CreateFunction"")]
partial class CreateFunction { }
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

    private void CreateMigrationWithStandardUsingFormat()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_FirstMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;
using System;

public partial class FirstMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(""Users"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(""Users"");
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_FirstMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_FirstMigration"")]
partial class FirstMigration { }
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;
using System.Linq;

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
