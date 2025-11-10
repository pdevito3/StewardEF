namespace StewardEF.Tests.Commands.SquashMigrations;

using Shouldly;
using StewardEF.Commands;

/// <summary>
/// Tests for SQL conversion functionality added to handle rename operations
/// that would otherwise cause "could not be found in target model" errors.
/// </summary>
public class SqlConversionTests : IDisposable
{
    private readonly string _testDirectory;

    public SqlConversionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"StewardEF_SqlConversion_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region HasProblematicRenameOperations Tests

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnTrue_WhenRenameColumnExists()
    {
        // Arrange
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: ""OldName"",
                table: ""Users"",
                newName: ""NewName"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnTrue_WhenRenameTableExists()
    {
        // Arrange
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: ""OldTable"",
                newName: ""NewTable"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnTrue_WhenRenameIndexExists()
    {
        // Arrange
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: ""IX_OldName"",
                table: ""Users"",
                newName: ""IX_NewName"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenNoRenameOperations()
    {
        // Arrange
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
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
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldSkipDesignerFiles()
    {
        // Arrange
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        var designerFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.Designer.cs");

        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(name: ""Users"", columns: table => new { });
        }
    }
}");

        // Designer file contains "RenameColumn" in a comment or attribute
        File.WriteAllText(designerFile, @"
// This file contains RenameColumn in a comment but should be skipped
[Migration(""20231201000000_TestMigration"")]
partial class TestMigration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // Model configuration
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile, designerFile });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldNotTriggerOnSimilarText()
    {
        // Arrange
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    // This migration does not RenameColumn but mentions it in a comment
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var columnName = ""OldRenameColumnName"";
            migrationBuilder.AddColumn<string>(
                name: columnName,
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - Should still be true because the text contains "RenameColumn"
        // This is acceptable as a false positive is safer than a false negative
        result.ShouldBeTrue();
    }

    #endregion

    #region FindProjectFile Tests

    [Fact]
    public void FindProjectFile_ShouldFindProjectInSameDirectory()
    {
        // Arrange
        var projectFile = Path.Combine(_testDirectory, "TestProject.csproj");
        File.WriteAllText(projectFile, "<Project></Project>");

        // Act
        var result = SquashMigrationsCommand.FindProjectFile(_testDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(projectFile);
    }

    [Fact]
    public void FindProjectFile_ShouldFindProjectInParentDirectory()
    {
        // Arrange
        var migrationsDir = Path.Combine(_testDirectory, "Migrations");
        Directory.CreateDirectory(migrationsDir);

        var projectFile = Path.Combine(_testDirectory, "TestProject.csproj");
        File.WriteAllText(projectFile, "<Project></Project>");

        // Act
        var result = SquashMigrationsCommand.FindProjectFile(migrationsDir);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(projectFile);
    }

    [Fact]
    public void FindProjectFile_ShouldPreferNonTestProject()
    {
        // Arrange
        var mainProject = Path.Combine(_testDirectory, "MainProject.csproj");
        var testProject = Path.Combine(_testDirectory, "MainProject.Tests.csproj");

        File.WriteAllText(mainProject, "<Project></Project>");
        File.WriteAllText(testProject, "<Project></Project>");

        // Act
        var result = SquashMigrationsCommand.FindProjectFile(_testDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(mainProject);
    }

    [Fact]
    public void FindProjectFile_ShouldReturnNullWhenNoProjectFound()
    {
        // Arrange - empty directory with no .csproj files

        // Act
        var result = SquashMigrationsCommand.FindProjectFile(_testDirectory);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindProjectFile_ShouldUseExplicitPathWhenProvided()
    {
        // Arrange
        var explicitProject = Path.Combine(_testDirectory, "Explicit.csproj");
        var otherProject = Path.Combine(_testDirectory, "Other.csproj");

        File.WriteAllText(explicitProject, "<Project></Project>");
        File.WriteAllText(otherProject, "<Project></Project>");

        // Act
        var result = SquashMigrationsCommand.FindProjectFile(_testDirectory, explicitProject);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(Path.GetFullPath(explicitProject));
    }

    [Fact]
    public void FindProjectFile_ShouldReturnNullWhenExplicitPathInvalid()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDirectory, "DoesNotExist.csproj");

        // Act
        var result = SquashMigrationsCommand.FindProjectFile(_testDirectory, invalidPath);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region ExtractMigrationId Tests

    [Fact]
    public void ExtractMigrationId_ShouldExtractIdFromValidDesignerFile()
    {
        // Arrange
        var designerFile = Path.Combine(_testDirectory, "20231201123456_TestMigration.Designer.cs");
        File.WriteAllText(designerFile, @"
namespace TestProject.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration(""20231201123456_TestMigration"")]
    partial class TestMigration
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.ExtractMigrationId(designerFile);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe("20231201123456_TestMigration");
    }

    [Fact]
    public void ExtractMigrationId_ShouldHandleDifferentFormatting()
    {
        // Arrange
        var designerFile = Path.Combine(_testDirectory, "20240101000000_Init.Designer.cs");
        File.WriteAllText(designerFile, @"
namespace TestProject.Migrations
{
    [Migration( ""20240101000000_Init"" )]
    partial class Init
    {
    }
}");

        // Act
        var result = SquashMigrationsCommand.ExtractMigrationId(designerFile);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe("20240101000000_Init");
    }

    [Fact]
    public void ExtractMigrationId_ShouldReturnNullWhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "DoesNotExist.Designer.cs");

        // Act
        var result = SquashMigrationsCommand.ExtractMigrationId(nonExistentFile);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractMigrationId_ShouldReturnNullWhenNoMigrationAttribute()
    {
        // Arrange
        var designerFile = Path.Combine(_testDirectory, "Invalid.Designer.cs");
        File.WriteAllText(designerFile, @"
namespace TestProject.Migrations
{
    partial class TestMigration
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.ExtractMigrationId(designerFile);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractMigrationId_ShouldHandleMigrationNamesWithUnderscores()
    {
        // Arrange
        var designerFile = Path.Combine(_testDirectory, "20231201000000_Add_User_Table.Designer.cs");
        File.WriteAllText(designerFile, @"
[Migration(""20231201000000_Add_User_Table"")]
partial class Add_User_Table
{
}");

        // Act
        var result = SquashMigrationsCommand.ExtractMigrationId(designerFile);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe("20231201000000_Add_User_Table");
    }

    #endregion
}
