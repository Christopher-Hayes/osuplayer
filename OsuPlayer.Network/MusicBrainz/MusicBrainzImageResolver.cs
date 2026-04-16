using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OsuPlayer.Network.MusicBrainz;

/// <summary>
/// Resolves an artist image URL via MusicBrainz url-rels + Wikidata.
/// Flow: MBID → MusicBrainz url-rels → Wikidata entity → Commons image filename → image URL.
/// No API keys required.
/// </summary>
public static class MusicBrainzImageResolver
{
    private static readonly HttpClient _http = new()
    {
        // MusicBrainz requires a proper User-Agent
        DefaultRequestHeaders =
        {
            { "User-Agent", "MusicPlayerForOsu/1.0 (https://github.com/Christopher-Hayes/osuplayer)" }
        }
    };

    /// <summary>
    /// Given a MusicBrainz artist MBID, returns a usable image URL or null.
    /// Tries to find a Wikidata "wikidata" url-rel and resolves the P18 (image) property.
    /// </summary>
    public static async Task<string?> GetArtistImageUrlAsync(string mbid)
    {
        try
        {
            var mbUrl = $"https://musicbrainz.org/ws/2/artist/{mbid}?inc=url-rels&fmt=json";
            var mbJson = await _http.GetStringAsync(mbUrl);

            var mbResponse = JsonSerializer.Deserialize<MusicBrainzArtistResponse>(mbJson);
            if (mbResponse?.Relations == null) return null;

            // Look for a "wikidata" url relation
            string? wikidataEntityId = null;
            foreach (var rel in mbResponse.Relations)
            {
                if (string.Equals(rel.Type, "wikidata", StringComparison.OrdinalIgnoreCase)
                    && rel.Url?.Resource != null)
                {
                    // Resource looks like: https://www.wikidata.org/wiki/Q12345
                    var parts = rel.Url.Resource.TrimEnd('/').Split('/');
                    if (parts.Length > 0)
                        wikidataEntityId = parts[^1]; // e.g. "Q12345"
                    break;
                }
            }

            if (wikidataEntityId == null) return null;

            return await GetImageFromWikidataAsync(wikidataEntityId);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> GetImageFromWikidataAsync(string entityId)
    {
        try
        {
            var wdUrl = $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={entityId}&props=claims&format=json";
            var wdJson = await _http.GetStringAsync(wdUrl);

            // Parse with JsonDocument for flexibility since the value can be a string or object
            using var doc = JsonDocument.Parse(wdJson);

            if (!doc.RootElement.TryGetProperty("entities", out var entities)) return null;
            if (!entities.TryGetProperty(entityId, out var entity)) return null;
            if (!entity.TryGetProperty("claims", out var claims)) return null;

            // P18 = image property in Wikidata
            if (!claims.TryGetProperty("P18", out var p18Claims)) return null;

            foreach (var claim in p18Claims.EnumerateArray())
            {
                if (!claim.TryGetProperty("mainsnak", out var mainsnak)) continue;
                if (!mainsnak.TryGetProperty("datavalue", out var datavalue)) continue;
                if (!datavalue.TryGetProperty("value", out var value)) continue;

                string? filename = null;
                if (value.ValueKind == JsonValueKind.String)
                    filename = value.GetString();
                else if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var innerValue))
                    filename = innerValue.GetString();

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    // Build the Wikimedia Commons Special:FilePath URL (free, no redirect needed)
                    var encoded = Uri.EscapeDataString(filename.Replace(' ', '_'));
                    return $"https://commons.wikimedia.org/wiki/Special:FilePath/{encoded}?height=512";
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
