using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Octokit;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Network.Data;

namespace OsuPlayer.Network;

/// <summary>
/// A static class to help us access various GitHub Repository data
/// Also it helps us provide updates for the osu!player and check if updates are available by checking our
/// GitHub-Repository releases
/// </summary>
public static class GitHub
{
    /// <summary>
    /// Checks if an older version is running and if so it will notify the user.
    /// </summary>
    /// <param name="releaseChannel">The release channel to be used</param>
    /// <returns>a UpdateResponse object</returns>
    public static async Task<UpdateResponse> CheckForUpdates(ReleaseChannels releaseChannel = ReleaseChannels.Stable)
    {
        try
        {
            var curVersion = Assembly.GetEntryAssembly()!.GetName().Version;

            var release = await GetLatestRelease(releaseChannel);

            if (release == default)
                return new UpdateResponse
                {
                    IsNewVersionAvailable = false
                };

            // Parse the GitHub tag as a Version so the comparison is numeric,
            // not string-based (avoids "2026.4.18" != "2026.04.18.4" false-positives).
            if (!Version.TryParse(release.TagName, out var releaseVersion))
                return new UpdateResponse
                {
                    IsNewVersionAvailable = false
                };

            if (curVersion < releaseVersion)
                return new UpdateResponse
                {
                    IsNewVersionAvailable = true,
                    HtmlUrl = release.HtmlUrl,
                    IsPrerelease = releaseChannel == ReleaseChannels.PreReleases,
                    Version = release.TagName,
                    ReleaseDate = release.CreatedAt,
                    PatchNotes = await GetLatestPatchNotes(releaseChannel),
                    Assets = release.Assets
                };

            return new UpdateResponse
            {
                IsNewVersionAvailable = false
            };
        }
        catch (RateLimitExceededException ex)
        {
            Debug.WriteLine($"Can't check for updates rate limit exceeded! + {ex.Message}");

            return new UpdateResponse
            {
                IsNewVersionAvailable = false
            };
        }
    }

    /// <summary>
    /// Gets the latest release from GitHub
    /// </summary>
    /// <param name="releaseChannel">The release channel to be used</param>
    /// <returns>a GitHub release</returns>
    public static async Task<Release?> GetLatestRelease(ReleaseChannels releaseChannel = ReleaseChannels.Stable)
    {
        try
        {
            var github = new GitHubClient(new ProductHeaderValue("osu!player"));

            var releases = await github.Repository.Release.GetAll("Christopher-Hayes", "osuplayer");

            var includePreReleases = releaseChannel == ReleaseChannels.PreReleases;

            Release latestRelease = null;

            foreach (var release in releases.OrderByDescending(x => x.CreatedAt))
            {
                if (release.Prerelease && !includePreReleases) continue;

                latestRelease = release;

                break;
            }

            return latestRelease;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);

            return default;
        }
    }

    public static async Task<string?> GetLatestPatchNotes(ReleaseChannels releaseChannel = ReleaseChannels.Stable)
    {
        try
        {
            var release = await GetLatestRelease(releaseChannel);

            if (release == default)
                return "**No patch-notes found**";

            var regex = new Regex(@"( in )([\w\s:\/\.-])*[\d]+");
            var newBody = regex.Replace(release.Body, "");
            regex = new Regex(@"(\n?\r?)*[\*]*(Full Changelog)[\*]*:.*$");
            newBody = regex.Replace(newBody, "");

            return $"## {(release.Prerelease ? "pre-release" : "release")} v" + release.TagName + Environment.NewLine
                   + "*released " + release.CreatedAt.ToString("F", new CultureInfo("en-us")) + "*"
                   + Environment.NewLine
                   + Environment.NewLine
                   + newBody;
        }
        catch (RateLimitExceededException ex)
        {
            Debug.WriteLine($"Can't check for updates rate limit exceeded! + {ex.Message}");
            return "**No patch-notes found, due to GitHub rate limit exceeded**";
        }
    }

    public static async Task<List<OsuPlayerContributor>?> GetContributers()
    {
        try
        {
            var github = new GitHubClient(new ProductHeaderValue("osu!player"));

            var githubData = await github.Repository.GetAllContributors("Christopher-Hayes", "osuplayer");

            var result = new List<OsuPlayerContributor>();

            foreach (var user in githubData)
            {
                using var client = new HttpClient();

                Bitmap? image = null;

                try
                {
                    var data = await client.GetByteArrayAsync(new Uri(user.AvatarUrl));

                    await using var stream = new MemoryStream(data);

                    image = await Task.Run(() => new Bitmap(stream));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                result.Add(new OsuPlayerContributor(user.Login, image));
            }

            return result;
        }
        catch (RateLimitExceededException ex)
        {
            Debug.WriteLine($"Can't check for updates rate limit exceeded! + {ex.Message}");
            return default;
        }
    }
}