using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportExclusiveSummary
    {
        [JsonProperty("days")] // can be null when lifetime exclusive
        public int? Days { get; set; }

        [JsonProperty("description"), JsonRequired]
        public string Description { get; set; }

        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }
    }
}