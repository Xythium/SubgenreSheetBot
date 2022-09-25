using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.AppleMusic.Api
{
    public class Artists

    {
        [JsonProperty("data")]
        public List<Artist> Data { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }
    }
}