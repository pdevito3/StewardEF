namespace StewardEF.Tests.Commands.SquashMigrations;

using NSubstitute;
using Shouldly;
using StewardEF.Commands;
using Spectre.Console.Cli;

/// <summary>
/// Tests for filtering functionality in SquashMigrations command (year and target migration filters).
/// </summary>
public class FilteringTests : IDisposable
{
    private readonly string _testDirectory;

    public FilteringTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"StewardEF_Filtering_{Guid.NewGuid()}");
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
    public void Execute_ShouldSquashMigrations_WithYearFilter()
    {
        // Arrange
        TestDataHelper.CreateMigrationsWithDifferentYears(_testDirectory);
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory,
            Year = 2023
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

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
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory,
            TargetMigration = "SecondMigration"
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);

        // Should squash only up to SecondMigration
        var remainingFiles = Directory.GetFiles(_testDirectory, "*.cs");
        remainingFiles.ShouldContain(f => f.Contains("ThirdMigration"));
    }

    [Fact]
    public void Execute_ShouldHandleYearFilterWithNoMatches()
    {
        // Edge case: year filter matches no migrations
        // Arrange
        TestDataHelper.CreateTestMigrationFiles(_testDirectory);
        var command = new SquashMigrationsCommand();
        var settings = new SquashMigrationsCommand.Settings
        {
            MigrationsDirectory = _testDirectory,
            Year = 2099 // Year that doesn't exist
        };
        var remainingArgs = Substitute.For<IRemainingArguments>();
        var context = new CommandContext([], remainingArgs, "test", null);

        // Act
        var result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0); // Should succeed but do nothing

        // Original files should remain unchanged
        var files = Directory.GetFiles(_testDirectory, "*.cs");
        files.Length.ShouldBeGreaterThan(0);
    }
}
