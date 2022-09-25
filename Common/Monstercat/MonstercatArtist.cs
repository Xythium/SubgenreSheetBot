using System;
using Newtonsoft.Json;

namespace Common.Monstercat;

public class MonstercatArtist
{
    public string Id { get; set; }

    [JsonProperty("URI")]
    public string Uri { get; set; }

    public string Name { get; set; }

    public bool Public { get; set; }

    public string Role { get; set; }
}