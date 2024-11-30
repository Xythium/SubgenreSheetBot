using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceImage
{
    [JsonProperty("id")]
    public string? Id { get; set; } = default!;

    [JsonProperty("uri")]
    public string? Uri { get; set; } = default!;

    [JsonProperty("dynamic_uri")]
    public string? DynamicUri { get; set; } = default!;
}