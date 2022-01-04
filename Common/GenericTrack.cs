using System;
using System.Collections.Generic;
using System.Linq;
using Common.AppleMusic;
using Serilog;
using SpotifyAPI.Web;

namespace Common
{
    public class GenericTrack
    {
        public TimeSpan Duration { get; set; }

        public int? Bpm { get; set; }

        public string Key { get; set; }

        public string Isrc { get; set; }

        public string Name { get; set; }

        public string MixName { get; set; }

        public int Number { get; set; }

        public List<string> Artists { get; set; }

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
                Artists = song.Relationships.Artists.Data.Select(a => a.Attributes.Name)
                    .ToList()
            };
        }

        public static GenericTrack FromTrack(SimpleTrack track)
        {
            Log.Information("ddd");
            return new GenericTrack
            {
                Duration = TimeSpan.FromMilliseconds(track.DurationMs),
                Bpm = null,
                Key = null,
                Isrc = null,
                Name = track.Name,
                MixName = null,
                Number = track.TrackNumber,
                Artists = track.Artists.Select(a => a.Name)
                    .ToList()
            };
        }
    }
}