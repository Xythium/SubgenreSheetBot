using Newtonsoft.Json;

namespace Common.Monstercat;

public class MonstercatLink
{
    [JsonProperty("Url")]
    public string Url { get; set; }

    public string Platform { get; set; }
}