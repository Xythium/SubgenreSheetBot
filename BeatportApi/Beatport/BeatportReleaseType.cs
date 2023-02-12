using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportReleaseType
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;
}