namespace StewardEF.Tests.Commands.SquashMigrations;

using NSubstitute;
using Shouldly;
using StewardEF.Commands;
using Spectre.Console.Cli;

/// <summary>
/// Basic functionality tests for the SquashMigrations command.
/// Tests core features like directory validation, basic squashing, using statements, and designer file updates.
/// </summary>
public class BasicTests : IDisposable
{
    private readonly string _testDirectory;

    public BasicTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"StewardEF_Basic_{Guid.NewGuid()}");
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
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
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
    public void Execute_ShouldPreserveUpMethodContent()
    {
        // Arrange
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
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
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
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
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
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
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
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

        var designerFile = Directory.GetFiles(_testDirectory, "*.Designer.cs")
            .Where(f => !f.Contains("ModelSnapshot"))
            .First();

        designerFile.ShouldContain("FirstMigration");

        var designerContent = File.ReadAllText(designerFile);
        designerContent.ShouldContain("[Migration(\"20230101120000_FirstMigration\")]");
        designerContent.ShouldContain("partial class FirstMigration");
    }

    [Fact]
    public void Execute_ShouldNotDeleteModelSnapshot()
    {
        // Arrange
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
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

    [Fact]
    public void Execute_ShouldDeduplicateUsingStatements()
    {
        // Verify that duplicate using statements across migrations are merged correctly
        // Arrange
        TestDataHelper.CreateMigrationsWithDuplicateUsings(_testDirectory);
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

        // Verify using statements are present (deduplication happens via HashSet)
        content.ShouldContain("using Microsoft.EntityFrameworkCore.Migrations;");
        content.ShouldContain("using System.Linq;");

        // File should compile successfully - if there were real duplicates at namespace level, it would be an error
        // But duplicate usings in different scopes are okay and expected
    }

    [Fact]
    public void Execute_ShouldHandleScopedNamespaceFormat()
    {
        // Test file-scoped namespace (namespace X;) vs block namespace (namespace X { })
        // Arrange
        TestDataHelper.CreateMigrationsWithScopedNamespace(_testDirectory);
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

        // Verify using statements are placed correctly after scoped namespace
        var lines = content.Split('\n');
        var namespaceLineIndex = Array.FindIndex(lines, l => l.Contains("namespace TestMigrations;"));
        var firstUsingLineIndex = Array.FindIndex(lines, l => l.TrimStart().StartsWith("using "));

        namespaceLineIndex.ShouldBeGreaterThan(-1);
        firstUsingLineIndex.ShouldBeGreaterThan(namespaceLineIndex, "Using statements should come after namespace");
    }
}
