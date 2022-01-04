using Newtonsoft.Json;

namespace Common.AppleMusic
{
    public class Meta
    {
        [JsonProperty("popularity")]
        public decimal Popularity { get; set; }
    }
}