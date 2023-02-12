using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportKeySummary
{
    [JsonProperty("camelot_letter"), JsonRequired]
    public string CamelotLetter { get; set; } = default!;

    [JsonProperty("camelot_number"), JsonRequired]
    public int CamelotNumber { get; set; } = default!;

    [JsonProperty("chord_type"), JsonRequired]
    public BeatportChordSummary ChordType { get; set; } = default!;

    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("is_flat"), JsonRequired]
    public bool IsFlat { get; set; } = default!;

    [JsonProperty("is_sharp"), JsonRequired]
    public bool IsSharp { get; set; } = default!;

    [JsonProperty("letter"), JsonRequired]
    public string Letter { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = default!;
}