using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace CardVault.Services;

/// <summary>
/// Checks the GitHub releases endpoint for the latest published tag and compares
/// it against the running version. Uses only the public (unauthenticated) API.
/// This is the app's single, explicit network call — it never transmits data.
/// </summary>
public static class UpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/Marin-Kitagawa/CardVault/releases/latest";
    public const string ReleasesUrl = "https://github.com/Marin-Kitagawa/CardVault/releases";
    private const string UserAgent = "CardVault/0.1";

    public static string CurrentVersion { get; } = ReadCurrentVersion();

    public sealed record CheckResult(bool HasUpdate, Version? Latest, string? Page, string? Error);

    public static async Task<CheckResult> CheckAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            using var response = await client.GetAsync(ApiUrl).ConfigureAwait(false);

            // No release published yet — nothing to update to.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new CheckResult(false, null, ReleasesUrl, null);

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() : ReleasesUrl;

            if (string.IsNullOrWhiteSpace(tag))
                return new CheckResult(false, null, page, null);

            var latest = ParseVersion(tag);
            if (latest is null)
                return new CheckResult(false, null, page, null);

            var current = ParseVersion(CurrentVersion);
            var hasUpdate = current is null || latest > current;
            return new CheckResult(hasUpdate, latest, page, null);
        }
        catch (Exception ex)
        {
            return new CheckResult(false, null, null, ex.Message);
        }
    }

    private static Version? ParseVersion(string tag)
    {
        var t = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(t, out var v) ? v : null;
    }

    private static string ReadCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "0.1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}