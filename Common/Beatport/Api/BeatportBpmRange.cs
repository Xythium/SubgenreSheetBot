using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportBpmRange
{
    [JsonProperty("min")]
    public int? Min { get; set; }

    [JsonProperty("max")]
    public int? Max { get; set; }
}