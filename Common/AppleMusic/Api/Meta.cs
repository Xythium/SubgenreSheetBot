using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Meta
{
    [JsonProperty("popularity")]
    public decimal Popularity { get; set; }
}