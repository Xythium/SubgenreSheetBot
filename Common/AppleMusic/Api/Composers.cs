using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Composers
{
    [JsonProperty("href")]
    public string Href { get; set; }
}