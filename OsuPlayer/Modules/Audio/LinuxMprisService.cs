using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Modules.Audio.Interfaces;
using Tmds.DBus.Protocol;

namespace OsuPlayer.Modules.Audio;

/// <summary>
/// Implements MPRIS2 D-Bus service on Linux so desktop media keys (play/pause/next/previous)
/// are routed to osu!player by GNOME Shell and other MPRIS-aware environments.
///
/// All a{sv} dictionaries are built as Dictionary&lt;string, VariantValue&gt; and serialised
/// with <see cref="MessageWriter.WriteDictionary(Dictionary{string, VariantValue})"/> so that
/// alignment, type signatures and variant nesting are handled entirely by the library.
/// </summary>
public sealed class LinuxMprisService : IMethodHandler, IDisposable
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const string BusName = "org.mpris.MediaPlayer2.osuplayer";
    private const string MprisPath = "/org/mpris/MediaPlayer2";
    private const string PlayerIface = "org.mpris.MediaPlayer2.Player";
    private const string RootIface = "org.mpris.MediaPlayer2";
    private const string PropsIface = "org.freedesktop.DBus.Properties";
    private const string IntrospectIface = "org.freedesktop.DBus.Introspectable";

    private const string IntrospectXml = """
        <!DOCTYPE node PUBLIC "-//freedesktop//DTD D-BUS Object Introspection 1.0//EN"
          "http://www.freedesktop.org/standards/dbus/1.0/introspect.dtd">
        <node name="/org/mpris/MediaPlayer2">
          <interface name="org.freedesktop.DBus.Introspectable">
            <method name="Introspect">
              <arg direction="out" name="xml_data" type="s"/>
            </method>
          </interface>
          <interface name="org.freedesktop.DBus.Properties">
            <method name="Get">
              <arg direction="in" name="interface_name" type="s"/>
              <arg direction="in" name="property_name" type="s"/>
              <arg direction="out" name="value" type="v"/>
            </method>
            <method name="GetAll">
              <arg direction="in" name="interface_name" type="s"/>
              <arg direction="out" name="props" type="a{sv}"/>
            </method>
            <method name="Set">
              <arg direction="in" name="interface_name" type="s"/>
              <arg direction="in" name="property_name" type="s"/>
              <arg direction="in" name="value" type="v"/>
            </method>
            <signal name="PropertiesChanged">
              <arg name="interface_name" type="s"/>
              <arg name="changed_properties" type="a{sv}"/>
              <arg name="invalidated_properties" type="as"/>
            </signal>
          </interface>
          <interface name="org.mpris.MediaPlayer2">
            <method name="Raise"/>
            <method name="Quit"/>
            <property name="CanQuit" type="b" access="read"/>
            <property name="CanRaise" type="b" access="read"/>
            <property name="HasTrackList" type="b" access="read"/>
            <property name="Identity" type="s" access="read"/>
            <property name="SupportedUriSchemes" type="as" access="read"/>
            <property name="SupportedMimeTypes" type="as" access="read"/>
          </interface>
          <interface name="org.mpris.MediaPlayer2.Player">
            <method name="Next"/>
            <method name="Previous"/>
            <method name="Pause"/>
            <method name="PlayPause"/>
            <method name="Stop"/>
            <method name="Play"/>
            <method name="Seek">
              <arg direction="in" name="Offset" type="x"/>
            </method>
            <method name="SetPosition">
              <arg direction="in" name="TrackId" type="o"/>
              <arg direction="in" name="Position" type="x"/>
            </method>
            <method name="OpenUri">
              <arg direction="in" name="Uri" type="s"/>
            </method>
            <signal name="Seeked">
              <arg name="Position" type="x"/>
            </signal>
            <property name="PlaybackStatus" type="s" access="read"/>
            <property name="LoopStatus" type="s" access="readwrite"/>
            <property name="Rate" type="d" access="readwrite"/>
            <property name="Shuffle" type="b" access="readwrite"/>
            <property name="Metadata" type="a{sv}" access="read"/>
            <property name="Volume" type="d" access="readwrite"/>
            <property name="Position" type="x" access="read"/>
            <property name="MinimumRate" type="d" access="read"/>
            <property name="MaximumRate" type="d" access="read"/>
            <property name="CanGoNext" type="b" access="read"/>
            <property name="CanGoPrevious" type="b" access="read"/>
            <property name="CanPlay" type="b" access="read"/>
            <property name="CanPause" type="b" access="read"/>
            <property name="CanSeek" type="b" access="read"/>
            <property name="CanControl" type="b" access="read"/>
          </interface>
        </node>
        """;

    // ── State ────────────────────────────────────────────────────────────────

    private readonly IPlayer _player;
    private Connection? _connection;
    private bool _isPlaying;
    private IMapEntry? _currentSong;
    private string? _currentArtUrl;

    // ── IMethodHandler ───────────────────────────────────────────────────────

    public string Path => MprisPath;

    /// <summary>
    /// Run synchronously on the receive loop to avoid the async-void wrapper in
    /// Tmds.DBus.Protocol that calls Disconnect() on any unhandled exception.
    /// </summary>
    public bool RunMethodHandlerSynchronously(Message message) => true;

    // ── Constructor ──────────────────────────────────────────────────────────

    public LinuxMprisService(IPlayer player)
    {
        _player = player;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task StartAsync()
    {
        try
        {
            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();

            // Register our method handler BEFORE requesting the name so we can
            // respond to the burst of GetAll queries GNOME Shell fires the
            // instant the name appears on the bus.
            _connection.AddMethodHandler(this);

            // Request the well-known MPRIS bus name
            uint reply = await RequestNameAsync();
            if (reply is not (1 or 4)) // 1=PrimaryOwner, 4=AlreadyOwner
                throw new InvalidOperationException($"RequestName returned {reply}");

            Console.Error.WriteLine($"[MPRIS] Registered as {BusName}");

            // Announce full Player property set so the desktop starts routing
            // media keys to us immediately.
            EmitPropertiesChanged(PlayerIface, BuildPlayerProps());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MPRIS] Failed to start: {ex.GetType().Name}: {ex.Message}");
            _connection?.Dispose();
            _connection = null;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }

    // ── Public update methods (called from Player) ──────────────────────────

    public void UpdatePlaybackStatus(bool isPlaying)
    {
        if (_connection is null) return;
        _isPlaying = isPlaying;
        EmitPropertiesChanged(PlayerIface, new Dictionary<string, VariantValue>
        {
            ["PlaybackStatus"] = _isPlaying ? "Playing" : "Paused",
        });
    }

    public void UpdateMetadata(IMapEntry mapEntry, string? artPath)
    {
        if (_connection is null) return;
        _currentSong = mapEntry;
        _currentArtUrl = !string.IsNullOrEmpty(artPath) && File.Exists(artPath)
            ? $"file://{artPath}"
            : null;
        EmitPropertiesChanged(PlayerIface, new Dictionary<string, VariantValue>
        {
            ["Metadata"] = BuildMetadataVariant(),
        });
    }

    // ── IMethodHandler.HandleMethodAsync ─────────────────────────────────────

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var iface = context.Request.InterfaceAsString;
        var member = context.Request.MemberAsString;

        try
        {
            DispatchMethod(context, iface, member);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MPRIS] {iface}.{member} failed: {ex.Message}");
            try { context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message); }
            catch { /* prevent cascading disconnect */ }
        }

        return default;
    }

    // ── Method dispatch ──────────────────────────────────────────────────────

    private void DispatchMethod(MethodContext ctx, string? iface, string? member)
    {
        // ── Introspectable ──
        if (iface == IntrospectIface && member == "Introspect")
        {
            ReplyIntrospect(ctx);
            return;
        }

        // ── Properties ──
        if (iface == PropsIface)
        {
            HandleProperties(ctx, member);
            return;
        }

        // ── Player transport ──
        if (iface == PlayerIface)
        {
            HandlePlayerMethod(ctx, member);
            return;
        }

        // ── Root ──
        if (iface == RootIface)
        {
            HandleRootMethod(ctx, member);
            return;
        }

        // When interface is null, match by member name (some clients omit it)
        if (iface is null)
        {
            if (member == "Introspect") { ReplyIntrospect(ctx); return; }
            if (member is "Get" or "GetAll" or "Set") { HandleProperties(ctx, member); return; }
            if (IsPlayerMethod(member)) { HandlePlayerMethod(ctx, member); return; }
            if (member is "Raise" or "Quit") { HandleRootMethod(ctx, member); return; }
        }

        ctx.ReplyError("org.freedesktop.DBus.Error.UnknownMethod",
            $"Unknown: {iface ?? "(null)"}.{member ?? "(null)"}");
    }

    // ── Introspect ───────────────────────────────────────────────────────────

    private static void ReplyIntrospect(MethodContext ctx)
    {
        using var w = ctx.CreateReplyWriter("s");
        w.WriteString(IntrospectXml);
        ctx.Reply(w.CreateMessage());
    }

    // ── Properties ───────────────────────────────────────────────────────────

    private void HandleProperties(MethodContext ctx, string? member)
    {
        var reader = ctx.Request.GetBodyReader();

        switch (member)
        {
            case "GetAll":
            {
                var target = reader.ReadString();
                var dict = target switch
                {
                    PlayerIface => BuildPlayerProps(),
                    RootIface   => BuildRootProps(),
                    _           => new Dictionary<string, VariantValue>(),
                };
                using var w = ctx.CreateReplyWriter("a{sv}");
                w.WriteDictionary(dict);
                ctx.Reply(w.CreateMessage());
                return;
            }
            case "Get":
            {
                var target = reader.ReadString();
                var prop = reader.ReadString();
                using var w = ctx.CreateReplyWriter("v");
                w.WriteVariant(GetSingleProperty(target, prop));
                ctx.Reply(w.CreateMessage());
                return;
            }
            case "Set":
            {
                // Read-only — silently acknowledge
                using var w = ctx.CreateReplyWriter(null);
                ctx.Reply(w.CreateMessage());
                return;
            }
        }

        ctx.ReplyError("org.freedesktop.DBus.Error.UnknownMethod",
            $"Unknown property method: {member}");
    }

    // ── Player methods ───────────────────────────────────────────────────────

    private void HandlePlayerMethod(MethodContext ctx, string? member)
    {
        switch (member)
        {
            case "PlayPause":
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(_player.PlayPause);
                break;
            case "Play":
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(_player.Play);
                break;
            case "Pause":
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(_player.Pause);
                break;
            case "Stop":
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(_player.Stop);
                break;
            case "Next":
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                    () => _player.NextSong(PlayDirection.Forward));
                break;
            case "Previous":
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                    () => _player.NextSong(PlayDirection.Backwards));
                break;
            case "Seek":
            case "SetPosition":
            case "OpenUri":
                break; // Not implemented — acknowledge silently
            default:
                ctx.ReplyError("org.freedesktop.DBus.Error.UnknownMethod",
                    $"Unknown player method: {member}");
                return;
        }

        using var w = ctx.CreateReplyWriter(null);
        ctx.Reply(w.CreateMessage());
    }

    // ── Root methods ─────────────────────────────────────────────────────────

    private static void HandleRootMethod(MethodContext ctx, string? member)
    {
        if (member == "Quit") Environment.Exit(0);

        // Raise is a no-op — we don't support it
        using var w = ctx.CreateReplyWriter(null);
        ctx.Reply(w.CreateMessage());
    }

    // ── Property builders ────────────────────────────────────────────────────

    private Dictionary<string, VariantValue> BuildPlayerProps()
    {
        return new Dictionary<string, VariantValue>
        {
            ["PlaybackStatus"] = _isPlaying ? "Playing" : "Paused",
            ["LoopStatus"]     = "None",
            ["Rate"]           = 1.0,
            ["Shuffle"]        = VariantValue.Bool(false),
            ["Volume"]         = 1.0,
            ["Position"]       = VariantValue.Int64(0),
            ["MinimumRate"]    = 1.0,
            ["MaximumRate"]    = 1.0,
            ["CanGoNext"]      = VariantValue.Bool(true),
            ["CanGoPrevious"]  = VariantValue.Bool(true),
            ["CanPlay"]        = VariantValue.Bool(true),
            ["CanPause"]       = VariantValue.Bool(true),
            ["CanSeek"]        = VariantValue.Bool(false),
            ["CanControl"]     = VariantValue.Bool(true),
            ["Metadata"]       = BuildMetadataVariant(),
        };
    }

    private static Dictionary<string, VariantValue> BuildRootProps()
    {
        return new Dictionary<string, VariantValue>
        {
            ["CanQuit"]             = VariantValue.Bool(false),
            ["CanRaise"]            = VariantValue.Bool(false),
            ["HasTrackList"]        = VariantValue.Bool(false),
            ["Identity"]            = "osu!player",
            ["SupportedUriSchemes"] = new Array<string>(),
            ["SupportedMimeTypes"]  = new Array<string>(),
        };
    }

    /// <summary>
    /// Builds the Metadata property value as a VariantValue wrapping an a{sv} dict.
    /// Using <see cref="Dict{TKey, TValue}"/> ensures the library handles all
    /// variant nesting and type signatures correctly.
    /// </summary>
    private VariantValue BuildMetadataVariant()
    {
        var meta = new Dict<string, VariantValue>
        {
            ["mpris:trackid"] = new ObjectPath("/org/mpris/MediaPlayer2/TrackList/NoTrack"),
        };

        if (_currentSong is not null)
        {
            meta["xesam:title"] = _currentSong.Title ?? string.Empty;

            if (!string.IsNullOrEmpty(_currentSong.Artist))
                meta["xesam:artist"] = new Array<string> { _currentSong.Artist };

            if (!string.IsNullOrEmpty(_currentArtUrl))
                meta["mpris:artUrl"] = _currentArtUrl;
        }

        return meta;
    }

    /// <summary>Get a single property as a VariantValue for Properties.Get replies.</summary>
    private VariantValue GetSingleProperty(string iface, string prop)
    {
        if (iface == PlayerIface)
        {
            return prop switch
            {
                "PlaybackStatus" => (VariantValue)(_isPlaying ? "Playing" : "Paused"),
                "LoopStatus"     => (VariantValue)"None",
                "Rate"           => (VariantValue)1.0,
                "Shuffle"        => VariantValue.Bool(false),
                "Volume"         => (VariantValue)1.0,
                "Position"       => VariantValue.Int64(0),
                "MinimumRate"    => (VariantValue)1.0,
                "MaximumRate"    => (VariantValue)1.0,
                "CanGoNext"      => VariantValue.Bool(true),
                "CanGoPrevious"  => VariantValue.Bool(true),
                "CanPlay"        => VariantValue.Bool(true),
                "CanPause"       => VariantValue.Bool(true),
                "CanSeek"        => VariantValue.Bool(false),
                "CanControl"     => VariantValue.Bool(true),
                "Metadata"       => BuildMetadataVariant(),
                _                => (VariantValue)string.Empty,
            };
        }

        if (iface == RootIface)
        {
            return prop switch
            {
                "CanQuit"             => VariantValue.Bool(false),
                "CanRaise"            => VariantValue.Bool(false),
                "HasTrackList"        => VariantValue.Bool(false),
                "Identity"            => (VariantValue)"osu!player",
                "SupportedUriSchemes" => new Array<string>(),
                "SupportedMimeTypes"  => new Array<string>(),
                _                     => (VariantValue)string.Empty,
            };
        }

        return string.Empty;
    }

    // ── Signal emission ──────────────────────────────────────────────────────

    private void EmitPropertiesChanged(string iface, Dictionary<string, VariantValue> changed)
    {
        if (_connection is null) return;
        try
        {
            using var w = _connection.GetMessageWriter();
            w.WriteSignalHeader(
                path: MprisPath,
                @interface: PropsIface,
                member: "PropertiesChanged",
                signature: "sa{sv}as");
            w.WriteString(iface);
            w.WriteDictionary(changed);
            w.WriteArray(System.Array.Empty<string>());
            _connection.TrySendMessage(w.CreateMessage());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MPRIS] EmitPropertiesChanged failed: {ex.Message}");
        }
    }

    // ── D-Bus helpers ────────────────────────────────────────────────────────

    private Task<uint> RequestNameAsync()
    {
        MessageBuffer msg = CreateRequestNameMessage();
        return _connection!.CallMethodAsync(
            msg,
            static (Message m, object? _) => m.GetBodyReader().ReadUInt32());
    }

    private MessageBuffer CreateRequestNameMessage()
    {
        using var w = _connection!.GetMessageWriter();
        w.WriteMethodCallHeader(
            destination: "org.freedesktop.DBus",
            path: "/org/freedesktop/DBus",
            @interface: "org.freedesktop.DBus",
            member: "RequestName",
            signature: "su");
        w.WriteString(BusName);
        w.WriteUInt32(1); // DBUS_NAME_FLAG_DO_NOT_QUEUE
        return w.CreateMessage();
    }

    private static bool IsPlayerMethod(string? m) =>
        m is "PlayPause" or "Play" or "Pause" or "Stop" or "Next" or "Previous"
            or "Seek" or "SetPosition" or "OpenUri";
}
