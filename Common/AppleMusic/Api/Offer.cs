using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Offer
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("priceFormatted")]
    public string PriceFormatted { get; set; }

    [JsonProperty("buyParams")]
    public string BuyParams { get; set; }
}