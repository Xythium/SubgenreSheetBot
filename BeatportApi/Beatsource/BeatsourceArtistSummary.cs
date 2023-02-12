using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceArtistSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image")] // can be null
    public BeatsourceImage? Image { get; set; }

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = default!;
}