using System.Collections.Generic;
using Newtonsoft.Json;

namespace Common.AppleMusic.Api;

public class SongAttributes
{
    [JsonProperty("previews")]
    public List<Preview> Previews { get; set; }

    [JsonProperty("artwork")]
    public Artwork Artwork { get; set; }

    [JsonProperty("artistName")]
    public string ArtistName { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("discNumber")]
    public int DiscNumber { get; set; }

    [JsonProperty("genreNames")]
    public List<string> GenreNames { get; set; }

    [JsonProperty("hasTimeSyncedLyrics")]
    public bool HasTimeSyncedLyrics { get; set; }

    [JsonProperty("isMasteredForItunes")]
    public bool IsMasteredForItunes { get; set; }

    [JsonProperty("durationInMillis")]
    public int DurationInMillis { get; set; }

    [JsonProperty("releaseDate")]
    public string ReleaseDate { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("isrc")]
    public string Isrc { get; set; }

    [JsonProperty("audioTraits")]
    public List<string> AudioTraits { get; set; }

    [JsonProperty("hasLyrics")]
    public bool HasLyrics { get; set; }

    [JsonProperty("albumName")]
    public string AlbumName { get; set; }

    [JsonProperty("trackNumber")]
    public int TrackNumber { get; set; }

    [JsonProperty("audioLocale")]
    public string AudioLocale { get; set; }

    [JsonProperty("offers")]
    public List<Offer> Offers { get; set; }
}