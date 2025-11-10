# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

StewardEF is a .NET 8.0 global CLI tool that squashes Entity Framework Core migrations to reduce build times. It consolidates multiple migration files into a single initial migration while preserving Up/Down logic, using statements, and designer file metadata.

## Commands

### Build & Package
```bash
# Build the project
dotnet build

# Create NuGet package
dotnet pack -c Release

# Install tool locally for testing
dotnet tool install --global --add-source ./StewardEF/nupkg StewardEF

# Uninstall tool
dotnet tool uninstall -g StewardEF
```

### Testing the Tool
```bash
# Run the tool (after installing locally)
steward squash -d path/to/migrations

# With year filter
steward squash -d path/to/migrations -y 2023

# With target migration
steward squash -d path/to/migrations -t MigrationName
```

### Development
```bash
# Run from source without installing
dotnet run --project StewardEF/StewardEF.csproj -- squash -d path/to/test/migrations

# Clean build artifacts
dotnet clean
```

## Architecture

### Core Components

**Program.cs**
- Entry point using Spectre.Console.Cli framework
- Registers the `squash` command
- Handles global exception formatting
- Triggers async version checker after execution

**Commands/SquashMigrations.cs** (Main Logic)
- `Settings`: CLI argument model (MigrationsDirectory, Year, TargetMigration)
- `Execute()`: Validates directory and delegates to core algorithm
- `SquashMigrations()`: Five-phase squashing process:
  1. File discovery & filtering (by year/target)
  2. Content aggregation (Up/Down methods + using statements)
  3. First migration update with aggregated content
  4. Designer file management (rename + update attributes)
  5. Cleanup (delete redundant files)

**VersionChecker.cs**
- Post-execution check for newer releases via GitHub API
- Non-blocking async operation

### Key Implementation Details

**Migration Content Extraction**
- Brace-level tracking algorithm to extract method bodies
- Wraps each migration's content in scoped blocks to prevent naming conflicts
- Preserves original indentation

**Using Statement Management**
- Supports both scoped (`namespace X;`) and block (`namespace X { }`) formats
- Merges and deduplicates using statements from all migrations
- Maintains alphabetical ordering

**Designer File Handling**
- Renames latest `.Designer.cs` file to match first migration
- Updates `[Migration("...")]` attribute and partial class name using regex
- Preserves all other designer file content

**Filtering Logic**
- Year filter: Matches 4-digit year prefix in filename (EF convention: `20231201123456_MigrationName.cs`)
- Target filter: Uses `TakeWhile` to squash up to specified migration name

## Important Considerations

### File Structure
- All source code in `/StewardEF` directory
- Single command implementation in `/StewardEF/Commands/SquashMigrations.cs`
- NuGet packages output to `./StewardEF/nupkg`

### EF Migration Patterns
- Standard EF migration naming: `{timestamp}_{name}.cs` and `{timestamp}_{name}.Designer.cs`
- Always excludes `*ModelSnapshot.cs` from processing (EF metadata file)
- Down migrations are processed in reverse order for correct rollback sequence

### Code Patterns
- Static helper methods for extraction, replacement, and updating
- Spectre.Console markup for colored output (`[green]`, `[yellow]`, `[red]`)
- No class state; purely functional approach
- Reads entire migration files into memory (acceptable due to small file size)

### Recent Enhancements
- Scoped namespace support: Migrations wrapped in their own scopes (commit 2886b0d)
- Designer file class name handling (commit e3b612f)
- Target migration filtering (commit 34257dc)
