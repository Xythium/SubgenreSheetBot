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
using Discord;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace SubgenreSheetBot.Commands
{
    public partial class BeatportModule
    {
        private static Beatport api;

        public BeatportModule()
        {
            if (api == null)
            {
                api = new Beatport(File.ReadAllText("beatport_token"));
            }
        }

        private async Task<string> GetIdFromUrl(string url)
        {
            var uri = new Uri(url);

            if (!string.Equals(uri.Host, "www.beatport.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "api.beatport.com", StringComparison.OrdinalIgnoreCase))
            {
                if (!uri.Host.Contains("beatport.com"))
                    await Context.Message.ReplyAsync($"ERROR: Host '{uri.Host}' is not beatport.com");
                /*else
                    await Context.Message.ReplyAsync($"ERROR: wtf <{url}> '<{uri.Host}>'");*/ //bug???
            }

            var directories = uri.LocalPath.Split(new[]
            {
                "/"
            }, StringSplitOptions.RemoveEmptyEntries);
            return directories.Last();
        }

        private async Task<BeatportRelease> GetAlbumOrCache(string albumId)
        {
            using var session = SubgenreSheetBot.BeatportStore.OpenSession();

            var t = session.Load<BeatportRelease>($"BeatportReleases/{albumId}");

            if (t == null)
            {
                var id = int.Parse(albumId);
                t = await api.GetReleaseById(id);

                if (t == null)
                {
                    throw new Exception("oh nouuu :( hshit");
                }

                await GetTracksOrCache(t.TrackUrls);
            }

            // outside 'if' to force document changes
            session.Store(t);
            session.SaveChanges();

            return t;
        }

        private async Task<BeatportTrack[]> GetTracksOrCache(string[] trackUrls)
        {
            using var session = SubgenreSheetBot.BeatportStore.OpenSession();

            var tracks = new List<BeatportTrack>();

            foreach (var url in trackUrls)
            {
                var id = await GetIdFromUrl(url);
                var t = session.Load<BeatportTrack>($"BeatportTracks/{id}");

                if (t == null)
                {
                    t = await api.GetTrackByTrackId(id);

                    if (t == null)
                    {
                        throw new Exception("oh nwwoo :( why");
                    }

                    session.Store(t);
                }

                tracks.Add(t);
            }

            session.SaveChanges();

            return tracks.OrderBy(t => t.Number)
                .ThenBy(t => t.Isrc)
                .ToArray();
        }

        private async Task<IUserMessage> UpdateOrSend(IUserMessage message, string str)
        {
            if (message == null)
            {
                return message = await ReplyAsync(str);
            }

            await message.ModifyAsync(m => m.Content = str);
            return message;
        }

        private async Task SendOrAttachment(string str)
        {
            if (str.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(str));
                await Context.Channel.SendFileAsync(writer, "content.txt", $"Message too long");
            }
            else
            {
                await ReplyAsync(str);
            }
        }

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
    }
}