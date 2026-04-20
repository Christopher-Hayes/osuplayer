namespace OsuPlayer.Data.OsuPlayer.Classes;

public class AddToPlaylistContextMenuEntry
{
    public string Name { get; set; }
    public Action<string> Action { get; set; }

    /// <summary>
    /// When true, all songs for the relevant context are already in this playlist.
    /// The UI should show a "remove" affordance instead of "add".
    /// </summary>
    public bool IsFullyInPlaylist { get; set; }

    public AddToPlaylistContextMenuEntry(string name, Action<string> action, bool isFullyInPlaylist = false)
    {
        Name = name;
        Action = action;
        IsFullyInPlaylist = isFullyInPlaylist;
    }
}