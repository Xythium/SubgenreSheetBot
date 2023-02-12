using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class SongRelationships
{
    [JsonProperty("composers")]
    public Composers Composers { get; set; }
        
    [JsonProperty("artists")]
    public ArtistSongRelationships Artists { get; set; }
}