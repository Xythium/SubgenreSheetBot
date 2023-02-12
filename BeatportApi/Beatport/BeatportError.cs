using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportError
{
    [JsonProperty("detail")]
    public string Detail { get; set; } = default!;
}