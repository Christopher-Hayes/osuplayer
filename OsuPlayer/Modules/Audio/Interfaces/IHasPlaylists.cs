using System.ComponentModel;
using OsuPlayer.Data.OsuPlayer.StorageModels;

namespace OsuPlayer.Modules.Audio.Interfaces;

/// <summary>
/// This interface provides playlist capability.
/// </summary>
public interface IHasPlaylists
{
    public Bindable<Playlist?> SelectedPlaylist { get; }
    public Bindable<bool> PlaylistEnableOnPlay { get; }

    /// <summary>
    /// The playlist currently used as the playback context (i.e. next/prev navigates within it).
    /// Null means the full library is the context.
    /// </summary>
    public Bindable<Playlist?> ActivePlaylistContext { get; }

    public event PropertyChangedEventHandler? PlaylistChanged;

    /// <summary>
    /// Triggers if the playlist got changed
    /// </summary>
    /// <param name="e"></param>
    public void TriggerPlaylistChanged(PropertyChangedEventArgs e);
}