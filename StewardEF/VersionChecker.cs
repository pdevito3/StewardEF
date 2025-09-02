namespace StewardEF;

using System.Reflection;
using Spectre.Console;

internal static class VersionChecker
{
    private const string DefaultVersion = "0.0.0";

    public static async Task CheckForLatestVersion()
    {
        try
        {
            var installedVersion = GetInstalledStewardEfVersion();
            var latestReleaseVersion = await GetLatestReleaseVersion();
            var result = new Version(installedVersion).CompareTo(new Version(latestReleaseVersion));
            if (result < 0)
            {
                AnsiConsole.MarkupLine(@$"{Environment.NewLine}[bold seagreen2]This StewardEF version '{installedVersion}' is older than that of the runtime '{latestReleaseVersion}'. Update the tools for the latest features and bug fixes (`dotnet tool update -g stewardef`).[/]{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // fail silently
        }
    }

    private static async Task<string> GetLatestReleaseVersion()
    {
        var latestCraftsmanPath = "https://github.com/pdevito3/stewardef/releases/latest";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        var response = await client.GetAsync(latestCraftsmanPath);
        response.EnsureSuccessStatusCode();

        var redirectUrl = response?.RequestMessage?.RequestUri;
        var version = redirectUrl?.ToString().Split('/').Last().Replace("v", "") ?? DefaultVersion;
        return version;
    }

    private static string GetInstalledStewardEfVersion()
    {
        var installedVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? DefaultVersion;
        installedVersion = installedVersion[0..^2]; // equivalent to installedVersion.Substring(0, installedVersion.Length - 2);

        return installedVersion;
    }
}
