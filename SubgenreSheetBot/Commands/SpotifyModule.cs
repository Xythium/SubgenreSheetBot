using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Commands
{
    [Group("Spotify"), Alias("s")]
    public class SpotifyModule : ModuleBase
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

        private async Task<string> GetIdFromUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var locals = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (locals.Length == 2)
                {
                    if (locals[0] == "album")
                    {
                        return locals[1];
                    }

                    await ReplyAsync($"Url has to link to an album");
                    return "";
                }

                await ReplyAsync($"{uri} is not a valid url");
                return "";
            }

            return url;
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

            var album = await api.Albums.Get(albumId);
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

            var album = await api.Albums.Get(albumId);
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
                var features = await api.Tracks.GetAudioFeatures(track.Id);

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
        public async Task Label([Summary("Year to find releases for")]int year,
            [Remainder, Summary("Label name to search for")]
            string labelName)
        {
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

        private static string IntToKey(int key)
        {
            return key switch
            {
                0 => "C",
                1 => "C#",
                2 => "D",
                3 => "D#",
                4 => "E",
                5 => "F",
                6 => "F#",
                7 => "G",
                8 => "G#",
                9 => "A",
                10 => "A#",
                11 => "B",
                _ => "?"
            };
        }

        private static string IntToMode(int mode)
        {
            return mode switch
            {
                0 => "min",
                1 => "maj",
                _ => "?"
            };
        }
    }
}