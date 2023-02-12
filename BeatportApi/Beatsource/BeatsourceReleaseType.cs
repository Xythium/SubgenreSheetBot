using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceReleaseType
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;
}