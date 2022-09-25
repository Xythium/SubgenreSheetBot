using Newtonsoft.Json;

namespace Common.AppleMusic.Api
{
    public class Artist
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("href")]
        public string Href { get; set; }
        
        [JsonProperty("attributes")]
        public ArtistAttributes Attributes { get; set; }
    }
}