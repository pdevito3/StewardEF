# StewardEF 🛠️

StewardEF is a tool designed to make managing Entity Framework (EF) migrations easier for projects with a lengthy history.
Specifically, it is able to squash your migrations back to a single file and drastically speed up your build times.

EF Core does not yet provide a built-in solution for squashing migrations yet ([relevant issue here](https://github.com/dotnet/efcore/issues/2174)), but in the meantime, StewardEF can help fill the gap.

---

## Installation

Install the `StewardEF` dotnet tool:

```bash
dotnet tool install -g StewardEF
```

Or update it:
```bash
dotnet tool update -g StewardEF
```

## Commands

### `squash`

Squashes EF migrations into the first migration in the specified directory.

#### **Usage:**

```bash
steward squash path/to/migrations [-y year] [-t migration-name]
```

##### Options

- `[MigrationsDirectory]`: Path to the directory containing your EF migrations. If omitted, you'll be prompted to enter it interactively.
- `-y|--year`: Optional. Specify the year up to which migrations should be squashed. If omitted, all migrations will be squashed.
- `-t|--target`: Optional. Specify the target migration name (without .cs extension) up to which migrations should be squashed. Since EF Core migrations follow the pattern `YYYYMMDDHHMMSS_MigrationName.cs`, you can specify either the full name (e.g. "20230615000000_AddUserTable") or just part of it (e.g. "AddUserTable"). The matching is case-insensitive. If omitted, all migrations will be squashed.
- `--skip-sql`: Optional. Skip automatic SQL conversion even if problematic rename operations are detected. A warning will be shown if rename-then-drop patterns are found. Use this if you want to keep the C# code format and handle any issues manually.

##### Examples

```bash
# Squash all migrations
steward squash path/to/migrations

# Squash migrations from 2023
steward squash path/to/migrations -y 2023

# Squash migrations up to a specific migration (using full name)
steward squash path/to/migrations -t 20230615000000_AddUserTable

# Squash migrations up to a specific migration (using partial name)
steward squash path/to/migrations -t AddUserTable

# Squash migrations but skip SQL conversion (keep C# format)
steward squash path/to/migrations --skip-sql
```

#### **How It Works**

> ⚠️ The squash command will modify your migrations files, so **please** be sure to start with a clean commit before squashing so you can easily undo your changes if needed

The squash command combines all existing migrations into a single, consolidated migration file. Here's how it works:

1. **C# Aggregation**: Combines the Up and Down methods across all migration files using standard C# code concatenation
2. **Using Statements**: Collects using statements from all migrations, ensuring no dependencies are missed
3. **Designer File Update**: Renames the latest designer file to match the first migration and updates its metadata
4. **Cleanup**: Deletes all intermediate migrations, leaving only the squashed first migration
5. **Automatic Rename Detection** (post-squash): Scans the squashed result for problematic rename-then-drop patterns (e.g., `RenameColumn` followed by `DropColumn` of the renamed column)
6. **SQL Conversion** (if needed): If a renamed entity is subsequently dropped, automatically converts the squashed migration to SQL format
   > *Why?* When EF executes rename operations, it needs the designer file to validate that the column/table exists. If a column is renamed then later dropped, the final designer snapshot won't contain it, causing "could not be found in target model" errors. Converting to SQL captures the correct schema transformations without relying on designer metadata.
   >
   > *Note:* Simple renames without a subsequent drop are safe and won't trigger SQL conversion.

##### Handling Rename Operations

The tool detects **rename-then-drop patterns** - cases where a column, table, or index is renamed and then subsequently dropped. These patterns are problematic because the drop operation references an entity by its new name, but the final model snapshot doesn't know about the rename history.

When problematic patterns are detected, you'll see output like this:

```
Squashing 15 migration files...
Migrations squashed successfully! ✨
⚠ Detected rename operations in squashed migration
Converting to SQL script format...
Found project: MyApp.csproj
✓ Successfully converted to SQL format
```

The tool automatically:
1. Squashes migrations normally using C# code concatenation
2. Checks for rename-then-drop patterns (e.g., `RenameColumn` → `DropColumn` for the same column)
3. If found, locates your `.csproj` file (searches up the directory tree)
4. Generates SQL scripts using `dotnet ef migrations script 0 <migration-id>`
5. Replaces the squashed migration content with `migrationBuilder.Sql()` calls containing the generated SQL

**Skipping SQL Conversion**

If you prefer to keep the C# format and handle any issues manually, use the `--skip-sql` flag:

```bash
steward squash path/to/migrations --skip-sql
```

When skipped, you'll see a warning if problematic patterns are detected:
```
⚠ Warning: Detected rename operations (RenameColumn, RenameTable, or RenameIndex) in squashed migration
  SQL conversion was skipped due to --skip-sql flag
  The squashed migration may have issues when applied to a fresh database
```

This approach solves the [common issue](https://github.com/pdevito3/StewardEF/issues/1) where squashed migrations fail with errors like "The column 'X' could not be found in target model" when columns are renamed then later dropped. By converting to SQL after squashing, the tool preserves the correct schema transformations without depending on intermediate designer files.

##### Things to Watch For

Depending on how your migrations were structured, you might need to make some manual adjustments after running squash:

- **Private Fields**: If any private readonly fields or other member data are in your migrations, ensure they're properly defined in the combined migration file
- **Custom C# Logic**: Complex data transformations or C# code beyond schema changes may not convert perfectly to SQL
- **Testing**: Always test squashed migrations on a clean database to verify they work correctly

---

### `convert-to-sql`

Converts an existing migration to use SQL scripts instead of C# code. This is useful when you need to manually convert a migration that has rename operations or other issues that would benefit from SQL representation.

#### **Usage:**

```bash
steward convert-to-sql path/to/migrations [-p project-path] [-m migration-name]
```

##### Options

- `[MigrationsDirectory]`: Path to the directory containing your EF migrations. If omitted, you'll be prompted to enter it interactively.
- `-p|--project`: Optional. Explicit path to your `.csproj` file. If omitted, the tool will search up the directory tree.
- `-m|--migration`: Optional. Name of the specific migration to convert (without .cs extension). If omitted, converts the most recent migration.

##### Examples

```bash
# Convert the most recent migration
steward convert-to-sql path/to/migrations

# Convert with explicit project path
steward convert-to-sql path/to/migrations -p path/to/MyProject.csproj

# Convert a specific migration
steward convert-to-sql path/to/migrations -m AddUserTable
steward convert-to-sql path/to/migrations -m 20231201000000_AddUserTable
```

#### **How It Works**

The convert-to-sql command:

1. Locates the specified migration (or most recent if not specified)
2. Finds the associated `.Designer.cs` file to extract the migration ID
3. Uses `dotnet ef migrations script` to generate SQL for the Up and Down methods
4. Replaces the migration's C# code with `migrationBuilder.Sql()` calls containing the generated SQL

This is particularly useful for:
- **Fixing problematic migrations** after they've been created
- **Converting migrations** that will be deployed to production via SQL scripts
- **Working around EF limitations** with rename operations in complex scenarios

> **Note**: This command requires the `dotnet ef` tools to be installed and your project to be buildable.
