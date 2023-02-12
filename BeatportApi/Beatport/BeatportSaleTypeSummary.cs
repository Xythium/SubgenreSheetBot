using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportSaleTypeSummary
{
    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = default!;
}