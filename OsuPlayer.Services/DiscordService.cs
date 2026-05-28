using System.Net.Sockets;
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

    private readonly Assets _defaultAssets;

    public DiscordConnectionStatus ConnectionStatus { get; private set; } = DiscordConnectionStatus.Disconnected;
    public event Action<DiscordConnectionStatus>? ConnectionStatusChanged;

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
        client.OnError += Client_OnError;
        client.OnClose += Client_OnClose;
        client.OnConnectionFailed += Client_OnConnectionFailed;
        return client;
    }

    private void SetStatus(DiscordConnectionStatus status)
    {
        ConnectionStatus = status;
        ConnectionStatusChanged?.Invoke(status);
    }

    /// <summary>
    /// Initializes the Discord Client and prepares all events
    /// </summary>
    public void Initialize()
    {
        if (_client.IsDisposed)
            _client = CreateClient();

        if (_client.IsInitialized)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            EnsureLinuxIpcSymlinks();

        SetStatus(DiscordConnectionStatus.Connecting);
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
    /// This method creates symlinks from the expected paths to wherever Discord actually placed its
    /// socket, and removes stale sockets/symlinks that exist on disk but are no longer accepting
    /// connections (e.g. after Discord restarts without a full system reboot to clear /run).
    /// </summary>
    private void EnsureLinuxIpcSymlinks()
    {
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
                         ?? $"/run/user/{Environment.GetEnvironmentVariable("UID") ?? "1000"}";

        var candidates = new[]
        {
            Path.Combine(runtimeDir, "app", "com.discordapp.Discord"),       // Flatpak
            Path.Combine(runtimeDir, "app", "com.discordapp.DiscordCanary"), // Flatpak Canary
            Path.Combine(runtimeDir, "app", "com.discordapp.DiscordPTB"),    // Flatpak PTB
            Path.Combine(runtimeDir, "snap.discord"),                         // Snap
        };

        for (var pipe = 0; pipe < 10; pipe++)
        {
            var socketName = $"discord-ipc-{pipe}";
            var standardPath = Path.Combine(runtimeDir, socketName);

            // If the standard path already exists, verify it actually accepts connections.
            // A stale socket/symlink (e.g. from a Discord crash or restart without a reboot)
            // will appear to exist but refuse connections, causing the RPC library to fail silently.
            if (File.Exists(standardPath) || Path.Exists(standardPath))
            {
                if (IsSocketAlive(standardPath))
                    continue;

                LogToConsole($"pipe #{pipe}: {standardPath} is stale, removing it.", LogType.Warning);
                try { File.Delete(standardPath); }
                catch (Exception ex) { LogToConsole($"pipe #{pipe}: Could not remove stale path: {ex.Message}", LogType.Error); }
            }

            foreach (var dir in candidates)
            {
                var source = Path.Combine(dir, socketName);
                if (!File.Exists(source) && !Path.Exists(source))
                    continue;

                if (!IsSocketAlive(source))
                {
                    // Delete the stale file from the candidate directory so Discord can
                    // recreate it the next time it starts. Without this, Discord finds the
                    // file already exists at its bind path and silently skips IPC setup.
                    LogToConsole($"pipe #{pipe}: candidate {source} is stale, removing it so Discord can recreate it on next launch.", LogType.Warning);
                    try { File.Delete(source); }
                    catch (Exception ex) { LogToConsole($"pipe #{pipe}: Could not remove stale candidate: {ex.Message}", LogType.Error); }
                    continue;
                }

                try
                {
                    File.CreateSymbolicLink(standardPath, source);
                    LogToConsole($"pipe #{pipe}: Created symlink {standardPath} -> {source}", LogType.Success);
                }
                catch (Exception ex)
                {
                    LogToConsole($"pipe #{pipe}: Could not create symlink: {ex.Message}", LogType.Error);
                }

                break;
            }
        }
    }

    /// <summary>
    /// Returns true if a Unix socket at <paramref name="path"/> is currently accepting connections.
    /// </summary>
    private static bool IsSocketAlive(string path)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(path));
            return true;
        }
        catch
        {
            return false;
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

        SetStatus(DiscordConnectionStatus.Disconnected);
    }

    /// <summary>
    /// Update the current RPC
    /// </summary>
    public async Task UpdatePresence(string details, string state, int beatmapSetId = 0, Assets? assets = null, TimeSpan? elapsed = null, TimeSpan? durationLeft = null)
    {
        if (!_client.IsInitialized)
            return;

        // Discord requires Details and State to be between 2 and 128 characters.
        // Strings outside this range cause the update to be silently ignored.
        details = SanitizePresenceField(details, "Unknown title");
        state   = SanitizePresenceField(state,   "Unknown artist");

        var oldCts = _presenceCts;
        _presenceCts = new CancellationTokenSource();
        var token = _presenceCts.Token;
        oldCts.Cancel();
        oldCts.Dispose();

        if (assets == null && beatmapSetId != 0)
            assets = await TryToGetThumbnail(beatmapSetId, token);

        if (token.IsCancellationRequested)
            return;

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

    /// <summary>
    /// Ensures a Discord presence field meets the 2–128 character requirement.
    /// Returns <paramref name="fallback"/> when the value is null/whitespace,
    /// pads to 2 chars with a trailing space when too short, and truncates at 128.
    /// </summary>
    private static string SanitizePresenceField(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        if (value.Length == 1)
            return value + " ";
        if (value.Length > 128)
            return value[..128];
        return value;
    }

    private async Task<Assets?> TryToGetThumbnail(int beatmapSetId, CancellationToken cancellationToken = default)
    {
        var url = string.Format(_defaultOsuThumbnailUrl, beatmapSetId);

        if (url != _lastOsuThumbnailUrl)
        {
            if (Encoding.UTF8.GetByteCount(url) > 256)
                return null;

            LogToConsole($"Request => {url}");

            HttpResponseMessage response;

            try
            {
                using var client = new HttpClient();
                response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
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

    private static Button[]? GetButtons()
    {
        return
        [
            new()
            {
                Label = "GitHub",
                Url = "https://github.com/Christopher-Hayes/osuplayer"
            }
        ];
    }

    private void Client_OnReady(object sender, ReadyMessage args)
    {
        SetStatus(DiscordConnectionStatus.Connected);
    }

    private void Client_OnPresenceUpdate(object sender, PresenceMessage args) { }

    private void Client_OnError(object sender, ErrorMessage args)
    {
        LogToConsole($"Discord RPC error: [{args.Code}] {args.Message}", LogType.Error);
        SetStatus(DiscordConnectionStatus.Error);
    }

    private void Client_OnClose(object sender, CloseMessage args)
    {
        LogToConsole($"Discord RPC connection closed: [{args.Code}] {args.Reason}", LogType.Warning);
        SetStatus(DiscordConnectionStatus.Disconnected);
    }

    private void Client_OnConnectionFailed(object sender, ConnectionFailedMessage args)
    {
        LogToConsole($"Discord RPC connection failed on pipe #{args.FailedPipe}. Is Discord running?", LogType.Error);
        SetStatus(DiscordConnectionStatus.Error);
    }
}
