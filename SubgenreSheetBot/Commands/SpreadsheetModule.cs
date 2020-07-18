using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using FuzzySharp;
using MusicTools.Parsing.Track;

namespace SubgenreSheetBot.Commands
{
    public partial class SpreadsheetModule : ModuleBase
    {
        [Command("track"), Alias("t"), Summary("Search for a track on the sheet")]
        public async Task Track(
            [Remainder, Summary("Track to search for")]
            string search)
        {
            await RevalidateCache();

            var split = search.Split(new[]
            {
                " - "
            }, StringSplitOptions.RemoveEmptyEntries);

            List<Entry> tracks;

            if (split.Length == 1)
            {
                tracks = GetTracksByTitleFuzzy(split[0]);
            }
            else if (split.Length != 2)
            {
                await ReplyAsync($"cannot parse `{search}` into `Artist - Title` or `Title`");
                return;
            }
            else
            {
                var artist = split[0];
                var tracksByArtist = GetAllTracksByArtistFuzzy(artist);

                if (tracksByArtist.Count == 0)
                {
                    await ReplyAsync($"no tracks found by artist `{artist}`");
                    return;
                }

                var title = split[1];
                tracks = GetTracksByTitleFuzzy(tracksByArtist, title);

                if (tracks.Count == 0)
                {
                    await ReplyAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
                    return;
                }
            }

            if (tracks.Count == 0)
            {
                await ReplyAsync($"pissed left pant");
                return;
            }

            foreach (var track in tracks)
            {
                await SendTrackEmbed(track);
            }
        }

        [Command("trackexact"), Alias("te"), Summary("Search for a track on the sheet")]
        public async Task TrackExact(
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
        public async Task TrackInfoExact(
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
                var info = TrackParser.GetTrackInfo(track.FormattedArtists, track.Title, null, null, track.Date);
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

        [Command("artist"), Alias("a"), Summary("Returns info about an artist")]
        public async Task Artist(
            [Remainder, Summary("Artist to search for")]
            string artist)
        {
            await RevalidateCache();

            var tracksByArtist = GetAllTracksByArtistFuzzy(artist);

            if (tracksByArtist.Count == 0)
            {
                await ReplyAsync($"no tracks found by artist `{artist}`");
                return;
            }

            await SendArtistInfo(artist, tracksByArtist);
        }

        [Command("artistdebug"), Alias("ad"), Summary("Returns a list of up to 15 artists most similar to the given input")]
        public async Task ArtistDebug(
            [Remainder, Summary("Artist to search for")]
            string artist)
        {
            await RevalidateCache();

            var artists = Process.ExtractTop(artist, entries.SelectMany(e => e.ArtistsList)
                    .Distinct(), scorer: scorer, limit: 15)
                .ToArray();

            var sb = new StringBuilder($"{artists.Length} most similar artists (using {scorer.GetType().Name})\r\n");

            for (var i = 0; i < artists.Length; i++)
            {
                var track = artists[i];
                sb.AppendLine($"{Array.IndexOf(artists, track) + 1}. `{track.Value}` {track.Score}% similar");
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
            var search = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

            if (search == null)
            {
                await ReplyAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
                return;
            }

            var tracks = entries.Where(e => string.Equals(e.Genre, search, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToArray();

            if (tracks.Length == 0)
            {
                await ReplyAsync($"No tracks with genre `{search}` found");
                return;
            }

            var description = BuildTopNumberOfTracksList(tracks, 10, out var top, out var numArtists);
            var bpmList = BuildBpmList(tracks, 20);

            var earliest = tracks.Last();
            var latest = tracks.First();
            var now = DateTime.Now;

            var color = GetGenreColor(search);
            var embed = new EmbedBuilder().WithTitle(search)
                .WithDescription($"We have {tracks.Length} {search} tracks, from {numArtists} artists.\r\n" + $"The first track {IsWas(earliest.Date, now)} on {earliest.Date:Y} by {earliest.FormattedArtists} and the latest {IsWas(latest.Date, now)} on {latest.Date:Y} by {latest.FormattedArtists}")
                .WithColor(color)
                .AddField($"Top {top} Artists", description.ToString(), true)
                .AddField("BPM", bpmList.ToString(), true);

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
                        ArtistCount = tracks.SelectMany(e => e.ActualArtists)
                            .Distinct()
                            .Count(),
                        TrackCount = tracks.Length
                    };
                })
                .OrderByDescending(a => a.TrackCount)
                .ThenByDescending(a => a.ArtistCount)
                .ThenBy(a => a.Key)
                .ToList();

            await SendOrAttachment(string.Join("\r\n", labels.Select(l => $"{l.Key} ({l.TrackCount} tracks, {l.ArtistCount} artists)")));
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

            if (tracks.Length < 1)
            {
                await ReplyAsync($"Cannot find any tracks by the label `{test}`");
                return;
            }

            var latest = tracks.First();
            var earliest = tracks.Last();
            var now = DateTime.UtcNow;
            var days = Math.Floor(now.Date.Subtract(earliest.Date)
                .TotalDays);

            var numArtists = tracks.SelectMany(t => t.ActualArtists)
                .Distinct()
                .Count();

            var embed = new EmbedBuilder().WithTitle(test)
                .WithDescription($"{test}'s latest release {IsWas(latest.Date, now)} on {latest.Date.ToString(DateFormat[0])} by {latest.FormattedArtists}, and their first release {IsWas(earliest.Date, now)} on {earliest.Date.ToString(DateFormat[0])} by {earliest.FormattedArtists}")
                .AddField("Tracks", tracks.Length, true)
                .AddField("Artists", numArtists, true)
                .AddField("Years active", days <= 0 ? "Not yet active" : $"{Math.Floor(days / 365)} years and {days % 365} days", true)
                .AddField("Genres", BuildTopGenreList(tracks, 5)
                    .ToString(), true);

            if (File.Exists($"logo_{test}.jpg"))
                embed = embed.WithThumbnailUrl($"https://raw.githubusercontent.com/Xythium/SubgenreSheetBot/master/SubgenreSheetBot/logo_{test}.jpg");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("labelartists"), Alias("la")]
        public async Task LabelArtists([Remainder] string label)
        {
            await RevalidateCache();

            var test = GetLabelNameFuzzy(label);

            if (string.IsNullOrWhiteSpace(test))
            {
                await ReplyAsync($"Cannot find the label `{label}`");
                return;
            }

            var tracks = GetAllTracksByLabelFuzzy(test);

            if (tracks.Length < 1)
            {
                await ReplyAsync($"Cannot find any tracks by the label `{test}`");
                return;
            }

            var artists = tracks.SelectMany(e => e.ActualArtists)
                .GroupBy(l => l, e => e, (s, list) =>
                {
                    return new
                    {
                        Key = s,
                        Count = list.Count()
                    };
                })
                .OrderByDescending(a => a.Count)
                .ThenBy(a => a.Key)
                .ToList();

            var sb = new StringBuilder();

            for (var index = 0; index < artists.Count; index++)
            {
                var artist = artists[index];
                sb.AppendLine($"{index + 1}. {artist.Key}: {artist.Count} tracks");
            }

            await SendOrAttachment(sb.ToString());
        }

        [Command("debug")]
        public async Task Debug()
        {
            await RevalidateCache();

            var subgenres = entries.Where(e => e.SubgenresList.Length > 1)
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