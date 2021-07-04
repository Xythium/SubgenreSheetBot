using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportArtistSummary
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("image"), JsonRequired]
        public BeatportImage Image { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("slug"), JsonRequired]
        public string Slug { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }
    }
}