using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Artwork
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("textColor1")]
    public string TextColor1 { get; set; }

    [JsonProperty("textColor2")]
    public string TextColor2 { get; set; }

    [JsonProperty("textColor3")]
    public string TextColor3 { get; set; }

    [JsonProperty("textColor4")]
    public string TextColor4 { get; set; }

    [JsonProperty("bgColor")]
    public string BackgroundColor { get; set; }

    [JsonProperty("hasP3")]
    public bool HasP3 { get; set; }
}