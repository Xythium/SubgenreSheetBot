using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using FuzzySharp.PreProcess;
using MusicTools;

namespace SubgenreSheetBot.Commands
{
    public partial class SpreadsheetModule : ModuleBase
    {
        [Command("trackexact"), Alias("te"), Summary("Search for a track on the sheet")]
        public async Task Track(
            [Remainder, Summary("Track to search for")]
            string search)
        {
            await RevalidateCache();

            var split = search.Split(new[]
            {
                " - "
            }, StringSplitOptions.RemoveEmptyEntries);

            if (split.Length != 2)
            {
                await ReplyAsync($"cannot parse `{search}` into `Artist - Title`");
                return;
            }

            var artist = split[0];
            var tracksByArtist = GetAllTracksByArtistExact(artist);

            if (tracksByArtist.Count == 0)
            {
                await ReplyAsync($"no tracks found by artist `{artist}`");
                return;
            }

            var title = split[1];
            var tracks = GetTracksByTitleExact(tracksByArtist, title);

            if (tracks.Count == 0)
            {
                await ReplyAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
                return;
            }

            foreach (var track in tracks)
            {
                await SendTrackEmbed(track);
            }
        }

        [Command("trackinfoexact"), Alias("tie"), Summary("Search for a track on the sheet")]
        public async Task TrackInfo(
            [Remainder, Summary("Track to search for")]
            string search)
        {
            await RevalidateCache();

            var split = search.Split(new[]
            {
                " - "
            }, StringSplitOptions.RemoveEmptyEntries);

            if (split.Length != 2)
            {
                await ReplyAsync($"cannot parse `{search}` into `Artist - Title` [{string.Join(", ", split)}]");
                return;
            }

            var artist = split[0];
            var tracksByArtist = GetAllTracksByArtistExact(artist);

            if (tracksByArtist.Count == 0)
            {
                await ReplyAsync($"no tracks found by artist `{artist}`");
                return;
            }

            var title = split[1];
            var tracks = GetTracksByTitleExact(tracksByArtist, title);

            if (tracks.Count == 0)
            {
                await ReplyAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
                return;
            }

            foreach (var track in tracks)
            {
                var info = TrackParser.GetTrackInfo(track.Artists, track.Title, null, null, track.Date);
                await SendTrackInfoEmbed(info);
            }
        }

        [Command("trackinfoforce"), Alias("tif"), Summary("Get information about a track")]
        public async Task TrackInfoForce(
            [Remainder, Summary("Track to search for")]
            string search)
        {
            var split = search.Split(new[]
            {
                " - "
            }, StringSplitOptions.RemoveEmptyEntries);

            if (split.Length < 2)
            {
                await ReplyAsync($"cannot parse `{search}` into `Artist - Title` [{string.Join(", ", split)}]");
                return;
            }

            var index = search.IndexOf(" - ", StringComparison.Ordinal);
            var artist = search.Substring(0, index);
            var title = search.Substring(3 + index);
            var info = TrackParser.GetTrackInfo(artist, title, null, null, DateTime.UtcNow);
            await SendTrackInfoEmbed(info);
        }

        [Command("artist"), Alias("a"), Summary("Returns a list of tracks by a given artist")]
        public async Task Artist(
            [Remainder, Summary("Artist to search for")]
            string artist = "")
        {
            await RevalidateCache();

            var tracksByArtist = GetAllTracksByArtistFuzzy(artist);

            if (tracksByArtist.Count == 0)
            {
                await ReplyAsync($"no tracks found by artist `{artist}`");
                return;
            }

            await SendTrackList(artist, tracksByArtist);
        }

        [Command("artistdebug"), Alias("ad"), Summary("Returns a list of up to 15 artists most similar to the given input")]
        public async Task ArtistDebug(
            [Remainder, Summary("Artist to search for")]
            string artist)
        {
            await RevalidateCache();

            var artists = entries.SelectMany(e => e.ArtistsList)
                .Distinct()
                .Select(a => (a, fuzzyFunc(artist, a, PreprocessMode.Full)))
                .OrderByDescending(a => a.Item2)
                .ThenBy(a => a.a)
                .Take(15)
                .ToArray();
            var sb = new StringBuilder($"{artists.Length} most similar artists (using {fuzzyFunc.Method.Name})\r\n");

            for (var i = 0; i < artists.Length; i++)
            {
                var track = artists[i];
                sb.AppendLine($"{Array.IndexOf(artists, track) + 1}. `{track.a}` {track.Item2}% similar");
            }

            await ReplyAsync(sb.ToString());
        }

        [Command("genre"), Alias("g"), Summary("Returns a list of up to 8 tracks of a given genre")]
        public async Task Genre(
            [Remainder, Summary("Genre to search for")]
            string genre)
        {
            await RevalidateCache();

            var genres = entries.Select(e => e.Genre)
                .Distinct()
                .ToArray();
            var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

            if (test == null)
            {
                await ReplyAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
                return;
            }

            var tracks = entries.Where(e => e.Sheet != "Genreless" && string.Equals(e.Genre, test, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToList();

            if (tracks.Count == 0)
            {
                await ReplyAsync($"No tracks with genre `{test}` found");
                return;
            }

            await SendTrackList(test, tracks, false);
        }

        [Command("genreinfo"), Alias("gi"), Summary("Returns information of a genre")]
        public async Task GenreInfo(
            [Remainder, Summary("Genre to search for")]
            string genre)
        {
            await RevalidateCache();

            var genres = entries.Select(e => e.Genre)
                .Distinct()
                .ToArray();
            var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

            if (test == null)
            {
                await ReplyAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
                return;
            }

            var tracks = entries.Where(e => string.Equals(e.Genre, test, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToArray();

            if (tracks.Length == 0)
            {
                await ReplyAsync($"No tracks with genre `{test}` found");
                return;
            }

            var count = tracks.SelectMany(t => t.ArtistsList)
                .GroupBy(a => a, s => s, (s, e) => new KeyCount<Entry>()
                {
                    Key = s,
                    Count = e.Count()
                })
                .OrderByDescending(a => a.Count)
                .ThenBy(a => a.Key)
                .ToArray();

            var sb = new StringBuilder("Top 10 artists: \r\n");
            var aaaa = count.Take(10);

            foreach (var artist in aaaa)
            {
                sb.AppendLine($"{artist.Key} - {artist.Count}");
            }

            var color = GetGenreColor(test);
            var embed = new EmbedBuilder().WithTitle(test)
                .WithDescription($"we have {tracks.Length} {test} tracks, from {count.Length} artists, including {count.First().Key}\r\n" + $"the first {test} was in {tracks.Last().Date:Y} by {tracks.Last().Artists} and the latest is on {tracks.First().Date:Y} by {tracks.First().Artists}" + $"\r\n\r\n{sb}" + $"\r\npog")
                .WithColor(color);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("subgenre"), Alias("sg"), Summary("Returns a list of up to 8 tracks of a given subgenre")]
        public async Task Subgenre(
            [Remainder, Summary("Genre to search for")]
            string genre)
        {
            await RevalidateCache();

            var genres = GetAllSubgenres();
            var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

            if (test == null)
            {
                await ReplyAsync($"Subgenre `{genre}` not found");
                return;
            }

            var tracks = entries.Where(e => e.SubgenresList.Contains(test, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToList();

            if (tracks.Count == 0)
            {
                await ReplyAsync($"No tracks with genre `{test}` found");
                return;
            }

            await SendTrackList(test, tracks, false);
        }

        [Command("subgenreexact"), Alias("sge"), Summary("Returns a list of up to 8 tracks of a given subgenre")]
        public async Task SubgenreExact(
            [Remainder, Summary("Genre to search for")]
            string genre)
        {
            await RevalidateCache();

            var genres = entries.Select(e => e.Subgenres)
                .Distinct()
                .ToArray();
            var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

            if (test == null)
            {
                await ReplyAsync($"Genre `{genre}` not found");
                return;
            }

            var tracks = entries.Where(e => string.Equals(e.Subgenres, test, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToList();

            if (tracks.Count == 0)
            {
                await ReplyAsync($"No tracks with genre `{test}` found");
                return;
            }

            await SendTrackList(test, tracks, false);
        }

        [Command("labels"), Alias("ls")]
        public async Task Labels()
        {
            await RevalidateCache();

            var labels = entries.SelectMany(e => e.LabelList)
                .GroupBy(l => l, e => e, (label, ls) =>
                {
                    var tracks = entries.Where(e => e.LabelList.Contains(label))
                        .ToArray();
                    return new
                    {
                        Key = label,
                        ArtistCount = tracks.SelectMany(e => e.ArtistsList)
                            .Distinct()
                            .Count(),
                        TrackCount = tracks.Length
                    };
                })
                .OrderByDescending(a => a.TrackCount)
                .ThenByDescending(a => a.ArtistCount)
                .ThenBy(a => a.Key)
                .ToList();

            await ReplyAsync(string.Join("\r\n", labels.Select(l => $"{l.Key} ({l.TrackCount} tracks, {l.ArtistCount} artists)")));
        }

        [Command("label"), Alias("l")]
        public async Task Label([Remainder] string label)
        {
            await RevalidateCache();

            var test = GetLabelNameFuzzy(label);

            if (string.IsNullOrWhiteSpace(test))
            {
                await ReplyAsync($"Cannot find the label `{label}`");
                return;
            }

            var tracks = GetAllTracksByLabelFuzzy(test);

            if (tracks.Count < 1)
            {
                await ReplyAsync($"Cannot find any tracks by the label `{test}`");
                return;
            }

            var latest = tracks.First();
            var earliest = tracks.Last();
            var now = DateTime.UtcNow;
            var days = Math.Floor(now.Date.Subtract(earliest.Date)
                .TotalDays);

            var embed = new EmbedBuilder().WithTitle(test)
                .WithDescription($"{test}'s latest release {(latest.Date.CompareTo(now) > 0 ? "is" : "was")} on {latest.Date:yyyy'-'MM'-'dd} by {latest.Artists}, and their first release {(earliest.Date.CompareTo(now) > 0 ? "is" : "was")} on {earliest.Date:yyyy'-'MM'-'dd} by {earliest.Artists}")
                .AddField("Tracks", tracks.Count, true)
                .AddField("Artists", tracks.SelectMany(t => t.ArtistsList).Distinct().Count(), true)
                .AddField("Years active", days <= 0 ? "Not yet active" : $"{Math.Floor(days / 365)} years and {(days % 365)} days", true);

            if (File.Exists($"logo_{test}.jpg"))
                embed = embed.WithThumbnailUrl($"https://raw.githubusercontent.com/Xythium/SubgenreSheetBot/master/SubgenreSheetBot/logo_{test}.jpg");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("debug")]
        public async Task Debug()
        {
            await RevalidateCache();

            var subgenres = entries.Where(e => e.SubgenresList.Count > 1)
                .GroupBy(e => e.Subgenres, e => e, (s, e) =>
                {
                    var enumerable = e.ToList();
                    return new KeyCount<Entry>
                    {
                        Key = s,
                        Elements = enumerable,
                        Count = enumerable.Count
                    };
                })
                .OrderByDescending(arg => arg.Count)
                .ThenBy(a => a.Key)
                .ToArray();

            var sb = new StringBuilder($"Most common combinations of subgenres ({subgenres.Length}):\r\n");

            foreach (var subgenre in subgenres.Take(25))
            {
                sb.AppendLine($"`{subgenre.Key}` - {subgenre.Count}");
            }

            await ReplyAsync(sb.ToString());
        }
    }
}