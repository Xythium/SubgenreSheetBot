using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class SongRelationships
    {
        [JsonProperty("composers")]
        public Composers Composers { get; set; }
        
        [JsonProperty("artists")]
        public ArtistSongRelationships Artists { get; set; }
    }
}