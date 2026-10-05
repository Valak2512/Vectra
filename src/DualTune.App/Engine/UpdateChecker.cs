using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace DualTune.App.Engine;

/// <summary>Version de Vectra et adresses du projet.</summary>
public static class AppInfo
{
    public const string RepoUrl = "https://github.com/Valak2512/Vectra";
    public const string ReleasesUrl = RepoUrl + "/releases/latest";
    public const string LatestReleaseApi = "https://api.github.com/repos/Valak2512/Vectra/releases/latest";

    public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(1, 0, 0);

    public static string VersionText => $"{Version.Major}.{Version.Minor}.{Math.Max(0, Version.Build)}";
}

/// <summary>Cherche sur GitHub si une version plus récente de Vectra est publiée.</summary>
public static class UpdateChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    /// <summary>La version publiée si elle est plus récente que <paramref name="current"/>, sinon null (ou en cas d'erreur).</summary>
    public static async Task<Version?> CheckAsync(Version current)
    {
        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Vectra/{AppInfo.VersionText}"); // exigé par l'API GitHub
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.GetAsync(AppInfo.LatestReleaseApi);
            if (!response.IsSuccessStatusCode) return null; // pas encore de version publiée, limite atteinte…
            return NewerVersion(await response.Content.ReadAsStringAsync(), current);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return null; // hors ligne : on réessaiera au prochain lancement
        }
    }

    internal static Version? NewerVersion(string releaseJson, Version current)
    {
        try
        {
            using var doc = JsonDocument.Parse(releaseJson);
            if (!doc.RootElement.TryGetProperty("tag_name", out var tag)) return null;
            var latest = ParseTag(tag.GetString());
            return latest is not null && latest > Normalize(current) ? latest : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>« v1.2.3 », « 1.2 », « v1.2.3-beta » → 1.2.3 ; null si ce n'est pas un numéro de version.</summary>
    internal static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var text = tag.Trim().TrimStart('v', 'V');
        int dash = text.IndexOf('-');
        if (dash >= 0) text = text[..dash];
        return Version.TryParse(text, out var v) ? Normalize(v) : null;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
