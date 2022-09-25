using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Raven.Client.Linq;
using Raven.Client.Linq.Indexing;

namespace Common.Monstercat;

public class MonstercatFullRelease
{
    public List<MonstercatArtist> Artists { get; set; }

    public string ArtistsTitle { get; set; }

    public string Brand { get; set; }

    public string CatalogId { get; set; }

    public string Description { get; set; }

    public bool Downloadable { get; set; }

    public string GenrePrimary { get; set; }

    public string GenreSecondary { get; set; }

    [JsonProperty("MonstercatId")]
    public string Id { get; set; }

    public bool InEarlyAccess { get; set; }

    public List<MonstercatLink> Links { get; set; }

    public DateTime ReleaseDate { get; set; }

    public bool Streamable { get; set; }

    public string Title { get; set; }

    public string Type { get; set; }

    public string Version { get; set; }

    public string YoutubeUrl { get; set; }

    public List<string> Tags { get; set; }

    [JsonProperty("UPC")]
    public string Upc { get; set; }

    public bool Public { get; set; }

    public bool Approved { get; set; }

    public List<MonstercatTrack> Tracks { get; set; }

    public MonstercatFullRelease() { }

    public MonstercatFullRelease(MonstercatCatalogResponse catalog, MonstercatReleaseSummary summary)
    {
        Artists = catalog.Release.Artists;
        ArtistsTitle = catalog.Release.ArtistsTitle;
        Brand = catalog.Release.Brand;
        CatalogId = catalog.Release.CatalogId;
        Description = catalog.Release.Description;
        Downloadable = catalog.Release.Downloadable;
        GenrePrimary = catalog.Release.GenrePrimary;
        GenreSecondary = catalog.Release.GenreSecondary;
        Id = catalog.Release.Id.ToString();
        InEarlyAccess = catalog.Release.InEarlyAccess;
        Links = catalog.Release.Links;
        ReleaseDate = catalog.Release.ReleaseDate;
        Streamable = catalog.Release.Streamable;
        Title = catalog.Release.Title;
        Type = catalog.Release.Type;
        Version = catalog.Release.Version;
        YoutubeUrl = catalog.Release.YoutubeUrl;
        Tags = summary.Tags;
        Upc = summary.Upc;
        Public = summary.Public;
        Approved = summary.Approved;
        Tracks = catalog.Tracks;
    }
}