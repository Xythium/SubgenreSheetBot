using Newtonsoft.Json;

namespace Common.AppleMusic.Api
{
    public class RecordLabels
    {
        [JsonProperty("href")]
        public string Href { get; set; }
    }
}