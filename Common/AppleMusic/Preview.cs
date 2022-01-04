using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class Preview
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}