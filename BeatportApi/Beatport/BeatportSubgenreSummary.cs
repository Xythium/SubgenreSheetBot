﻿using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportSubgenreSummary
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("slug"), JsonRequired]
        public string Slug { get; set; }

        [JsonProperty("url"), JsonRequired]
        public string Url { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }
    }
}