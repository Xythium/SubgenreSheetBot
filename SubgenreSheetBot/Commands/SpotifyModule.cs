using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Serilog;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Commands
{
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

        [Command("spotify"), Alias("s")]
        public async Task Spotify([Remainder] string search)
        {
            var album = await api.Albums.Get(search);
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