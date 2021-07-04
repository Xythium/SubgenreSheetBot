using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourceReleaseSummary
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("image"), JsonRequired]
        public BeatsourceImage Image { get; set; }

        [JsonProperty("label"), JsonRequired]
        public BeatsourceLabelSummary Label { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("slug"), JsonRequired]
        public string Slug { get; set; }
    }
}