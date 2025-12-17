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

    #region SanitizeEfGeneratedSql Tests

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveStartTransaction()
    {
        // Arrange
        var sql = @"START TRANSACTION;
CREATE TABLE users (id INT);
COMMIT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldNotContain("START TRANSACTION");
        result.ShouldNotContain("COMMIT");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveBeginTransaction()
    {
        // Arrange - SQL Server style
        var sql = @"BEGIN TRANSACTION;
CREATE TABLE users (id INT);
COMMIT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldNotContain("BEGIN TRANSACTION");
        result.ShouldNotContain("COMMIT");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveStandaloneBegin()
    {
        // Arrange - Some providers use just BEGIN
        var sql = @"BEGIN;
CREATE TABLE users (id INT);
COMMIT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldNotContain("BEGIN;");
        result.ShouldNotContain("COMMIT");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveEfSchemaPreamble()
    {
        // Arrange - DO $EF$ blocks that check pg_namespace for schema creation are EF preamble
        // and should be removed (they cause duplicates when squashing)
        var sql = @"DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'myschema') THEN
        CREATE SCHEMA myschema;
    END IF;
END $EF$;
CREATE TABLE myschema.users (id INT);";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert - The EF preamble should be removed, but the actual table creation preserved
        result.ShouldNotContain("DO $EF$");
        result.ShouldNotContain("pg_namespace");
        result.ShouldContain("CREATE TABLE myschema.users");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveInsertIntoEfMigrationsHistory()
    {
        // Arrange
        var sql = @"CREATE TABLE users (id INT);

INSERT INTO __EFMigrationsHistory (migration_id, product_version)
VALUES ('20231201000000_InitialCreate', '8.0.0');";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldNotContain("INSERT INTO __EFMigrationsHistory");
        result.ShouldNotContain("20231201000000_InitialCreate");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveInsertWithSchemaQualifiedTable()
    {
        // Arrange - PostgreSQL with schema
        var sql = @"CREATE TABLE my_schema.users (id INT);

INSERT INTO my_schema.""__EFMigrationsHistory"" (migration_id, product_version)
VALUES ('20231201000000_InitialCreate', '10.0.0');";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE my_schema.users");
        result.ShouldNotContain("INSERT INTO");
        result.ShouldNotContain("__EFMigrationsHistory");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveDeleteFromEfMigrationsHistory()
    {
        // Arrange
        var sql = @"DROP TABLE users;

DELETE FROM __EFMigrationsHistory
WHERE migration_id = '20231201000000_InitialCreate';";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("DROP TABLE users");
        result.ShouldNotContain("DELETE FROM __EFMigrationsHistory");
        result.ShouldNotContain("20231201000000_InitialCreate");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveDeleteWithSchemaQualifiedTable()
    {
        // Arrange - PostgreSQL with schema
        var sql = @"DROP TABLE my_schema.users;

DELETE FROM my_schema.""__EFMigrationsHistory""
WHERE migration_id = '20231201000000_InitialCreate';";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("DROP TABLE my_schema.users");
        result.ShouldNotContain("DELETE FROM");
        result.ShouldNotContain("__EFMigrationsHistory");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldHandleCompletePostgresScript()
    {
        // Arrange - Realistic PostgreSQL migration script
        var sql = @"START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'my_schema') THEN
        CREATE SCHEMA my_schema;
    END IF;
END $EF$;

CREATE TABLE my_schema.users (
    id text NOT NULL,
    email text NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE INDEX ix_users_email ON my_schema.users (email);

INSERT INTO my_schema.""__EFMigrationsHistory"" (migration_id, product_version)
VALUES ('20250824024911_InitialCreate', '10.0.0');

COMMIT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldNotContain("START TRANSACTION");
        result.ShouldNotContain("COMMIT");
        result.ShouldNotContain("INSERT INTO");
        result.ShouldNotContain("__EFMigrationsHistory");

        // These should all be preserved
        result.ShouldContain("DO $EF$");
        result.ShouldContain("CREATE SCHEMA my_schema");
        result.ShouldContain("CREATE TABLE my_schema.users");
        result.ShouldContain("CREATE INDEX ix_users_email");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldHandleCompleteSqlServerScript()
    {
        // Arrange - Realistic SQL Server migration script
        var sql = @"BEGIN TRANSACTION;

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20231201000000_Init')
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20231201000000_Init', N'8.0.0');

COMMIT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldNotContain("BEGIN TRANSACTION");
        result.ShouldNotContain("COMMIT;");
        result.ShouldNotContain("INSERT INTO [__EFMigrationsHistory]");

        // The IF NOT EXISTS check and CREATE TABLE should be preserved
        result.ShouldContain("IF NOT EXISTS");
        result.ShouldContain("CREATE TABLE [Users]");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldBeCaseInsensitive()
    {
        // Arrange
        var sql = @"start transaction;
CREATE TABLE users (id INT);
commit;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldNotContain("start transaction");
        result.ShouldNotContain("commit");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldHandleMultipleTransactionBlocks()
    {
        // Arrange - Multiple transaction blocks (can happen with multiple migrations)
        var sql = @"START TRANSACTION;
CREATE TABLE users (id INT);
COMMIT;

START TRANSACTION;
ALTER TABLE users ADD COLUMN email TEXT;
COMMIT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldContain("ALTER TABLE users ADD COLUMN email TEXT");
        result.ShouldNotContain("START TRANSACTION");
        result.ShouldNotContain("COMMIT");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldCleanUpExcessiveBlankLines()
    {
        // Arrange
        var sql = @"CREATE TABLE users (id INT);



ALTER TABLE users ADD COLUMN email TEXT;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert - Should not have more than 2 consecutive newlines
        result.ShouldNotContain("\n\n\n");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldTrimResult()
    {
        // Arrange
        var sql = @"

CREATE TABLE users (id INT);

";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldBe("CREATE TABLE users (id INT);");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldPreserveLegitimateDeleteStatements()
    {
        // Arrange - DELETE that's not from __EFMigrationsHistory should be preserved
        var sql = @"DELETE FROM users WHERE id = 1;
DELETE FROM orders WHERE user_id = 1;";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("DELETE FROM users");
        result.ShouldContain("DELETE FROM orders");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldPreserveLegitimateInsertStatements()
    {
        // Arrange - INSERT that's not into __EFMigrationsHistory should be preserved
        var sql = @"INSERT INTO users (id, email) VALUES (1, 'test@example.com');
INSERT INTO roles (id, name) VALUES (1, 'admin');";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("INSERT INTO users");
        result.ShouldContain("INSERT INTO roles");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldHandleEmptyInput()
    {
        // Arrange
        var sql = "";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldBe("");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldHandleWhitespaceOnlyInput()
    {
        // Arrange
        var sql = "   \n\n   ";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldBe("");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldHandleTransactionWithWhitespace()
    {
        // Arrange - Extra whitespace around transaction statements
        var sql = @"  START TRANSACTION;
CREATE TABLE users (id INT);
   COMMIT;   ";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldContain("CREATE TABLE users");
        result.ShouldNotContain("START TRANSACTION");
        result.ShouldNotContain("COMMIT");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveCreateTableEfMigrationsHistory()
    {
        // Arrange - PostgreSQL style with schema
        var sql = @"CREATE TABLE IF NOT EXISTS my_schema.""__EFMigrationsHistory"" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

CREATE TABLE my_schema.users (id INT);";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldNotContain("__EFMigrationsHistory");
        result.ShouldNotContain("pk___ef_migrations_history");
        result.ShouldContain("CREATE TABLE my_schema.users");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveCreateTableEfMigrationsHistorySqlServer()
    {
        // Arrange - SQL Server style with brackets
        var sql = @"CREATE TABLE [__EFMigrationsHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);

CREATE TABLE [Users] (Id INT);";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldNotContain("__EFMigrationsHistory");
        result.ShouldContain("CREATE TABLE [Users]");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemovePostgresSchemaCreationPreamble()
    {
        // Arrange - PostgreSQL schema creation preamble
        var sql = @"DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'my_schema') THEN
        CREATE SCHEMA my_schema;
    END IF;
END $EF$;

CREATE TABLE my_schema.users (id INT);";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert
        result.ShouldNotContain("DO $EF$");
        result.ShouldNotContain("pg_namespace");
        result.ShouldNotContain("CREATE SCHEMA");
        result.ShouldContain("CREATE TABLE my_schema.users");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldRemoveDuplicatePreambles()
    {
        // Arrange - Duplicate preambles (the actual bug scenario)
        var sql = @"DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'my_schema') THEN
        CREATE SCHEMA my_schema;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS my_schema.""__EFMigrationsHistory"" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'my_schema') THEN
        CREATE SCHEMA my_schema;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS my_schema.""__EFMigrationsHistory"" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

CREATE TABLE my_schema.users (
    id text NOT NULL,
    email text NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert - All preambles should be removed
        result.ShouldNotContain("DO $EF$");
        result.ShouldNotContain("pg_namespace");
        result.ShouldNotContain("CREATE SCHEMA");
        result.ShouldNotContain("__EFMigrationsHistory");

        // The actual table should be preserved
        result.ShouldContain("CREATE TABLE my_schema.users");
        result.ShouldContain("pk_users");
    }

    [Fact]
    public void SanitizeEfGeneratedSql_ShouldPreserveUserCreatedSchemas()
    {
        // Arrange - User's own CREATE SCHEMA that's not part of the EF preamble
        var sql = @"CREATE SCHEMA IF NOT EXISTS custom_schema;
CREATE TABLE custom_schema.my_table (id INT);";

        // Act
        var result = SquashMigrationsCommand.SanitizeEfGeneratedSql(sql);

        // Assert - User's schema creation should be preserved
        result.ShouldContain("CREATE SCHEMA IF NOT EXISTS custom_schema");
        result.ShouldContain("CREATE TABLE custom_schema.my_table");
    }

    #endregion
}
