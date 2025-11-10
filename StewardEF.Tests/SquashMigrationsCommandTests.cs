namespace StewardEF.Tests;

using NSubstitute;
using Shouldly;
using StewardEF.Commands;
using Spectre.Console.Cli;

public class SquashMigrationsCommandTests : IDisposable
{
    private readonly string _testDirectory;

    public SquashMigrationsCommandTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"StewardEF_Tests_{Guid.NewGuid()}");
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
    public void Execute_ShouldReturnError_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = Path.Combine(_testDirectory, "NonExistent")
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Execute_ShouldReturnError_WhenDirectoryIsEmpty()
    {
        // Arrange
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = ""
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Execute_ShouldSquashMigrations_WhenValidDirectoryProvided()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        // Verify only the first migration and its designer remain (plus snapshot)
        var remainingFiles = Directory.GetFiles(_testDirectory, "*.cs");
        remainingFiles.Length.ShouldBe(3); // First migration, designer, and snapshot
    }

    [Fact]
    public void Execute_ShouldSquashMigrations_WithYearFilter()
    {
        // Arrange
        CreateTestMigrationFilesWithDifferentYears();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory,
            Year = 2023
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        // Should only squash 2023 migrations, leaving 2024 intact
        var remainingFiles = Directory.GetFiles(_testDirectory, "*.cs");
        remainingFiles.ShouldContain(f => f.Contains("20240101"));
    }

    [Fact]
    public void Execute_ShouldSquashMigrations_WithTargetMigration()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory,
            TargetMigration = "SecondMigration"
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        // Should squash only up to SecondMigration
        var remainingFiles = Directory.GetFiles(_testDirectory, "*.cs");
        remainingFiles.ShouldContain(f => f.Contains("ThirdMigration"));
    }

    [Fact]
    public void Execute_ShouldPreserveUpMethodContent()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*FirstMigration.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        content.ShouldContain("migrationBuilder.CreateTable");
        content.ShouldContain("20230101120000_FirstMigration.cs");
        content.ShouldContain("20230102120000_SecondMigration.cs");
        content.ShouldContain("20230103120000_ThirdMigration.cs");
    }

    [Fact]
    public void Execute_ShouldPreserveDownMethodContent()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*FirstMigration.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        content.ShouldContain("migrationBuilder.DropTable");
    }

    [Fact]
    public void Execute_ShouldMergeUsingStatements()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        var firstMigrationFile = Directory.GetFiles(_testDirectory, "20230101*FirstMigration.cs").First();
        var content = File.ReadAllText(firstMigrationFile);

        content.ShouldContain("using Microsoft.EntityFrameworkCore.Migrations;");
        content.ShouldContain("using System.Collections.Generic;");
    }

    [Fact]
    public void Execute_ShouldUpdateDesignerFile()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        var designerFile = Directory
            .GetFiles(_testDirectory, "*.Designer.cs")
            .First(f => !f.Contains("ModelSnapshot"));

        designerFile.ShouldContain("FirstMigration");

        var designerContent = File.ReadAllText(designerFile);
        designerContent.ShouldContain("""[Migration("20230101120000_FirstMigration")]""");
        designerContent.ShouldContain("partial class FirstMigration");
    }

    [Fact]
    public void Execute_ShouldNotDeleteModelSnapshot()
    {
        // Arrange
        CreateTestMigrationFiles();
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);

        var snapshotFile = Directory.GetFiles(_testDirectory, "*ModelSnapshot.cs");
        snapshotFile.ShouldNotBeEmpty();
    }

    private void CreateTestMigrationFiles()
    {
        // First migration
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_FirstMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class FirstMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: ""Users"",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: ""Users"");
    }
}
");

        // First migration designer
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_FirstMigration.Designer.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(TestContext))]
[Migration(""20230101120000_FirstMigration"")]
partial class FirstMigration
{
}
");

        // Second migration
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;
using System.Collections.Generic;

public partial class SecondMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: ""Products"",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: ""Products"");
    }
}
");

        // Second migration designer
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(TestContext))]
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration
{
}
");

        // Third migration
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230103120000_ThirdMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class ThirdMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: ""Orders"",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: ""Orders"");
    }
}
");

        // Third migration designer
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230103120000_ThirdMigration.Designer.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(TestContext))]
[Migration(""20230103120000_ThirdMigration"")]
partial class ThirdMigration
{
}
");

        // Model snapshot (should never be deleted)
        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[DbContext(typeof(TestContext))]
partial class TestContextModelSnapshot : ModelSnapshot
{
}
");
    }

    private void CreateTestMigrationFilesWithDifferentYears()
    {
        // 2023 migrations
        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_FirstMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class FirstMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230101120000_FirstMigration.Designer.cs"),
            @"namespace TestMigrations;

[Migration(""20230101120000_FirstMigration"")]
partial class FirstMigration
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
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;

[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration
{
}
");

        // 2024 migration
        File.WriteAllText(
            Path.Combine(_testDirectory, "20240101120000_ThirdMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class ThirdMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
");

        File.WriteAllText(
            Path.Combine(_testDirectory, "20240101120000_ThirdMigration.Designer.cs"),
            @"namespace TestMigrations;

[Migration(""20240101120000_ThirdMigration"")]
partial class ThirdMigration
{
}
");

        // Model snapshot
        File.WriteAllText(
            Path.Combine(_testDirectory, "TestContextModelSnapshot.cs"),
            @"namespace TestMigrations;

partial class TestContextModelSnapshot
{
}
");
    }
}
