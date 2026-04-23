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
    /// Cancels any in-flight UpdatePresence call so that a stale async thumbnail fetch
    /// cannot overwrite a newer presence update (e.g. a Play() arriving after a Pause()).
    /// </summary>
    private CancellationTokenSource _presenceCts = new();

    /// <summary>
    /// Cancels the pending inactivity clear when the player resumes before the timeout fires.
    /// </summary>
    private CancellationTokenSource _inactivityCts = new();

    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(5);

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
    public async Task UpdatePresence(string details, string state, int beatmapSetId = 0, Assets? assets = null, TimeSpan? elapsed = null, TimeSpan? durationLeft = null)
    {
        if (!_client.IsInitialized)
            return;

        // Cancel any previous in-flight update and grab a fresh token.
        // This prevents a slow thumbnail fetch (e.g. from Pause()) from
        // overwriting a faster, newer update (e.g. from Play() after a seek).
        var oldCts = _presenceCts;
        _presenceCts = new CancellationTokenSource();
        var token = _presenceCts.Token;
        oldCts.Cancel();
        oldCts.Dispose();

        if (assets == null && beatmapSetId != 0)
        {
            assets = await TryToGetThumbnail(beatmapSetId, token);
        }

        // Bail out if a newer UpdatePresence call has already superseded this one.
        if (token.IsCancellationRequested)
            return;

        // Build timestamps from the caller-supplied elapsed/remaining values so that:
        //   Start = now - elapsed  → Discord shows the correct elapsed time (not reset on seek)
        //   End   = now + remaining → Discord shows a countdown to the end of the track
        // When neither value is provided (e.g. paused) Timestamps is left null and Discord
        // removes the timer entirely.
        Timestamps? timestamps = null;
        if (elapsed.HasValue || durationLeft.HasValue)
        {
            timestamps = new Timestamps();
            if (elapsed.HasValue)
                timestamps.Start = DateTime.UtcNow - elapsed.Value;
            if (durationLeft.HasValue)
                timestamps.End = DateTime.UtcNow + durationLeft.Value;
        }

        _client.SetPresence(new RichPresence
        {
            Details = details,
            State = state,
            Assets = assets ?? _defaultAssets,
            Buttons = GetButtons(),
            Timestamps = timestamps,
            Type = ActivityType.Listening
        });

        // Cancel any running inactivity countdown, then restart it only when paused
        // (no timestamps means the player is paused — elapsed/durationLeft are both null).
        var oldInactivityCts = _inactivityCts;
        _inactivityCts = new CancellationTokenSource();
        oldInactivityCts.Cancel();
        oldInactivityCts.Dispose();

        if (timestamps == null)
        {
            var inactivityToken = _inactivityCts.Token;
            _ = Task.Run(async () =>
            {
                await Task.Delay(InactivityTimeout, inactivityToken);
                if (!inactivityToken.IsCancellationRequested && _client.IsInitialized)
                    _client.ClearPresence();
            }, inactivityToken);
        }
    }

    private async Task<Assets?> TryToGetThumbnail(int beatmapSetId, CancellationToken cancellationToken = default)
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

                response = await client.SendAsync(req, cancellationToken);
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