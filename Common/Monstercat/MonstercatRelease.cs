using System;
using System.Collections.Generic;

namespace Common.Monstercat;

public class MonstercatRelease
{
    public List<MonstercatArtist> Artists { get; set; }

    public string ArtistsTitle { get; set; }

    public string Brand { get; set; }

    public string CatalogId { get; set; }

    public string Description { get; set; }

    public bool Downloadable { get; set; }

    public string GenrePrimary { get; set; }

    public string GenreSecondary { get; set; }

    public string Id { get; set; }

    public bool InEarlyAccess { get; set; }

    public List<MonstercatLink> Links { get; set; }

    public DateTime ReleaseDate { get; set; }

    public bool Streamable { get; set; }

    public string Title { get; set; }

    public string Type { get; set; }

    public string Version { get; set; }

    public string YoutubeUrl { get; set; }
}