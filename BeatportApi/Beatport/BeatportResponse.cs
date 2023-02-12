using System.Collections.Generic;
using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportResponse<T>
{
    [JsonProperty("page"), JsonRequired]
    public string Page { get; set; } = default!;

    [JsonProperty("per_page"), JsonRequired]
    public int PerPage { get; set; } = default!;

    [JsonProperty("count"), JsonRequired]
    public int Count { get; set; } = default!;

    [JsonProperty("next")] // optional
    public string? Next { get; set; }

    [JsonProperty("previous")] // optional
    public string? Previous { get; set; }

    [JsonProperty("results"), JsonRequired]
    public List<T> Results { get; set; } = default!;
}