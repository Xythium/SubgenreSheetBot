using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceExclusiveSummary
{
    [JsonProperty("days"), JsonRequired]
    public int Days { get; set; } = default!;

    [JsonProperty("description"), JsonRequired]
    public string Description { get; set; } = default!;

    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = default!;
}