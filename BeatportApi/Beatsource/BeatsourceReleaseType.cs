using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourceReleaseType
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }
    }
}