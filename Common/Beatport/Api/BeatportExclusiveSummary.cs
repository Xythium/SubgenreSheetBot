using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportExclusiveSummary
{
    [JsonProperty("days")] // can be null when lifetime exclusive
    public int? Days { get; set; }

    [JsonProperty("description"), JsonRequired]
    public string Description { get; set; } = default!;

    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = default!;
}