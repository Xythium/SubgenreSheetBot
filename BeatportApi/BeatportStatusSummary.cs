using Newtonsoft.Json;

namespace BeatportApi
{
    public class BeatportStatusSummary
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }
    }
}