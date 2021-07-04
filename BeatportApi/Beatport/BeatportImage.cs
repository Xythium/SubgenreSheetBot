using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportImage
    {
        [JsonProperty("dynamic_uri"), JsonRequired]
        public string DynamicUri { get; set; }

        [JsonProperty("id"), JsonRequired]
        public string Id { get; set; }

        [JsonProperty("uri"), JsonRequired]
        public string Uri { get; set; }
    }
}