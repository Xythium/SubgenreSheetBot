using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportBpmRange
{
    [JsonProperty("min")]
    public int? Min { get; set; }

    [JsonProperty("max")]
    public int? Max { get; set; }
}