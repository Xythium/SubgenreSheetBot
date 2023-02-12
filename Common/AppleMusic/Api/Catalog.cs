using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Catalog
{
    [JsonProperty("x")]
    public long X { get; set; }

    [JsonProperty("d")]
    public List<Albums> D { get; set; }
}