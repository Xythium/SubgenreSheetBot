using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class ArtistSongRelationships
{
    [JsonProperty("href")]
    public string Href { get; set; }
        
    [JsonProperty("data")]
    public List<Artist> Data { get; set; }
}