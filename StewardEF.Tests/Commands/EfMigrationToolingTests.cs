namespace StewardEF.Tests.Commands;

using Shouldly;
using StewardEF.Commands;

public class EfMigrationToolingTests
{
    [Fact]
    public void CreateEfScriptStartInfo_ShouldIncludeProjectAndNoBuildByDefault()
    {
        var options = new EfScriptOptions("/repo/App/App.csproj");

        var startInfo = EfMigrationTooling.CreateEfScriptStartInfo("0", "20240101010101_Init", options);

        startInfo.FileName.ShouldBe("dotnet");
        startInfo.ArgumentList.Cast<string>().ShouldBe(
        [
            "ef",
            "migrations",
            "script",
            "0",
            "20240101010101_Init",
            "--project",
            Path.GetFullPath("/repo/App/App.csproj"),
            "--no-build"
        ]);
    }

    [Fact]
    public void CreateEfScriptStartInfo_ShouldIncludeStartupProjectContextAndConfiguration_WhenProvided()
    {
        var options = new EfScriptOptions(
            "/repo/App/App.csproj",
            "/repo/Web/Web.csproj",
            "AppDbContext",
            "Release");

        var startInfo = EfMigrationTooling.CreateEfScriptStartInfo("20240101010101_Init", "0", options);

        startInfo.ArgumentList.Cast<string>().ShouldBe(
        [
            "ef",
            "migrations",
            "script",
            "20240101010101_Init",
            "0",
            "--project",
            Path.GetFullPath("/repo/App/App.csproj"),
            "--startup-project",
            Path.GetFullPath("/repo/Web/Web.csproj"),
            "--context",
            "AppDbContext",
            "--configuration",
            "Release",
            "--no-build"
        ]);
    }
}
