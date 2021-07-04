using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourceChordSummary
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }
    }
}