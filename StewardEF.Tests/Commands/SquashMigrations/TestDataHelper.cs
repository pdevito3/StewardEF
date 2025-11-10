namespace StewardEF.Tests.Commands.SquashMigrations;

/// <summary>
/// Helper class for creating test migration files used across multiple test classes.
/// </summary>
public static class TestDataHelper
{
    public static void CreateTestMigrationFiles(string testDirectory)
    {
        // First migration
        File.WriteAllText(
            Path.Combine(testDirectory, "20230101120000_FirstMigration.cs"),
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
            Path.Combine(testDirectory, "20230101120000_FirstMigration.Designer.cs"),
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
            Path.Combine(testDirectory, "20230102120000_SecondMigration.cs"),
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
            Path.Combine(testDirectory, "20230102120000_SecondMigration.Designer.cs"),
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
            Path.Combine(testDirectory, "20230103120000_ThirdMigration.cs"),
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
            Path.Combine(testDirectory, "20230103120000_ThirdMigration.Designer.cs"),
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
            Path.Combine(testDirectory, "TestContextModelSnapshot.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[DbContext(typeof(TestContext))]
partial class TestContextModelSnapshot : ModelSnapshot
{
}
");
    }

    public static void CreateMigrationsWithDuplicateUsings(string testDirectory)
    {
        File.WriteAllText(
            Path.Combine(testDirectory, "20230101120000_FirstMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;
using System.Linq;

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
            Path.Combine(testDirectory, "20230101120000_FirstMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_FirstMigration"")]
partial class FirstMigration { }
");

        File.WriteAllText(
            Path.Combine(testDirectory, "20230102120000_SecondMigration.cs"),
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
            Path.Combine(testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }

    public static void CreateMigrationsWithScopedNamespace(string testDirectory)
    {
        File.WriteAllText(
            Path.Combine(testDirectory, "20230101120000_FirstMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;

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
            Path.Combine(testDirectory, "20230101120000_FirstMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230101120000_FirstMigration"")]
partial class FirstMigration { }
");

        File.WriteAllText(
            Path.Combine(testDirectory, "20230102120000_SecondMigration.cs"),
            @"namespace TestMigrations;

using Microsoft.EntityFrameworkCore.Migrations;
using System.Collections.Generic;

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
            Path.Combine(testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;
[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration { }
");

        File.WriteAllText(
            Path.Combine(testDirectory, "TestContextModelSnapshot.cs"),
            "namespace TestMigrations;\npartial class TestContextModelSnapshot { }");
    }

    public static void CreateMigrationsWithDifferentYears(string testDirectory)
    {
        // 2023 migrations
        File.WriteAllText(
            Path.Combine(testDirectory, "20230101120000_FirstMigration.cs"),
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
            Path.Combine(testDirectory, "20230101120000_FirstMigration.Designer.cs"),
            @"namespace TestMigrations;

[Migration(""20230101120000_FirstMigration"")]
partial class FirstMigration
{
}
");

        File.WriteAllText(
            Path.Combine(testDirectory, "20230102120000_SecondMigration.cs"),
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
            Path.Combine(testDirectory, "20230102120000_SecondMigration.Designer.cs"),
            @"namespace TestMigrations;

[Migration(""20230102120000_SecondMigration"")]
partial class SecondMigration
{
}
");

        // 2024 migration
        File.WriteAllText(
            Path.Combine(testDirectory, "20240101120000_ThirdMigration.cs"),
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
            Path.Combine(testDirectory, "20240101120000_ThirdMigration.Designer.cs"),
            @"namespace TestMigrations;

[Migration(""20240101120000_ThirdMigration"")]
partial class ThirdMigration
{
}
");

        // Model snapshot
        File.WriteAllText(
            Path.Combine(testDirectory, "TestContextModelSnapshot.cs"),
            @"namespace TestMigrations;

partial class TestContextModelSnapshot
{
}
");
    }
}
