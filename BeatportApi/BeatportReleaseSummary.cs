﻿using Newtonsoft.Json;

namespace BeatportApi
{
    public class BeatportReleaseSummary
    {
        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("image"), JsonRequired]
        public BeatportImage Image { get; set; }

        [JsonProperty("label"), JsonRequired]
        public BeatportLabelSummary Label { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("slug"), JsonRequired]
        public string Slug { get; set; }
    }
}