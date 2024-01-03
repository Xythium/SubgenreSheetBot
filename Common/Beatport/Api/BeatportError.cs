using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportError
{
    [JsonProperty("detail")]
    public string Detail { get; set; } = default!;
}