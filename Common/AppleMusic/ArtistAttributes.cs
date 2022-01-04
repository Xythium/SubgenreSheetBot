using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class ArtistAttributes
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("url")]
        public string Url { get; set; }
        
    }
}