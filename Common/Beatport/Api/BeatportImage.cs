using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportImage
{
    [JsonProperty("dynamic_uri"), JsonRequired]
    public string DynamicUri { get; set; } = default!;

    [JsonProperty("id"), JsonRequired]
    public string Id { get; set; } = default!;

    [JsonProperty("uri"), JsonRequired]
    public string Uri { get; set; } = default!;
}