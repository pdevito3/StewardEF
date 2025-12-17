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

    #region HasProblematicRenameOperations Tests - Rename Only (No Drop)

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenRenameColumnOnly()
    {
        // Arrange - Rename without subsequent drop is NOT problematic
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

        // Assert - Rename without drop is safe
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenRenameTableOnly()
    {
        // Arrange - Rename without subsequent drop is NOT problematic
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

        // Assert - Rename without drop is safe
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenRenameIndexOnly()
    {
        // Arrange - Rename without subsequent drop is NOT problematic
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

        // Assert - Rename without drop is safe
        result.ShouldBeFalse();
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

        // Designer file contains rename operations in comments - should be skipped
        File.WriteAllText(designerFile, @"
// This file contains RenameColumn and DropColumn in a comment but should be skipped
[Migration(""20231201000000_TestMigration"")]
partial class TestMigration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // migrationBuilder.RenameColumn(...);
        // migrationBuilder.DropColumn(...);
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile, designerFile });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenTextContainsRenameColumnButNoActualCall()
    {
        // Arrange - Text mentions RenameColumn but no actual method call
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    // This migration does not call RenameColumn but mentions it in a comment
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

        // Assert - No actual rename-then-drop pattern, so false
        result.ShouldBeFalse();
    }

    #endregion

    #region HasProblematicRenameOperations Tests - Rename Then Drop (Problematic)

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnTrue_WhenRenameColumnThenDropColumn()
    {
        // Arrange - Rename followed by drop of the renamed column IS problematic
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

            migrationBuilder.DropColumn(
                name: ""NewName"",
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnTrue_WhenRenameTableThenDropTable()
    {
        // Arrange - Rename followed by drop of the renamed table IS problematic
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

            migrationBuilder.DropTable(
                name: ""NewTable"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnTrue_WhenRenameIndexThenDropIndex()
    {
        // Arrange - Rename followed by drop of the renamed index IS problematic
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

            migrationBuilder.DropIndex(
                name: ""IX_NewName"",
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenDropColumnBeforeRename()
    {
        // Arrange - Drop comes BEFORE rename (order matters) - not problematic
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: ""SomeColumn"",
                table: ""Users"");

            migrationBuilder.RenameColumn(
                name: ""OldName"",
                table: ""Users"",
                newName: ""SomeColumn"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - Drop comes before rename, so the drop doesn't reference the renamed entity
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenDropDifferentColumn()
    {
        // Arrange - Rename and drop are for different columns
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

            migrationBuilder.DropColumn(
                name: ""UnrelatedColumn"",
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - Different column is dropped, not the renamed one
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenDropSameColumnDifferentTable()
    {
        // Arrange - Same column name but different table
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

            migrationBuilder.DropColumn(
                name: ""NewName"",
                table: ""Orders"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - Different table, so not the same column
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldBeCaseInsensitive()
    {
        // Arrange - Case difference in table/column names should still match
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: ""oldname"",
                table: ""users"",
                newName: ""newname"");

            migrationBuilder.DropColumn(
                name: ""NEWNAME"",
                table: ""USERS"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - Should match despite case difference
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldHandleMultipleRenamesWithOneDrop()
    {
        // Arrange - Multiple renames, only one is subsequently dropped
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: ""Col1"",
                table: ""Users"",
                newName: ""Column1"");

            migrationBuilder.RenameColumn(
                name: ""Col2"",
                table: ""Users"",
                newName: ""Column2"");

            migrationBuilder.DropColumn(
                name: ""Column2"",
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - Column2 is renamed then dropped
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldHandleComplexScenario()
    {
        // Arrange - Complex migration with mixed operations
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a table
            migrationBuilder.CreateTable(
                name: ""Products"",
                columns: table => new { Id = table.Column<int>() });

            // Rename a column in a different table
            migrationBuilder.RenameColumn(
                name: ""OldPrice"",
                table: ""Items"",
                newName: ""Price"");

            // Add a column
            migrationBuilder.AddColumn<string>(
                name: ""Description"",
                table: ""Products"");

            // Rename and then drop a column - THIS is problematic
            migrationBuilder.RenameColumn(
                name: ""TempCol"",
                table: ""Users"",
                newName: ""ToBeDeleted"");

            migrationBuilder.DropColumn(
                name: ""ToBeDeleted"",
                table: ""Users"");

            // Drop an unrelated column
            migrationBuilder.DropColumn(
                name: ""UnrelatedCol"",
                table: ""Orders"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - TempCol is renamed to ToBeDeleted, then ToBeDeleted is dropped
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldHandleMultilineFormats()
    {
        // Arrange - Different formatting styles
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: ""Old"", table: ""T"", newName: ""New"");
            migrationBuilder.DropColumn(name: ""New"", table: ""T"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldReturnFalse_WhenDroppingOriginalNameNotNewName()
    {
        // Arrange - Dropping the ORIGINAL column name, not the renamed one
        // This is valid: create OldName, rename to NewName, then drop a different OldName column
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

            // Dropping OldName (the original), not NewName (the renamed)
            migrationBuilder.DropColumn(
                name: ""OldName"",
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - This is NOT problematic; we only care if the NEW name is dropped
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldHandleIndexWithoutTableParameter()
    {
        // Arrange - DropIndex often omits table parameter in EF migrations
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: ""IX_Old"",
                newName: ""IX_New"");

            migrationBuilder.DropIndex(
                name: ""IX_New"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasProblematicRenameOperations_ShouldDetectChainedRenames()
    {
        // Arrange - Rename A→B, then B→C, then drop C
        // The B→C rename followed by drop C should be detected
        var migrationFile = Path.Combine(_testDirectory, "20231201000000_TestMigration.cs");
        File.WriteAllText(migrationFile, @"
namespace TestProject.Migrations
{
    public partial class TestMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: ""ColumnA"",
                table: ""Users"",
                newName: ""ColumnB"");

            migrationBuilder.RenameColumn(
                name: ""ColumnB"",
                table: ""Users"",
                newName: ""ColumnC"");

            migrationBuilder.DropColumn(
                name: ""ColumnC"",
                table: ""Users"");
        }
    }
}");

        // Act
        var result = SquashMigrationsCommand.HasProblematicRenameOperations(new[] { migrationFile });

        // Assert - B→C rename followed by drop C is problematic
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
