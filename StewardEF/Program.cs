using Spectre.Console;
using Spectre.Console.Cli;
using StewardEF;
using StewardEF.Commands;
using System.Runtime.InteropServices;
using System.Text;

var app = new CommandApp();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Console.OutputEncoding = Encoding.Unicode;
}

app.Configure(config =>
{
    config.SetApplicationName("steward");

    config.AddCommand<SquashMigrationsCommand>("squash")
        .WithDescription("Squashes EF migrations into the first migration.")
        .WithExample(new[] { "squash", "path/to/migrations" })
        .WithExample(new[] { "squash", "path/to/migrations", "-y", "2023" })
        .WithExample(new[] { "squash", "path/to/migrations", "-t", "20230615000000_AddUserTable" })
        .WithExample(new[] { "squash", "path/to/migrations", "-t", "AddUserTable" });

    config.AddCommand<ConvertToSqlCommand>("convert-to-sql")
        .WithDescription("Converts a migration to use SQL scripts instead of C# code.")
        .WithExample(new[] { "convert-to-sql", "path/to/migrations" })
        .WithExample(new[] { "convert-to-sql", "path/to/migrations", "-p", "path/to/Project.csproj" })
        .WithExample(new[] { "convert-to-sql", "path/to/migrations", "-m", "AddUserTable" });
});

try
{
    app.Run(args);
}
catch (Exception e)
{
    AnsiConsole.WriteException(e, new ExceptionSettings
    {
        Format = ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks,
        Style = new ExceptionStyle
        {
            Exception = new Style().Foreground(Color.Grey),
            Message = new Style().Foreground(Color.White),
            NonEmphasized = new Style().Foreground(Color.Cornsilk1),
            Method = new Style().Foreground(Color.Red),
            Path = new Style().Foreground(Color.Red),
        }
    });
}
finally
{
    await VersionChecker.CheckForLatestVersion(); // Ensure the tool is up-to-date
}