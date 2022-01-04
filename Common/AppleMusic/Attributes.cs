using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class Attributes
    {
        [JsonProperty("artwork")]
        public Artwork Artwork { get; set; }

        [JsonProperty("artistName")]
        public string ArtistName { get; set; }

        [JsonProperty("isSingle")]
        public bool IsSingle { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; }

        [JsonProperty("genreNames")]
        public List<string> GenreNames { get; set; }

        [JsonProperty("trackCount")]
        public int TrackCount { get; set; }

        [JsonProperty("isMasteredForItunes")]
        public bool IsMasteredForItunes { get; set; }

        [JsonProperty("releaseDate")]
        public string ReleaseDate { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("recordLabel")]
        public string RecordLabel { get; set; }

        [JsonProperty("upc")]
        public string Upc { get; set; }

        [JsonProperty("audioTraits")]
        public List<string> AudioTraits { get; set; }

        [JsonProperty("copyright")]
        public string Copyright { get; set; }

        [JsonProperty("isCompilation")]
        public bool IsCompilation { get; set; }

        [JsonProperty("offers")]
        public List<Offer> Offers { get; set; }
    }
}