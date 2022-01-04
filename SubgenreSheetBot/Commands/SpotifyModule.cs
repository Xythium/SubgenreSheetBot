using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.Spotify;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Commands
{
    [Group("Spotify"), Alias("s")]
    public partial class SpotifyModule : ModuleBase
    {
        [Command("tracks"), Alias("t"), Summary("Get all tracks from an album")]
        public async Task Tracks([Remainder, Summary("Album ID to search for")] string url)
        {
            var (trackId, albumId) = GetIdFromUrl(url);

            if (!string.IsNullOrWhiteSpace(trackId))
                throw new ArgumentException("Track urls are not supported");

            if (string.IsNullOrWhiteSpace(albumId))
                throw new ArgumentException("No album ID found");

            var album = (await GetAlbumOrCache(albumId)).Album;
            var albumArtists = album.Artists;

            if (albumArtists.Count == 0)
            {
                await ReplyAsync("the artist count is 0");
                return;
            }

            var embed = new EmbedBuilder().WithTitle($"{string.Join(" & ", albumArtists.Select(a => a.Name))} - {album.Name}")
                .WithThumbnailUrl(album.Images.OrderByDescending(i => i.Width)
                    .First()
                    .Url);
            var sb = new StringBuilder();

            //2009-09-22	House	Tech House | Progressive House	deadmau5	Lack of a Better Name	mau5trap	8:15	FALSE	128	FALSE	F min
            foreach (var track in album.Tracks.Items)
            {
                var features = await api.Tracks.GetAudioFeatures(track.Id);

                foreach (var artist in track.Artists)
                {
                    if (!string.Equals(artist.Type, "artist", StringComparison.OrdinalIgnoreCase))
                    {
                        await ReplyAsync($"artist {artist.Name} is a {artist.Type} but Mark does not know about this existing");
                    }
                }

                sb.AppendLine($"`{album.ReleaseDate},?,?,{string.Join(" & ", track.Artists.Select(a => a.Name))},{track.Name},{album.Label},{TimeSpan.FromMilliseconds(track.DurationMs):m':'ss},FALSE,{Math.Round(features.Tempo)},FALSE,{SpotifyUtils.IntToKey(features.Key)} {SpotifyUtils.IntToMode(features.Mode)}`");
            }

            await ReplyAsync(embed: embed.Build());
            await ReplyAsync(sb.ToString());
        }

        [Command("info"), Alias("i"), Summary("Get all tracks from an album")]
        public async Task Info([Remainder, Summary("Album ID to search for")] string url)
        {
            var (trackId, albumId) = GetIdFromUrl(url);

            if (!string.IsNullOrWhiteSpace(trackId))
                throw new ArgumentException("Track urls are not supported");

            if (string.IsNullOrWhiteSpace(albumId))
                throw new ArgumentException("No album ID found");

            var album = (await GetAlbumOrCache(albumId)).Album;

            var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(album));

            if (file != null)
            {
                await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
                file.Close();
            }
            else
            {
                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("label"), Alias("l"), Summary("Get all releases from a label")]
        public async Task Label([Remainder, Summary("Label name to search for")] string labelName)
        {
            labelName = labelName.Replace("\"", "");
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Album, $"label:\"{labelName}\""));

            var albums = new List<FullAlbum>();
            var sb = new StringBuilder();

            await foreach (var album in api.Paginate(response.Albums, s => s.Albums))
            {
                var cacheResult = await GetAlbumOrCache(album.Id);

                if (labelName.Contains("mau5trap", StringComparison.OrdinalIgnoreCase) && !labelName.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase) && cacheResult.Album.Label.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase))
                    continue;

                albums.Add(cacheResult.Album);
                if (albums.Count == 2000)
                    break;
            }

            /*var playlist =await CreateOrUpdatePlaylist(labelName, albums.OrderByDescending(a => a.ReleaseDate)
                .ToArray());*/

            foreach (var album in albums.OrderByDescending(a => a.ReleaseDate))
            {
                var line = $"{string.Join(" & ", album.Artists.Select(a => $"{a.Name}{(a.Type != "artist" ? $" ({a.Type})" : "")}"))} - {album.Name} ({album.ReleaseDate})";
                sb.AppendLine(line);
            }

            if (sb.Length < 1)
            {
                await ReplyAsync("pissed my pant");
                return;
            }

            //  await ReplyAsync($"{playlist.Uri}");
            if (sb.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            }
            else
            {
                await ReplyAsync(sb.ToString());
            }
        }

        [Command("artist"), Alias("a"), Summary("Get all releases from an artist")]
        public async Task Artist([Remainder, Summary("Artist to search for")] string artistName)
        {
            artistName = artistName.Replace("\"", "");

            var paging = await api.Artists.GetAlbums(artistName);

            var albums = await api.PaginateAll(paging);

            var sb = new StringBuilder();

            /*var playlist =await CreateOrUpdatePlaylist(labelName, albums.OrderByDescending(a => a.ReleaseDate)
                .ToArray());*/

            foreach (var album in albums.OrderBy(a => a.Name)
                         .ThenByDescending(a => a.ReleaseDate)
                         .ThenBy(a => string.Join(" & ", a.Artists.Select(_ => _.Name))))
            {
                var line = $"{string.Join(" & ", album.Artists.Select(a => $"{a.Name}{(a.Type != "artist" ? $" ({a.Type})" : "")}"))} - {album.Name} ({album.ReleaseDate}) https://open.spotify.com/album/{album.Id}";
                sb.AppendLine(line);
            }

            if (sb.Length < 1)
            {
                await ReplyAsync("pissed my pant");
                return;
            }

            //  await ReplyAsync($"{playlist.Uri}");
            if (sb.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, $"{artistName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            }
            else
            {
                await ReplyAsync(sb.ToString());
            }
        }

        [Command("label"), Alias("l"), Summary("Get all releases from a label from a certain year")]
        public async Task Label([Summary("Year to find releases for")] int year, [Remainder, Summary("Label name to search for")] string labelName)
        {
            labelName = labelName.Replace("\"", "");
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Album, $"label:\"{labelName}\" year:{year}"));

            var albums = new List<FullAlbum>();
            var sb = new StringBuilder();

            await foreach (var album in api.Paginate(response.Albums, s => s.Albums))
            {
                var cacheResult = await GetAlbumOrCache(album.Id);

                if (labelName.Contains("mau5trap", StringComparison.OrdinalIgnoreCase) && !labelName.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase) && cacheResult.Album.Label.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase))
                    continue;

                albums.Add(cacheResult.Album);

                if (albums.Count == 2000)
                {
                    await ReplyAsync("reached 2000 album limit");
                    break;
                }
            }

            if (albums.Count < 1)
            {
                await ReplyAsync("no albums found");
                return;
            }

            foreach (var album in albums.OrderByDescending(a => a.ReleaseDate))
            {
                var line = $"{string.Join(" & ", album.Artists.Select(a => $"{a.Name}{(a.Type != "artist" ? $" ({a.Type})" : "")}"))} - {album.Name} ({album.ReleaseDate})";
                sb.AppendLine(line);
            }

            if (sb.Length < 1)
            {
                await ReplyAsync("pissed my pant");
                return;
            }

            if (sb.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, $"{labelName}-{year}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            }
            else
            {
                await ReplyAsync(sb.ToString());
            }
        }

        [Command("peep")]
        public async Task Peep([Remainder] string labelName)
        {
            labelName = labelName.Replace("\"", "");
            var notFound = await SpotifyUtils.Peep(api, labelName);

            var sb = new StringBuilder();

            foreach (var artist in notFound)
            {
                sb.AppendLine($"`{artist.Name}` <https://open.spotify.com/artist/{artist.Id}>");
            }

            if (sb.Length > 0)
            {
                await SendOrAttachment(sb.ToString());
            }
            else
            {
                await ReplyAsync($"couldnt find anything for {labelName}");
            }
        }

        [Command("peepn"), Alias("pn")]
        public async Task PeepNoDoubleCheck([Remainder] string labelName)
        {
            labelName = labelName.Replace("\"", "");
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Artist | SearchRequest.Types.Track, $"label:\"{labelName}\""));

            var searchedArtists = new HashSet<FullArtist>(new FullArtistComparer());
            var trackArtists = new HashSet<SimpleArtist>(new SimpleArtistComparer());

            await foreach (var artist in api.Paginate(response.Artists, s => s.Artists, new CachingPaginator()))
            {
                searchedArtists.Add(artist);
                if (searchedArtists.Count == 2000)
                    break;
            }

            IUserMessage message = null;
            var count = 0;

            await foreach (var track in api.Paginate(response.Tracks, s => s.Tracks))
            {
                var artists = track.Artists.Select(a => a)
                    .ToArray();

                foreach (var artist in artists)
                {
                    trackArtists.Add(artist);
                }

                if (++count == 2000)
                {
                    await ReplyAsync("too many tracks");
                    break;
                }

                if (count % 250 == 0)
                {
                    message = await UpdateOrSend(message, $"{count} tracks");
                }
            }

            if (message == null)
            {
                await ReplyAsync($"Checking {searchedArtists.Count} artists & {trackArtists.Count} artists from every track");
            }
            else
            {
                await message.ModifyAsync(m => m.Content = $"Checking {searchedArtists.Count} artists & {trackArtists.Count} artists from every track");
            }

            var notFound = searchedArtists.Where(searchedArtist => trackArtists.FirstOrDefault(trackArtist => string.Equals(trackArtist.Name, searchedArtist.Name, StringComparison.OrdinalIgnoreCase)) == null)
                .ToArray();

            var sb = new StringBuilder();

            foreach (var artist in notFound)
            {
                sb.AppendLine($"`{artist.Name}` <https://open.spotify.com/artist/{artist.Id}>");
            }

            if (sb.Length > 0)
            {
                await SendOrAttachment(sb.ToString());
            }
            else
            {
                await ReplyAsync($"couldnt find anything for {labelName}");
            }
        }

        /*[Command("isrc")]
        public async Task Isrc(
            [Remainder, Summary("Label name to search for")]
            string labelName)
        {
            var sb = new StringBuilder();

            IUserMessage message = null;

            try
            {
                for (int i = 423 - 1; i >= 0; i--)
                {
                    var isrc = $"GBTDG07{i.ToString().PadLeft(5, '0')}";
                    if (i % 50 == 0)
                        message = await UpdateOrSend(message, $"{isrc} looking");

                    var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"isrc:\"{isrc}\""));

                    await foreach (var track in api.Paginate(response.Tracks, s => s.Tracks))
                    {
                        //message = await UpdateOrSend(message, $"GBTDG13{i.ToString().PadLeft(5, '0')} found");
                        sb.AppendLine($"{string.Join(" & ", track.Artists.Select(a => a.Name.ToUpper()))},{track.Name.ToUpper()},{isrc},,,{TimeSpan.FromMilliseconds(track.DurationMs)}");
                    }

                    await Task.Delay(100);
                }
            }
            catch
            {
            }
            finally
            {
                await SendOrAttachment(sb.ToString());
            }
        }*/
    }
}