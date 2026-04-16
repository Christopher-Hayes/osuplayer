using Avalonia.Controls;
using Avalonia.Threading;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Modules.Audio.Interfaces;

namespace OsuPlayer.Modules;

/// <summary>
/// Manages the "playing" CSS class on <see cref="ListBoxItem"/> containers inside a
/// <see cref="ListBox"/> so that the currently playing song is visually highlighted.
/// </summary>
public sealed class NowPlayingHighlighter
{
    private readonly ListBox _listBox;
    private readonly IPlayer _player;
    private const string PlayingClass = "playing";

    public NowPlayingHighlighter(ListBox listBox, IPlayer player)
    {
        _listBox = listBox;
        _player = player;

        _listBox.ContainerPrepared += OnContainerPrepared;
        _listBox.ContainerClearing += OnContainerClearing;

        _player.CurrentSong.ValueChanged += _ =>
        {
            Dispatcher.UIThread.Post(RefreshAll);
        };
    }

    public void Detach()
    {
        _listBox.ContainerPrepared -= OnContainerPrepared;
        _listBox.ContainerClearing -= OnContainerClearing;
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem item)
            UpdateClass(item);
    }

    private void OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is ListBoxItem item)
            item.Classes.Remove(PlayingClass);
    }

    private void RefreshAll()
    {
        if (_listBox.ItemsSource == null) return;

        var itemCount = _listBox.ItemCount;
        for (var i = 0; i < itemCount; i++)
        {
            if (_listBox.ContainerFromIndex(i) is ListBoxItem item)
                UpdateClass(item);
        }
    }

    private void UpdateClass(ListBoxItem item)
    {
        var currentHash = _player.CurrentSong.Value?.Hash;
        var isPlaying = item.DataContext is IMapEntryBase song
                        && currentHash != null
                        && song.Hash == currentHash;

        if (isPlaying)
            item.Classes.Add(PlayingClass);
        else
            item.Classes.Remove(PlayingClass);
    }
}