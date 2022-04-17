using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BeatportApi;
using BeatportApi.Beatport;
using BeatportApi.Beatsource;
using Discord;
using Raven.Client;
using Serilog;

namespace Common.Beatport
{
    public static class BeatportUtils
    {
        public static IdFromUrlResult GetIdFromUrl(Uri uri)
        {
            if (!string.Equals(uri.Host, "www.beatport.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "api.beatport.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "www.beatsource.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "api.beatsource.com", StringComparison.OrdinalIgnoreCase))
            {
                if (!uri.Host.Contains("beatport.com") && !uri.Host.Contains("beatsource.com"))
                {
                    return new IdFromUrlResult
                    {
                        Error = $"ERROR: Host '{uri.Host}' is not beatport.com"
                    };
                }
                /*else
                    await Context.Message.ReplyAsync($"ERROR: wtf <{url}> '<{uri.Host}>'");*/ //bug???
            }

            var directories = uri.LocalPath.Split(new[]
            {
                "/"
            }, StringSplitOptions.RemoveEmptyEntries);

            var str = directories.Last();

            if (!int.TryParse(str, out var id))
            {
                return new IdFromUrlResult
                {
                    Error = $"ERROR: Id '{id}' is not numeric"
                };
            }

            return new IdFromUrlResult
            {
                Id = id
            };
        }

        public static IdFromUrlResult GetIdFromUrl(string url) { return GetIdFromUrl(new Uri(url)); }

        public static async Task<EmbedBuilder> EmbedBuilder(BeatportApi.Beatport.Beatport api, BeatportRelease release, IDocumentSession session)
        {
            var tracks = await BeatportDbUtils.GetTracksOrCache(api, session, release.TrackUrls);

            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            var sb3 = new StringBuilder();

            foreach (var track in tracks)
            {
                var line = FormatTrack(track);

                if (sb2.Length + line.Length >= 1023)
                {
                    sb3.AppendLine(line);
                }
                else if (sb1.Length + line.Length >= 1023)
                {
                    sb2.AppendLine(line);
                }
                else
                {
                    sb1.AppendLine(line);
                }
            }

            /*  var color = ColorUtils.MostCommonColor(release.Image.DynamicUri.Replace("{w}", "1400")
                  .Replace("{h}", "1400"));
  
              Log.Information($"embed color: {color} {color:X8}");*/
            var artist = release.ArtistConcat;

            if (release.Artists.Count > 9 || artist.Length > 128)
                artist = "Various Artists";

            var embed = new EmbedBuilder().WithTitle($"{artist} - {release.Name}")
                //.WithColor(color)
                .WithUrl($"https://www.beatport.com/release/{release.Slug}/{release.Id}")
                .WithThumbnailUrl(release.Image.DynamicUri.Replace("{w}", "1400")
                    .Replace("{h}", "1400"))
                .AddField("Release Date", release.NewReleaseDate.ToString("yyyy-MM-dd"), true)
                .AddField("UPC", release.Upc ?? "(none)", true)
                .AddField("Catalog", release.CatalogNumber ?? "(none)", true)
                .AddField("Tracklist", sb1.Length > 1 ? sb1.ToString() : "(empty)");

            if (sb2.Length > 0)
                embed = embed.AddField("Tracklist (cont.)", sb2.ToString());
            if (sb3.Length > 0)
                embed = embed.AddField("Tracklist (cont. again)", sb3.ToString());

            if (!string.IsNullOrWhiteSpace(release.Description))
                embed = embed.WithDescription(release.Description);
            return embed;
        }

        public static async Task<EmbedBuilder> EmbedBuilder(Beatsource api, BeatsourceRelease release, IDocumentSession session)
        {
            var tracks = await BeatportDbUtils.GetTracksOrCache(api, session, release.TrackUrls);

            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            var sb3 = new StringBuilder();

            foreach (var track in tracks)
            {
                var line = FormatTrack(track);

                if (sb2.Length + line.Length >= 1023)
                {
                    sb3.AppendLine(line);
                }
                else if (sb1.Length + line.Length >= 1023)
                {
                    sb2.AppendLine(line);
                }
                else
                {
                    sb1.AppendLine(line);
                }
            }

            var color = ColorUtils.MostCommonColor(release.Image.DynamicUri.Replace("{w}", "1400")
                .Replace("{h}", "1400"));

            Log.Information($"embed color: {color} {color:X8}");
            var artist = release.ArtistConcat;

            if (release.Artists.Count > 9 || artist.Length > 128)
                artist = "Various Artists";

            var embed = new EmbedBuilder().WithTitle($"{artist} - {release.Name}")
                //.WithColor(color)
                .WithUrl($"https://www.beatsource.com/release/{release.Slug}/{release.Id}")
                .WithThumbnailUrl(release.Image.DynamicUri.Replace("{w}", "1400")
                    .Replace("{h}", "1400"))
                .AddField("Release Date", release.NewReleaseDate.ToString("yyyy-MM-dd"), true)
                .AddField("UPC", release.Upc, true)
                .AddField("Catalog", release.CatalogNumber ?? "(none)", true)
                .AddField("Tracklist", sb1.ToString());

            if (!string.IsNullOrWhiteSpace(release.Description))
                embed = embed.WithDescription(release.Description);
            return embed;
        }

        public static string FormatTrack(BeatportTrack track, bool includeArtist = false)
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

            if (!string.IsNullOrWhiteSpace(track.MixName) && track.MixName != "Original Mix")
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

        public static string FormatTrack(BeatsourceTrack track, bool includeArtist = false)
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

            if (!string.IsNullOrWhiteSpace(track.MixName) && track.MixName != "Original Mix")
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