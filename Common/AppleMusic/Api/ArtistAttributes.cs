using Newtonsoft.Json;

namespace Common.AppleMusic.Api
{
    public class ArtistAttributes
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("url")]
        public string Url { get; set; }
        
    }
}