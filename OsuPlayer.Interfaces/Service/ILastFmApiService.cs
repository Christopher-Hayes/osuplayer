namespace OsuPlayer.Interfaces.Service;

public interface ILastFmApiService
{
    public void SetApiKeyAndSecret(string apiKey, string secret);
    public Task Scrobble(string title, string artist);
    public bool LoadSessionKey();
    public Task<bool> LoadSessionKeyAsync();
    public bool IsAuthorized();

    /// <summary>
    /// Validates the current session key against the Last.fm server.
    /// Returns true when the key is accepted, false when it is revoked/expired (error 9).
    /// Also clears the in-memory key and wipes the on-disk session file when invalid.
    /// </summary>
    public Task<bool> ValidateSessionAsync();

    public Task<string> GetAuthToken();
    public void AuthorizeToken();
    public Task GetSessionKey();
    public Task SaveSessionKeyAsync();

    /// <summary>
    /// Gets artist info from the Last.fm API (artist.getInfo)
    /// </summary>
    /// <param name="artist">The artist name</param>
    /// <returns>Raw JSON string response, or null on failure</returns>
    public Task<string?> GetArtistInfoAsync(string artist);

    /// <summary>
    /// Gets similar artists from the Last.fm API (artist.getSimilar)
    /// </summary>
    /// <param name="artist">The artist name</param>
    /// <param name="limit">Max number of similar artists to return</param>
    /// <returns>Raw JSON string response, or null on failure</returns>
    public Task<string?> GetSimilarArtistsAsync(string artist, int limit = 10);
}