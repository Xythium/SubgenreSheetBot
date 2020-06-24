using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using FuzzySharp;
using FuzzySharp.PreProcess;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using MusicTools;
using Serilog;

namespace SubgenreSheetBot.Commands
{
    public partial class SpreadsheetModule : ModuleBase
    {
        private readonly SheetsService sheetService;
        private const string SPREADSHEET_ID = "13reh863zpVJEnFR8vFJ7dRhaln86ETk9etbE7tFHS2g";

        private static readonly Func<string, string, PreprocessMode, int> fuzzyFunc = Fuzz.TokenSetRatio;

        #region Setup
        public SpreadsheetModule()
        {
            UserCredential credential;

            using (var stream = new FileStream(new FileInfo("credentials.json").FullName, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(GoogleClientSecrets.Load(stream)
                        .Secrets, new[]
                    {
                        SheetsService.Scope.SpreadsheetsReadonly
                    }, "user", CancellationToken.None, new FileDataStore("token", true))
                    .Result;
            }

            sheetService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Subgenre Sheet Bot"
            });
        }

        private const int A = 0;
        private const int B = A + 1;
        private const int C = B + 1;
        private const int D = C + 1;
        private const int E = D + 1;
        private const int F = E + 1;
        private const int G = F + 1;
        private const int H = G + 1;
        private const int I = H + 1;
        private const int J = I + 1;
        private const int K = J + 1;

        private static List<Entry> entries = new List<Entry>();
        private static DateTime? lastTime = null;

        private async Task RevalidateCache()
        {
            if (lastTime == null || DateTime.UtcNow.Subtract(lastTime.Value)
                .TotalSeconds > 60)
            {
                var now = DateTime.UtcNow;

                entries = new List<Entry>();
                var values = await BatchRequest("'2020-2024'!A2:I", "'2015-2019'!A2:I", "'2010-2014'!A2:I", "'Upcoming'!A2:I", "'Pre-2010s'!A2:I", "'Genreless'!A2:I");
                if (values != null)
                    entries.AddRange(values);

                lastTime = DateTime.UtcNow;
                Log.Information($"Cache revalidation took {DateTime.UtcNow.Subtract(now).TotalMilliseconds}ms");
            }
        }

        private static readonly Dictionary<string, Color> genreColors = new Dictionary<string, Color>
        {
            {
                "Hip Hop", new Color(215, 127, 125)
            },
            {
                "Traditional", new Color(208, 173, 96)
            },
            {
                "Future Bass", new Color(153, 153, 251)
            },
            {
                "UK Garage", new Color(191, 127, 255)
            },
            {
                "Downtempo", new Color(240, 180, 181)
            },
            {
                "Ambient", new Color(240, 180, 181)
            },
            {
                "Drum & Bass", new Color(246, 26, 3)
            },
            {
                "?", new Color(185, 185, 185)
            },
            {
                "Electronic", new Color(185, 185, 185)
            },
            {
                "Miscellaneous", new Color(185, 185, 185)
            },
            {
                "Experimental", new Color(117, 124, 107)
            },
            {
                "House", new Color(235, 130, 0)
            },
            {
                "Electro House", new Color(231, 205, 0)
            },
            {
                "Hardcore", new Color(0, 150, 0)
            },
            {
                "Midtempo", new Color(12, 151, 88)
            },
            {
                "Pop", new Color(22, 172, 176)
            },
            {
                "Trance", new Color(0, 127, 232)
            },
            {
                "Dubstep", new Color(150, 30, 234)
            },
            {
                "Drumstep", new Color(242, 33, 137)
            },
            {
                "Trap", new Color(129, 0, 41)
            },
            {
                "Metal", new Color(0, 58, 18)
            },
            {
                "Punk", new Color(58, 0, 58)
            },
            {
                "Breaks", new Color(10, 24, 87)
            },
            {
                "Rock", new Color(135, 192, 149)
            },
            {
                "R&B", new Color(105, 136, 162)
            },
            {
                "Industrial", new Color(40, 40, 40)
            },
            {
                "Techno", new Color(42, 63, 215)
            },
            {
                "Synthwave", new Color(103, 78, 167)
            },
            {
                "Space Bass", new Color(167, 78, 142)
            },
        };
        #endregion

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
                .GroupBy(l => l, e => e, (s, e) => new
                {
                    Key = s,
                    TrackCount = e.Count()
                })
                .OrderByDescending(a => a.TrackCount)
                .ThenBy(a => a.Key)
                .ToList();

            await ReplyAsync(string.Join("\r\n", labels.Select(l => $"{l.Key} ({l.TrackCount} tracks, {entries.Where(e => e.LabelList.Contains(l.Key)).SelectMany(e => e.ArtistsList).Distinct().Count()} artists)")));
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
                .AddField("Years active", days <= 0 ? "Not yet active" : $"{Math.Floor(days / 365)} years and {(days % 365)} days", true);

            if (test == "mau5trap")
                embed = embed.WithThumbnailUrl("https://i0.wp.com/thegroovecartel.com/wp-content/uploads/2019/07/mau5trap-logo.jpg");

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