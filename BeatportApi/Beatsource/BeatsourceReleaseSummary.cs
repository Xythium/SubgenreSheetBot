using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceReleaseSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image"), JsonRequired]
    public BeatsourceImage Image { get; set; } = default!;

    [JsonProperty("label"), JsonRequired]
    public BeatsourceLabelSummary Label { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;
}