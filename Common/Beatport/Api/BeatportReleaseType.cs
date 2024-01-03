using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportReleaseType
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;
}