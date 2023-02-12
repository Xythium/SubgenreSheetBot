using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourcePrice
{
    [JsonProperty("code"), JsonRequired]
    public string Code { get; set; } = default!;

    [JsonProperty("display"), JsonRequired]
    public string Display { get; set; } = default!;

    [JsonProperty("symbol"), JsonRequired]
    public string Symbol { get; set; } = default!;

    [JsonProperty("value"), JsonRequired]
    public decimal Value { get; set; } = default!;
}