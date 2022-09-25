using Newtonsoft.Json;

namespace Common.AppleMusic.Api
{
    public class Preview
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}