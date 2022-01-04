using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class Relationships
    {
        [JsonProperty("artists")]
        public Artists Artists { get; set; }

        [JsonProperty("record-labels")]
        public RecordLabels RecordLabels { get; set; }

        [JsonProperty("tracks")]
        public Tracks Tracks { get; set; }
    }
}