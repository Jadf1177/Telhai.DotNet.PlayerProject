using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Telhai.DotNet.PlayerProject.Models
{
    public class ItunesSearchResponse
    {
        [JsonPropertyName("resultCount")]
        public int ResultCount { get; set; }

        [JsonPropertyName("results")]
        public List<ItunesTrackResult> Results { get; set; } = new List<ItunesTrackResult>();
    }

    public class ItunesTrackResult
    {
        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("collectionName")]
        public string? CollectionName { get; set; }

        [JsonPropertyName("artworkUrl100")]
        public string? ArtworkUrl100 { get; set; }

        // Optionally use larger size if exists (not always present)
        [JsonPropertyName("artworkUrl600")]
        public string? ArtworkUrl600 { get; set; }
    }
}
