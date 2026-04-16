using System.Text.Json.Serialization;

namespace OsuPlayer.Network.LastFm.Responses;

public record ArtistInfoResponse(
    [property: JsonPropertyName("artist")] ArtistInfoData? Artist
);

public record ArtistInfoData(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("mbid")] string? Mbid,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("image")] List<ArtistImage>? Image,
    [property: JsonPropertyName("stats")] ArtistStats? Stats,
    [property: JsonPropertyName("bio")] ArtistBio? Bio,
    [property: JsonPropertyName("similar")] SimilarArtistsContainer? Similar,
    [property: JsonPropertyName("tags")] ArtistTagsContainer? Tags
);

public record ArtistImage(
    [property: JsonPropertyName("#text")] string? Url,
    [property: JsonPropertyName("size")] string? Size
);

public record ArtistStats(
    [property: JsonPropertyName("listeners")] string? Listeners,
    [property: JsonPropertyName("playcount")] string? Playcount
);

public record ArtistBio(
    [property: JsonPropertyName("published")] string? Published,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("content")] string? Content
);

public record SimilarArtistsContainer(
    [property: JsonPropertyName("artist")] List<SimilarArtistData>? Artist
);

public record SimilarArtistData(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("image")] List<ArtistImage>? Image
);

public record ArtistTagsContainer(
    [property: JsonPropertyName("tag")] List<ArtistTag>? Tag
);

public record ArtistTag(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("url")] string? Url
);
