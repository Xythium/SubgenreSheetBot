using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BeatportApi;
using BeatportApi.Beatport;
using Common;
using Common.Beatport;
using Discord;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace SubgenreSheetBot.Commands
{
    public partial class BeatportModule
    {
        private static Beatport api;

        public BeatportModule() { api ??= new Beatport(File.ReadAllText("beatport_token")); }

        private static string FormatTrack(BeatportTrack track, bool includeArtist = false)
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

        private static Task<BeatportRelease?> GetAlbum(int albumId)
        {
            using var session = SubgenreSheetBot.BeatportStore.OpenSession();
            return BeatportDbUtils.GetAlbumOrCache(api, session, albumId);
        }

        private static Task<BeatportTrack[]> GetTracks(BeatportRelease album)
        {
            using var session = SubgenreSheetBot.BeatportStore.OpenSession();
            return album.GetTracksOrCache(api, session);
        }

        private static async Task<List<BeatportRelease>> GetAlbums(int labelId)
        {
            var releases = new List<BeatportRelease>();
            var response = await api.GetReleasesByLabelId(labelId, 200);
            return await GetAlbums(labelId, releases, response);
        }

        private static async Task<List<BeatportRelease>> GetAlbums(int labelId, List<BeatportRelease> releases, BeatportResponse<BeatportRelease> response)
        {
            if (response.Results == null || response.Results.Count < 1)
                return releases;

            await Task.WhenAll(response.Results.Select(async release =>
            {
                using var session = SubgenreSheetBot.BeatportStore.OpenSession();
                var realRelease = await BeatportDbUtils.GetAlbumOrCache(api, session, release.Id);
                if (realRelease == null)
                    return;

                if (realRelease.TrackUrls == null || realRelease.TrackUrls.Length < 1)
                {
                    //Log($"ERROR {release.Id} {release.Name}: no track urls");
                    return;
                }

                session.SaveChanges();
                releases.Add(realRelease);
            }));

            if (response.Next != null)
            {
                var url = new Uri($"https://{response.Next}");
                var query = HttpUtility.ParseQueryString(url.Query);
                var page = query.Get("page");

                if (!int.TryParse(page, out var realPage))
                {
                    //Log($"ERROR: no page query {labelReleases.Next}");
                }

                response = await api.GetReleasesByLabelId(labelId, 200, realPage);
                return await GetAlbums(labelId, releases, response);
            }

            return releases;
        }
    }
}