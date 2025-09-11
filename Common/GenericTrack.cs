using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BeatportApi.Beatsource;
using Common.AppleMusic.Api;
using Common.Beatport.Api;
using Common.Monstercat;
using Common.Spotify;
using SpotifyAPI.Web;

namespace Common;

public class GenericTrack
{
    public required TimeSpan Duration { get; set; }

    public required string? Bpm { get; set; }

    public required string? Key { get; set; }

    public required string? Isrc { get; set; }

    public required string Name { get; set; }

    public required string? MixName { get; set; }

    public required int Number { get; set; }

    public required List<string> Artists { get; set; }

    private static readonly CultureInfo cultureInfo = CultureInfo.GetCultureInfo("en-US");

    public static GenericTrack FromTrack(Song song)
    {
        return new GenericTrack
        {
            Duration = TimeSpan.FromMilliseconds(song.Attributes.DurationInMillis),
            Bpm = null,
            Key = null,
            Isrc = song.Attributes.Isrc,
            Name = song.Attributes.Name,
            MixName = null,
            Number = song.Attributes.TrackNumber,
            Artists = song.Relationships.Artists.Data.Select(a => a.Attributes.Name).ToList()
        };
    }

    public static GenericTrack FromTrack(BeatportTrack song)
    {
        return new GenericTrack
        {
            Duration = song.Length ?? TimeSpan.Zero,
            Bpm = song.Bpm?.ToString(),
            Key = song.Key?.Name,
            Isrc = song.Isrc,
            Name = song.Name,
            MixName = song.MixName,
            Number = song.Number.Value,
            Artists = song.Artists.Select(a => a.Name).ToList()
        };
    }

    public static GenericTrack FromTrack(BeatsourceTrack song)
    {
        return new GenericTrack
        {
            Duration = song.Length,
            Bpm = song.Bpm?.ToString(),
            Key = song.Key?.Name,
            Isrc = song.Isrc,
            Name = song.Name,
            MixName = song.MixName,
            Number = song.Number,
            Artists = song.Artists.Select(a => a.Name).ToList()
        };
    }

    public static GenericTrack FromTrack(FullTrack track, TrackAudioFeatures? features)
    {
        return new GenericTrack
        {
            Duration = TimeSpan.FromMilliseconds(track.DurationMs),
            Bpm = features != null ? SpotifyUtils.BpmToString(features) : null,
            Key = features != null ? $"{SpotifyUtils.IntToKey(features.Key)} {SpotifyUtils.IntToMode(features.Mode)}" : null,
            Isrc = track.ExternalIds.GetValueOrDefault("isrc"),
            Name = track.Name,
            MixName = null,
            Number = track.TrackNumber,
            Artists = track.Artists.Select(a => a.Name).ToList()
        };
    }

    public static GenericTrack FromTrack(MonstercatTrack track)
    {
        return new GenericTrack
        {
            Duration = TimeSpan.FromSeconds(track.Duration),
            Bpm = track.Bpm.ToString(cultureInfo),
            Key = null,
            Isrc = track.Isrc,
            Name = track.Title,
            MixName = track.Version,
            Number = track.TrackNumber,
            Artists = track.Artists.Select(a => a.Name).ToList()
        };
    }
}