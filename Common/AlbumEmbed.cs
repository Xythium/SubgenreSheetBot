using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using BeatportApi.Beatport;
using Common.Beatport;
using Discord;
using Raven.Client;

namespace Common
{
    public class AlbumEmbed
    {
        public static (EmbedBuilder embed, MemoryStream tracklist) EmbedBuilder(GenericAlbum album)
        {
            var tracklist = new List<StringBuilder>
            {
                new StringBuilder()
            };

            foreach (var track in album.Tracks)
            {
                var line = FormatTrack(track);

                StringBuilder sb = null;

                for (var i = tracklist.Count - 1; i >= 0; i--)
                {
                    var builder = tracklist[i];
                    if (builder.Length + line.Length >= 1000)
                        continue;

                    sb = builder;
                    break;
                }

                if (sb == null)
                {
                    sb = new StringBuilder();
                    tracklist.Add(sb);
                }

                sb.AppendLine(line);
            }

            /*  var color = ColorUtils.MostCommonColor(release.Image.DynamicUri.Replace("{w}", "1400")
                  .Replace("{h}", "1400"));
  
              Log.Information($"embed color: {color} {color:X8}");*/
            var artist = string.Join(" × ", album.Artists);

            if (album.Artists.Count > 9 || artist.Length > 128)
                artist = "Various Artists";

            var embed = new EmbedBuilder().WithTitle($"{artist} - {album.Name}");

            if (!string.IsNullOrWhiteSpace(album.Url))
                embed = embed.WithUrl(album.Url);

            //.WithColor(color)

            if (!string.IsNullOrWhiteSpace(album.Image))
                embed = embed.WithThumbnailUrl(album.Image);

            if (!string.IsNullOrWhiteSpace(album.Label))
                embed = embed.AddField("Label", album.Label, true);

            var releaseDate = album.ReleaseDate;
            if (string.IsNullOrWhiteSpace(album.ReleaseDate))
                releaseDate = "(error)";
            embed = embed.AddField("Release Date", releaseDate, true);

            if (!string.IsNullOrWhiteSpace(album.CatalogNumber) && !string.IsNullOrWhiteSpace(album.Barcode) && album.CatalogNumber == album.Barcode)
            {
                embed = embed.AddField("Barcode/Catalog", album.CatalogNumber, true);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(album.CatalogNumber))
                    embed = embed.AddField("Catalog", album.CatalogNumber, true);

                if (!string.IsNullOrWhiteSpace(album.Barcode))
                    embed = embed.AddField("Barcode", album.Barcode, true);
            }

            MemoryStream ms = null;

            if (tracklist.Count > 3)
            {
                var sb = new StringBuilder();

                foreach (var builder in tracklist)
                    sb.Append(builder.ToString());
                ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
            }
            else
                foreach (var builder in tracklist)
                {
                    var name = "Tracklist";
                    if (tracklist.Count > 1)
                        name = $"Tracklist {tracklist.IndexOf(builder) + 1}";

                    var text = builder.ToString();
                    if (string.IsNullOrWhiteSpace(text))
                        text = "(empty)";

                    embed = embed.AddField(name, text);
                }

            if (album.FreeDownloads.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var download in album.FreeDownloads)
                    sb.AppendLine($"`{download.Start:yyyy-MM-dd} - {download.End:yyyy-MM-dd}`");
                
                embed = embed.AddField("Free Downloads", sb.ToString());
            }

            if (!string.IsNullOrWhiteSpace(album.Description))
                embed = embed.WithDescription(new string(album.Description.Take(150)
                    .ToArray()));

            return (embed, ms);
        }

        private static string FormatTrack(GenericTrack track, bool includeArtist = false)
        {
            var featureList = new List<string>
            {
                track.Duration.ToString("m':'ss")
            };

            if (!string.IsNullOrWhiteSpace(track.Bpm) && track.Bpm != "0")
            {
                featureList.Add(track.Bpm);
            }

            if (track.Key != null)
            {
                featureList.Add(track.Key);
            }

            if (track.Isrc != null)
            {
                featureList.Add(track.Isrc);
            }

            var name = track.Name;

            if (!string.IsNullOrWhiteSpace(track.MixName) && track.MixName != "Original Mix" && track.MixName != "Original")
            {
                name = $"{track.Name} ({track.MixName})";
            }

            var sb = new StringBuilder();

            if (includeArtist)
            {
                sb.Append($"{track.Number}. {string.Join(" × ", track.Artists)} - {name} [{string.Join(", ", featureList)}]");
            }
            else
            {
                sb.Append($"{track.Number}. {name} [{string.Join(", ", featureList)}]");
            }

            return sb.ToString();
        }
    }
}