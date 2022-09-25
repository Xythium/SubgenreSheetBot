using Newtonsoft.Json;

namespace Common.AppleMusic.Api
{
    public class Song
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("attributes")]
        public SongAttributes Attributes { get; set; }

        [JsonProperty("relationships")]
        public SongRelationships Relationships { get; set; }

        [JsonProperty("meta")]
        public Meta Meta { get; set; }
    }
}