using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportLabelSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image"), JsonRequired]
    public BeatportImage Image { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;
}