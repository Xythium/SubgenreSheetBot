using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportReleaseSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image"), JsonRequired]
    public BeatportImage Image { get; set; } = default!;

    [JsonProperty("label"), JsonRequired]
    public BeatportLabelSummary Label { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;
}