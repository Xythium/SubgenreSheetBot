using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourceExclusiveSummary
    {
        [JsonProperty("days"), JsonRequired]
        public int Days { get; set; }

        [JsonProperty("description"), JsonRequired]
        public string Description { get; set; }

        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }
    }
}