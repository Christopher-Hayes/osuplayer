using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OsuPlayer.Network.MusicBrainz;

public record WikidataEntitiesResponse(
    [property: JsonPropertyName("entities")] Dictionary<string, WikidataEntity>? Entities
);

public record WikidataEntity(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("claims")] Dictionary<string, List<WikidataClaim>>? Claims
);

public record WikidataClaim(
    [property: JsonPropertyName("mainsnak")] WikidataMainsnak? Mainsnak
);

public record WikidataMainsnak(
    [property: JsonPropertyName("datavalue")] WikidataDatavalue? Datavalue
);

public record WikidataDatavalue(
    [property: JsonPropertyName("value")] object? Value
);
