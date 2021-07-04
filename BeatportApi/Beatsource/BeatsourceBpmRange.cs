using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourceBpmRange
    {
        [JsonProperty("min")]
        public int? Min { get; set; }

        [JsonProperty("max")]
        public int? Max { get; set; }
    }
}