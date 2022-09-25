using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BeatportApi;
using BeatportApi.Beatport;
using Common;
using Common.Beatport;
using Discord;
using Discord.Commands;
using Raven.Client;

namespace SubgenreSheetBot.Commands
{
    [Group("Beatport"), Alias("b", "bp")]
    public partial class BeatportModule : ModuleBase
    {
        [Command("tracks"), Alias("t"), Summary("Get all tracks from an album")]
        public async Task Tracks([Remainder, Summary("Album ID to search for")] string text)
        {
            var idResult = BeatportUtils.GetIdFromUrl(text);

            if (!string.IsNullOrWhiteSpace(idResult.Error))
            {
                await Context.Message.ReplyAsync($"{idResult.Error}");
                return;
            }

            var album = await GetAlbum(idResult.Id);
            var albumArtists = album.Artists;

            if (albumArtists.Count == 0)
            {
                await Context.Message.ReplyAsync("the artist count is 0");
                return;
            }

            var embed = new EmbedBuilder().WithTitle($"{album.ArtistConcat} - {album.Name}")
                .WithThumbnailUrl(album.Image.DynamicUri.Replace("{w}", "1400")
                    .Replace("{h}", "1400"));
            var sb = new StringBuilder();

            var tracks = await GetTracks(album);

            if (!tracks.Any())
            {
                await Context.Message.ReplyAsync("no tracks");
            }

            //2009-09-22	House	Tech House | Progressive House	deadmau5	Lack of a Better Name	mau5trap	8:15	FALSE	128	FALSE	F min
            foreach (var track in tracks)
            {
                sb.AppendLine($"`{album.NewReleaseDate},?,?,{track.ArtistConcat},{track.Name},{album.Label.Name},{TimeSpan.FromMilliseconds(track.LengthMs):m':'ss}`");
            }

            await Context.Message.ReplyAsync(embed: embed.Build());
            await Context.Message.ReplyAsync(sb.ToString());
        }

        [Command("album"), Alias("a", "release"), Summary("Get all tracks from an album")]
        public async Task Album([Remainder, Summary("Album ID to search for")] string text)
        {
            var idResult = BeatportUtils.GetIdFromUrl(text);

            if (!string.IsNullOrWhiteSpace(idResult.Error))
            {
                await Context.Message.ReplyAsync($"{idResult.Error}");
                return;
            }

            var album = await GetAlbum(idResult.Id);

            if (album is null)
            {
                await Context.Message.ReplyAsync($"The album could not be loaded");
                return;
            }

            var tracks = await GetTracks(album);

            if (tracks.Length < 1)
            {
                //throw new Exception("this release has no tracks, maybe because mark fucked up");
            }

            using var session = SubgenreSheetBot.BeatportStore.OpenSession();

            var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(album, tracks.ToList()));

            if (file != null)
            {
                await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
                file.Close();
            }
            else
            {
                await Context.Message.ReplyAsync(embed: embed.Build());
            }
        }

        [Command("isrc"), Alias("i"), Summary("Search by ISRC")]
        public async Task Isrc([Remainder, Summary("ISRC to search for")] string isrc)
        {
            if (string.IsNullOrWhiteSpace(isrc))
            {
                return;
            }

            using var session = SubgenreSheetBot.BeatportStore.OpenSession();
            var results = session.Query<BeatportTrack>("TrackIsrc")
                .Search(t => t.Isrc, isrc)
                //.Where(t => t.Isrc == isrc)
                .ToArray();

            if (results.Length == 0)
            {
                await Context.Message.ReplyAsync($"found no tracks for isrc {isrc}");
                return;
            }

            var genres = new List<string>();

            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            var sb3 = new StringBuilder();

            //2009-09-22	House	Tech House | Progressive House	deadmau5	Lack of a Better Name	mau5trap	8:15	FALSE	128	FALSE	F min
            foreach (var track in results)
            {
                var line = $"{track.ArtistConcat} - {track.Name} <https://www.beatport.com/track/{track.Slug}/{track.Id}>";

                if (track.Subgenre != null)
                {
                    if (!genres.Contains(track.Subgenre.Name))
                        genres.Add(track.Subgenre.Name);
                }
                else
                {
                    if (!genres.Contains(track.Genre.Name))
                        genres.Add(track.Genre.Name);
                }

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

            var embed = new EmbedBuilder().WithTitle($"{isrc}")
                    .WithThumbnailUrl(results[new Random().Next(0, results.Length)]
                        .Release.Image.DynamicUri.Replace("{w}", "1400")
                        .Replace("{h}", "1400"))
                /*.AddField("Release Date", album.NewReleaseDate.ToString("yyyy-MM-dd"), true)
                .AddField("Type", album.Type?.Name ?? "None", true)*/;

            embed = embed /*.AddField("Label", album.Label.Name, true)*/
                .AddField("Tracklist", sb1.ToString());

            if (sb2.Length > 0)
                embed = embed.AddField("Tracklist (cont.)", sb2.ToString());
            if (sb3.Length > 0)
                embed = embed.AddField("Tracklist (cont. again)", sb3.ToString());

            await Context.Message.ReplyAsync(embed: embed.Build());
        }

        [Command("label"), Summary("Get all releases from a label")]
        public async Task Label([Remainder, Summary("Label name to search for")] string labelName)
        {
            var idResult = BeatportUtils.GetIdFromUrl(labelName);

            if (!string.IsNullOrWhiteSpace(idResult.Error))
            {
                await Context.Message.ReplyAsync(idResult.Error);
                return;
            }

            var albums = await GetAlbums(idResult.Id);

            var sb = new StringBuilder();

            foreach (var album in albums.OrderByDescending(a => a.NewReleaseDate))
            {
                var line = $"{album.ArtistConcat} - {album.Name} ({album.CatalogNumber} {album.NewReleaseDate:yyyy-MM-dd})";
                sb.AppendLine(line);
            }

            if (sb.Length < 1)
            {
                await Context.Message.ReplyAsync("pissed my pant");
                return;
            }

            //  await Context.Message.ReplyAsync($"{playlist.Uri}");
            if (sb.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            }
            else
            {
                await Context.Message.ReplyAsync(sb.ToString());
            }
        }

        [Command("labelcached"), Alias("labelc", "lc"), Summary("Get all releases from a label")]
        public async Task LabelCached([Remainder, Summary("Label name to search for")] string labelName)
        {
            using var session = SubgenreSheetBot.BeatportStore.OpenSession();

            var albums = new List<BeatportRelease>();

            var query = session.Advanced.Stream(session.Query<BeatportRelease>("ReleaseByLabel"));

            while (query.MoveNext())
            {
                if (query.Current is null)
                    throw new Exception();

                var album = query.Current.Document;
                if (!album.Label.Name.StartsWith(labelName, StringComparison.OrdinalIgnoreCase))
                    continue;

                albums.Add(album);
            }

            var sb = new StringBuilder();

            foreach (var album in albums.OrderByDescending(a => a.NewReleaseDate))
            {
                var line = $"{album.ArtistConcat} - {album.Name} ({album.CatalogNumber} {album.NewReleaseDate:yyyy-MM-dd})";
                sb.AppendLine(line);
            }

            if (sb.Length < 1)
            {
                await Context.Message.ReplyAsync("pissed my pant");
                return;
            }

            //  await Context.Message.ReplyAsync($"{playlist.Uri}");
            if (sb.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            }
            else
            {
                await Context.Message.ReplyAsync(sb.ToString());
            }
        }
    }
}