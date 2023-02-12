using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceError
{
    [JsonProperty("detail")]
    public string Detail { get; set; } = default!;

    [JsonProperty("Error")]
    public string Error { get; set; } = default!;
}