using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DiscordRPC;
using DiscordRPC.Message;
using Nein.Extensions;
using OsuPlayer.Interfaces.Service;
using Splat;
using ConsoleLogger = DiscordRPC.Logging.ConsoleLogger;
using LogLevel = DiscordRPC.Logging.LogLevel;

namespace OsuPlayer.Services;

public class DiscordService : OsuPlayerService, IDiscordService
{
    public override string ServiceName => "DISCORD_SERVICE";

    private const string ApplicationId = "1495955314522980497";
    private const string DefaultImageKey = "logo";
    private DiscordRpcClient _client;
    private readonly string _defaultOsuThumbnailUrl = "https://assets.ppy.sh/beatmaps/{0}/covers/list.jpg";
    private string _lastOsuThumbnailUrl = string.Empty;

    /// <summary>
    /// Default assets for the RPC including the logo
    /// </summary>
    private readonly Assets _defaultAssets;

    public DiscordService()
    {
        _defaultAssets = new Assets
        {
            LargeImageKey = "logo"
        };

        _client = CreateClient();
    }

    private DiscordRpcClient CreateClient()
    {
        var client = new DiscordRpcClient(ApplicationId);
        client.Logger = new ConsoleLogger { Level = LogLevel.Warning };
        client.OnReady += Client_OnReady;
        client.OnPresenceUpdate += Client_OnPresenceUpdate;
        return client;
    }

    /// <summary>
    /// Initializes the Discord Client and prepares all events
    /// </summary>
    public void Initialize()
    {
        // If the previous client was disposed, create a fresh one before initializing.
        if (_client.IsDisposed)
            _client = CreateClient();

        if (_client.IsInitialized)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            EnsureLinuxIpcSymlinks();

        _client.Initialize();

        _client.SetPresence(new RichPresence
        {
            Details = "Music Player for osu!",
            State = "doing nothing...",
            Assets = new Assets
            {
                LargeImageKey = DefaultImageKey
            },
            Type = ActivityType.Listening
        });
    }

    ~DiscordService()
    {
        DeInitialize();
    }

    /// <summary>
    /// On Linux, the DiscordRichPresence library only checks a fixed set of socket paths.
    /// Flatpak Discord puts its IPC socket under a subdirectory that the library doesn't scan.
    /// This method creates symlinks from the expected paths to wherever Discord actually
    /// placed its socket, covering the most common install methods (native, Flatpak, Snap).
    /// </summary>
    private static void EnsureLinuxIpcSymlinks()
    {
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
                         ?? $"/run/user/{Environment.GetEnvironmentVariable("UID") ?? "1000"}";

        // Subdirectories where different Discord installs place their IPC sockets.
        var candidates = new[]
        {
            Path.Combine(runtimeDir, "app", "com.discordapp.Discord"),      // Flatpak
            Path.Combine(runtimeDir, "app", "com.discordapp.DiscordCanary"), // Flatpak Canary
            Path.Combine(runtimeDir, "app", "com.discordapp.DiscordPTB"),   // Flatpak PTB
            Path.Combine(runtimeDir, "snap.discord"),                        // Snap (already checked by lib, but let's keep native symlink)
        };

        for (var pipe = 0; pipe < 10; pipe++)
        {
            var socketName = $"discord-ipc-{pipe}";
            var standardPath = Path.Combine(runtimeDir, socketName);

            // If the standard path already exists (real socket or symlink), skip it.
            if (File.Exists(standardPath) || Path.Exists(standardPath))
                continue;

            foreach (var dir in candidates)
            {
                var source = Path.Combine(dir, socketName);
                if (!File.Exists(source) && !Path.Exists(source))
                    continue;

                try
                {
                    File.CreateSymbolicLink(standardPath, source);
                    Debug.WriteLine($"[Discord] Created symlink {standardPath} -> {source}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Discord] Could not create symlink {standardPath}: {ex.Message}");
                }

                break; // Only need one symlink per pipe number.
            }
        }
    }

    /// <summary>
    /// Needs to be called to dispose the client properly.
    /// </summary>
    public void DeInitialize()
    {
        if (!_client.IsDisposed)
        {
            if (_client.IsInitialized)
                _client.ClearPresence();
            _client.Dispose();
        }
    }

    /// <summary>
    /// Update the current RPC
    /// </summary>
    /// <param name="details">Text of the first line</param>
    /// <param name="state">Text of the second line</param>
    /// <param name="beatmapSetId">Optional beatmapset ID</param>
    /// <param name="assets">Optional assets to use</param>
    /// <param name="durationLeft">Optional duration left that is displayed in the RPC</param>
    public async Task UpdatePresence(string details, string state, int beatmapSetId = 0, Assets? assets = null, TimeSpan? durationLeft = null)
    {
        if (!_client.IsInitialized)
            return;

        if (assets == null && beatmapSetId != 0)
        {
            assets = await TryToGetThumbnail(beatmapSetId);
        }

        var timestamps = durationLeft == null ? null : Timestamps.FromTimeSpan(durationLeft.Value);

        _client.SetPresence(new RichPresence
        {
            Details = details,
            State = state,
            Assets = assets ?? _defaultAssets,
            Buttons = GetButtons(),
            Timestamps = timestamps,
            Type = ActivityType.Listening
        });
    }

    private async Task<Assets?> TryToGetThumbnail(int beatmapSetId)
    {
        var url = string.Format(_defaultOsuThumbnailUrl, beatmapSetId);

        if (url != _lastOsuThumbnailUrl)
        {
            // Discord can't accept URLs bigger than 256 bytes and throws an exception, so we check for that here
            if (Encoding.UTF8.GetByteCount(url) > 256)
            {
                return null;
            }

            LogToConsole($"Request => {url}");

            HttpResponseMessage response;

            try
            {
                using var client = new HttpClient();

                var req = new HttpRequestMessage(HttpMethod.Get, url);

                response = await client.SendAsync(req);
            }
            catch (Exception)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            _lastOsuThumbnailUrl = url;
        }

        return new()
        {
            LargeImageKey = url
        };
    }

    private Button[]? GetButtons()
    {
        return new Button[]
        {
            new()
            {
                Label = "GitHub",
                Url = "https://github.com/Christopher-Hayes/osuplayer"
            }
        };
    }

    private void Client_OnReady(object sender, ReadyMessage args)
    {
        Debug.WriteLine("Discord client ready...");
    }

    private void Client_OnPresenceUpdate(object sender, PresenceMessage args)
    {
        Debug.WriteLine("Discord Presence updated...");
    }
}