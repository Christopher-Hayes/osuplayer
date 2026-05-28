using OsuPlayer.Interfaces.Service;
using OsuPlayer.Services.ShuffleImpl;

namespace OsuPlayer.Services;

/// <summary>
/// Provides the shuffle implementation used for song navigation.
/// Always uses <see cref="RngHistoryShuffler"/> — random selection with a
/// 10-entry history buffer so the user can go back to previously played songs.
/// </summary>
public class ShuffleService : OsuPlayerService, IShuffleServiceProvider
{
    public List<IShuffleImpl> ShuffleAlgorithms { get; } = new();
    public IShuffleImpl? ShuffleImpl { get; private set; }

    public override string ServiceName => "SHUFFLE_SERVICE";

    public ShuffleService()
    {
        ShuffleImpl = new RngHistoryShuffler();
    }

    // No-op: algorithm is fixed; kept for interface compatibility.
    public void SetShuffleImpl(IShuffleImpl? algorithm) { }
}