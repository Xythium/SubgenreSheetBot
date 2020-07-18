using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Commands
{
    [Group("Spotify"), Alias("s")]
    public partial class SpotifyModule : ModuleBase
    {
        private static SpotifyClient api;

        public SpotifyModule()
        {
            if (api == null)
            {
                var config = SpotifyClientConfig.CreateDefault()
                    .WithAuthenticator(new ClientCredentialsAuthenticator(File.ReadAllText("spotify_id"), File.ReadAllText("spotify_secret")));

                api = new SpotifyClient(config);
            }
        }

        [Command("tracks"), Alias("t"), Summary("Get all tracks from an album")]
        public async Task Tracks(
            [Remainder, Summary("Album ID to search for")]
            string albumId)
        {
            albumId = await GetIdFromUrl(albumId);

            if (string.IsNullOrWhiteSpace(albumId))
            {
                return;
            }

            var album = await GetAlbumOrCache(albumId);
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

                sb.AppendLine($"`{album.ReleaseDate},?,?,{string.Join(" & ", track.Artists.Select(a => a.Name))},{track.Name},{album.Label},{TimeSpan.FromMilliseconds(track.DurationMs):m':'ss},FALSE,{Math.Round(features.Tempo)},FALSE,{IntToKey(features.Key)} {IntToMode(features.Mode)}`");
            }

            await ReplyAsync(embed: embed.Build());
            await ReplyAsync(sb.ToString());
        }

        [Command("info"), Alias("i"), Summary("Get all tracks from an album")]
        public async Task Info(
            [Remainder, Summary("Album ID to search for")]
            string albumId)
        {
            albumId = await GetIdFromUrl(albumId);

            if (string.IsNullOrWhiteSpace(albumId))
            {
                return;
            }

            var album = await GetAlbumOrCache(albumId);
            var albumArtists = album.Artists;

            if (albumArtists.Count == 0)
            {
                await ReplyAsync("the artist count is 0");
                return;
            }

            var genres = string.Join(", ", album.Genres);

            if (string.IsNullOrWhiteSpace(genres))
            {
                if (album.Genres.Count > 0)
                {
                    await ReplyAsync($"asdadasdasdadasdadsda {MentionUtils.MentionUser(131768632354144256)}");
                }

                genres = "None";
            }

            var sb = new StringBuilder();
            var sb1 = new StringBuilder();

            //2009-09-22	House	Tech House | Progressive House	deadmau5	Lack of a Better Name	mau5trap	8:15	FALSE	128	FALSE	F min
            foreach (var track in album.Tracks.Items)
            {
                var features = await GetAudioFeaturesOrCache(track.Id);

                if (sb.Length > 900)
                {
                    sb1.AppendLine($"{track.TrackNumber}. {track.Name}");
                    sb1.AppendLine($"{TimeSpan.FromMilliseconds(track.DurationMs):m':'ss} - {Math.Round(features.Tempo)} {features.TimeSignature}/4 {IntToKey(features.Key)} {IntToMode(features.Mode)}");
                    sb1.AppendLine();
                }
                else
                {
                    sb.AppendLine($"{track.TrackNumber}. {track.Name}");
                    sb.AppendLine($"{TimeSpan.FromMilliseconds(track.DurationMs):m':'ss} - {Math.Round(features.Tempo)} {features.TimeSignature}/4 {IntToKey(features.Key)} {IntToMode(features.Mode)}");
                    sb.AppendLine();
                }
            }

            var embed = new EmbedBuilder().WithTitle($"{string.Join(" & ", albumArtists.Select(a => a.Name))} - {album.Name}")
                .WithThumbnailUrl(album.Images.OrderByDescending(i => i.Width)
                    .First()
                    .Url)
                .AddField("Release Date", album.ReleaseDate)
                .AddField("Type", string.IsNullOrWhiteSpace(album.AlbumType) ? "None" : album.AlbumType)
                .AddField("Genre", genres)
                .AddField("Popularity", album.Popularity)
                .AddField("Label", album.Label)
                .AddField("Tracklist", sb.ToString());
            if (sb1.Length > 0)
                embed = embed.AddField("Tracklist (cont.)", sb1.ToString());
            await ReplyAsync(embed: embed.Build());
        }

        [Command("label"), Alias("l"), Summary("Get all releases from a label")]
        public async Task Label(
            [Remainder, Summary("Label name to search for")]
            string labelName)
        {
            labelName = labelName.Replace("\"", "");
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Album, $"label:\"{labelName}\""));

            var albums = new List<SimpleAlbum>();
            var sb = new StringBuilder();

            await foreach (var album in api.Paginate(response.Albums, s => s.Albums))
            {
                albums.Add(album);
                if (albums.Count == 2000)
                    break;
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

            if (sb.Length > 1000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, "albums.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            }
            else
            {
                await ReplyAsync(sb.ToString());
            }
        }

        [Command("label"), Alias("l"), Summary("Get all releases from a label from a certain year")]
        public async Task Label(
            [Summary("Year to find releases for")] int year, [Remainder, Summary("Label name to search for")]
            string labelName)
        {
            labelName = labelName.Replace("\"", "");
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Album, $"label:\"{labelName}\" year:{year}"));

            var albums = new List<SimpleAlbum>();
            var sb = new StringBuilder();

            await foreach (var album in api.Paginate(response.Albums, s => s.Albums))
            {
                albums.Add(album);
                if (albums.Count == 2000)
                    break;
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

            if (sb.Length > 1000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                await Context.Channel.SendFileAsync(writer, "albums.txt", $"I found {albums.Count} albums which does not fit in a discord message");
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
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Artist | SearchRequest.Types.Track, $"label:\"{labelName}\""));

            var allArtists = new HashSet<FullArtist>(new FullArtistComparer());
            var testArtists = new HashSet<SimpleArtist>(new SimpleArtistComparer());

            await foreach (var artist in api.Paginate(response.Artists, s => s.Artists, new SimplePaginator()))
            {
                allArtists.Add(artist);
                if (allArtists.Count == 2000)
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
                    testArtists.Add(artist);
                }

                if (++count == 2000)
                {
                    await ReplyAsync("too many tracks");
                    break;
                }

                if (count % 250 == 0)
                {
                    message = await UpdateOrSend(message, $"{count} tracks");
                    await Task.Delay(100);
                }
            }

            var notFound = allArtists.Where(a => testArtists.FirstOrDefault(f => string.Equals(f.Name, a.Name, StringComparison.OrdinalIgnoreCase)) == null)
                .ToArray();

            if (message == null)
            {
                await ReplyAsync($"Checking {allArtists.Count} artists & {testArtists.Count} artists from every track");
            }
            else
            {
                await message.ModifyAsync(m => m.Content = $"Checking {allArtists.Count} artists & {testArtists.Count} artists from every track");
            }

            var sb = new StringBuilder();

            foreach (var artist in notFound)
            {
                response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"label:{labelName} \"{artist.Name}\""));

                if (response.Tracks.Items.Count < 1)
                {
                    sb.AppendLine($"`{artist.Name}` <https://open.spotify.com/artist/{artist.Id}>");
                }

                await Task.Delay(100);
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