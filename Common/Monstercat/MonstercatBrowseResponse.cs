using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.Monstercat;

public class MonstercatBrowseResponse
{
    [JsonProperty("Limit")]
    public int Limit { get; set; }

    [JsonProperty("Offset")]
    public int Offset { get; set; }

    [JsonProperty("Total")]
    public int Total { get; set; }

    [JsonProperty("NotFound")]
    public string NotFound { get; set; }

    [JsonProperty("Data")]
    public List<MonstercatTrack> Data { get; set; }
}