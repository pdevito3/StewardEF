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
```

#### **How It Works**

> ⚠️ The squash command will modify your migrations files, so **please** be sure to start with a clean commit before squashing so you can easily undo your changes if needed

The squash command combines all existing migrations into a single, consolidated migration file. Here's how it works:

- Combines Migration Methods: Aggregates the Up and Down methods across all migration files.
- Retains Important Using Statements: Collects using statements from all migrations, ensuring no dependencies are missed.
- Preserves the First Migration: Inserts the combined code into the first migration file in the specified directory.
- Removes Redundant Files: Deletes all migrations except the first one, leaving a single, squashed migration.

Depending on how your migrations were structured, you might need to make some manual adjustments after running squash. Some things to watch for:

- Private Fields: If any private readonly fields or other member data are in your migrations, ensure they're properly defined in the combined migration file.
