using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class RecordLabels
    {
        [JsonProperty("href")]
        public string Href { get; set; }
    }
}