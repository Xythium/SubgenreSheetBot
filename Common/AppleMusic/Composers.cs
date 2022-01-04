using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class Composers
    {
        [JsonProperty("href")]
        public string Href { get; set; }
    }
}