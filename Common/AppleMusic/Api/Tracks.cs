﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class Tracks
{
    [JsonProperty("href")]
    public string Href { get; set; }

    [JsonProperty("data")]
    public List<Song> Data { get; set; }
}