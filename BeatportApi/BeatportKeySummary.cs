using Newtonsoft.Json;

namespace BeatportApi
{
    public class BeatportKeySummary
    {
        [JsonProperty("camelot_letter"), JsonRequired]
        public string CamelotLetter { get; set; }

        [JsonProperty("camelot_number"), JsonRequired]
        public int CamelotNumber { get; set; }

        [JsonProperty("chord_type"), JsonRequired]
        public BeatportChordSummary ChordType { get; set; }

        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("is_flat"), JsonRequired]
        public bool IsFlat { get; set; }

        [JsonProperty("is_sharp"), JsonRequired]
        public bool IsSharp { get; set; }

        [JsonProperty("letter"), JsonRequired]
        public string Letter { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }
    }
}