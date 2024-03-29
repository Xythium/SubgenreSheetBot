﻿using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Albums
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("href")]
    public string Href { get; set; }

    [JsonProperty("attributes")]
    public Attributes Attributes { get; set; }

    [JsonProperty("relationships")]
    public Relationships Relationships { get; set; }
}