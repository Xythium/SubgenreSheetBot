using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.Monstercat;

public class MonstercatReleaseSummary
{
    public string ArtistsTitle { get; set; }

    public string CatalogId { get; set; }

    public string Id { get; set; }

    public List<string> Tags { get; set; }

    public string Title { get; set; }

    public string Type { get; set; }

    public DateTime ReleaseDate { get; set; }

    public string Version { get; set; }

    [JsonProperty("UPC")]
    public string Upc { get; set; }

    public string Description { get; set; }

    public bool Public { get; set; }

    public bool Approved { get; set; }
    
    
}