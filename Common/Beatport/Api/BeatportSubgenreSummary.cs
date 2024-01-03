using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportSubgenreSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = default!;

    [JsonProperty("enabled")]
    public bool Enabled { get; set; }
}