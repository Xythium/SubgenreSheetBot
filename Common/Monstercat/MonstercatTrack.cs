using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.Monstercat;

public class MonstercatTrack
{
    public string ArtistsTitle { get; set; }

    public string Brand { get; set; }

    public int BrandId { get; set; }

    [JsonProperty("BPM")]
    public double Bpm { get; set; }

    public bool CreatorFriendly { get; set; }

    public DateTime DebutDate { get; set; }

    public bool Downloadable { get; set; }

    public int Duration { get; set; }

    public bool Explicit { get; set; }

    public string GenrePrimary { get; set; }

    public string GenreSecondary { get; set; }

    public string Id { get; set; }

    public bool InEarlyAccess { get; set; }

    [JsonProperty("ISRC")]
    public string Isrc { get; set; }

    public bool Streamable { get; set; }

    public string Title { get; set; }

    public int TrackNumber { get; set; }

    public List<string> Tags { get; set; }

    public string Version { get; set; }

    public MonstercatReleaseSummary Release { get; set; }

    public List<MonstercatArtist> Artists { get; set; }

    public int PlaylistSort { get; set; }
}