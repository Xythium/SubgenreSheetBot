using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BeatportApi;
using BeatportApi.Beatsource;
using Common;
using Common.Beatport;
using Discord;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace SubgenreSheetBot.Commands
{
    public partial class BeatsourceModule
    {
        private Beatsource api;

        public BeatsourceModule()
        {
            api ??= new Beatsource();
            api.Login(File.ReadAllText("beatsource_user"), File.ReadAllText("beatsource_pass"))
                .GetAwaiter()
                .GetResult();
        }

        private static string FormatTrack(BeatsourceTrack track, bool includeArtist = false)
        {
            var featureList = new List<string>
            {
                TimeSpan.FromMilliseconds(track.LengthMs)
                    .ToString("m':'ss")
            };

            if (track.Bpm != null && track.Bpm != 0)
            {
                featureList.Add(track.Bpm.ToString());
            }

            if (track.Key != null)
            {
                featureList.Add(track.Key.Name);
            }

            if (track.Isrc != null)
            {
                featureList.Add(track.Isrc);
            }

            var name = track.Name;

            if (!string.IsNullOrWhiteSpace(track.MixName))
            {
                name = $"{track.Name} ({track.MixName})";
            }

            var sb = new StringBuilder();

            if (includeArtist)
            {
                sb.Append($"{track.Number}. {track.ArtistConcat} - {name} [{string.Join(", ", featureList)}]");
            }
            else
            {
                sb.Append($"{track.Number}. {name} [{string.Join(", ", featureList)}]");
            }

            return sb.ToString();
        }

        private Task<BeatsourceRelease> GetAlbum(int albumId)
        {
            using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
            return BeatportDbUtils.GetAlbumOrCache(api, session, albumId);
        }

        private Task<BeatsourceTrack[]> GetTracks(BeatsourceRelease album)
        {
            using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
            return album.GetTracksOrCache(api, session);
        }
    }
}