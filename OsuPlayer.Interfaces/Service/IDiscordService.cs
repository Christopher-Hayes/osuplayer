using DiscordRPC;

namespace OsuPlayer.Interfaces.Service;

public enum DiscordConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public interface IDiscordService
{
    public DiscordConnectionStatus ConnectionStatus { get; }
    public event Action<DiscordConnectionStatus>? ConnectionStatusChanged;
    public void Initialize();
    public void DeInitialize();
    public Task UpdatePresence(string details, string state, int beatmapSetId = 0, Assets? assets = null, TimeSpan? elapsed = null, TimeSpan? durationLeft = null);
}
