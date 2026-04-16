using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OsuPlayer.Network.MusicBrainz;

public record MusicBrainzArtistResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("relations")] List<MusicBrainzRelation>? Relations
);

public record MusicBrainzRelation(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("url")] MusicBrainzUrl? Url
);

public record MusicBrainzUrl(
    [property: JsonPropertyName("resource")] string? Resource
);
