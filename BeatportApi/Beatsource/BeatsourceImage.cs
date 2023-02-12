using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceImage
{
    [JsonProperty("id"), JsonRequired]
    public string Id { get; set; } = default!;

    [JsonProperty("uri"), JsonRequired]
    public string Uri { get; set; } = default!;

    [JsonProperty("dynamic_uri"), JsonRequired]
    public string DynamicUri { get; set; } = default!;
}