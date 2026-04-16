using System.Text.Json.Serialization;

namespace OsuPlayer.Network.LastFm.Responses;

public record SimilarArtistsResponse(
    [property: JsonPropertyName("similarartists")] SimilarArtistsData? SimilarArtists
);

public record SimilarArtistsData(
    [property: JsonPropertyName("artist")] List<SimilarArtistEntry>? Artist
);

public record SimilarArtistEntry(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("match")] string? Match,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("image")] List<ArtistImage>? Image
);
