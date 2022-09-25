using System.Collections.Generic;
using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportResponse<T>
    {
        [JsonProperty("page"), JsonRequired]
        public string Page { get; set; }

        [JsonProperty("per_page"), JsonRequired]
        public int PerPage { get; set; }

        [JsonProperty("count"), JsonRequired]
        public int Count { get; set; }

        [JsonProperty("next")] // optional
        public string? Next { get; set; }

        [JsonProperty("previous")] // optional
        public string? Previous { get; set; }

        [JsonProperty("results"), JsonRequired]
        public List<T> Results { get; set; }
    }
}