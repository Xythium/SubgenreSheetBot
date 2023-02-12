using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceLabelSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image")] // optional
    public BeatsourceImage? Image { get; set; }

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;
}