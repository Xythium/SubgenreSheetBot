using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using FuzzySharp;
using FuzzySharp.PreProcess;
using FuzzySharp.SimilarityRatio.Scorer;
using FuzzySharp.SimilarityRatio.Scorer.StrategySensitive;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using MusicTools;
using Serilog;

namespace SubgenreSheetBot.Commands
{
    public partial class SpreadsheetModule
    {
        private readonly SheetsService sheetService;
        private const string SPREADSHEET_ID = "13reh863zpVJEnFR8vFJ7dRhaln86ETk9etbE7tFHS2g";

        private static readonly Func<string, string, PreprocessMode, int> fuzzyFunc = Fuzz.TokenSetRatio;

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

        private static List<Entry> entries = new List<Entry>();
        private static DateTime? lastTime = null;

        private async Task RevalidateCache()
        {
            if (lastTime == null || DateTime.UtcNow.Subtract(lastTime.Value)
                .TotalSeconds > 60)
            {
                var now = DateTime.UtcNow;

                entries = new List<Entry>();
                var values = await BatchRequest("'2020-2024'!A2:N", "'2015-2019'!A2:N", "'2010-2014'!A2:N", "'Upcoming'!A2:N", "'Pre-2010s'!A2:N", "'Genreless'!A2:N");
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

        public static readonly string[] DateFormat =
        {
            "yyyy'-'MM'-'dd"
        };

        public static readonly string[] TimeFormat =
        {
            "m':'ss" /*, "h:mm:ss"*/
        };

        private static IRatioScorer scorer = new TokenSetScorer();

        private static Color GetGenreColor(string genre)
        {
            if (!genreColors.TryGetValue(genre, out var color))
                color = Color.Default;
            return color;
        }

        private static List<Entry> GetAllTracksByArtistExact(string artist)
        {
            return entries.Where(e => string.Equals(e.Artists, artist, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        private static List<Entry> GetAllTracksByArtistFuzzy(string artist, int threshold = 80)
        {
            return entries.Where(e => e.ArtistsList.Any(s => fuzzyFunc(s, artist, PreprocessMode.Full) >= threshold))
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        private static List<Entry> GetTracksByTitleExact(List<Entry> tracksByArtist, string title)
        {
            return tracksByArtist.Where(e => string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        private static List<Entry> GetTracksByTitleFuzzy(List<Entry> tracksByArtist, string title, int threshold = 80)
        {
            return tracksByArtist.Where(e => Fuzz.Ratio(e.Title, title, PreprocessMode.Full) >= threshold)
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        private static List<Entry> GetTracksByTitleFuzzy(string title) { return GetTracksByTitleFuzzy(entries, title); }

        private static List<Entry> GetAllTracksByLabelFuzzy(string label, int threshold = 80)
        {
            var test = GetLabelNameFuzzy(label, threshold);
            return entries.Where(e => e.LabelList.Any(s => string.Equals(s, test, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        private static string[] GetAllLabelNames()
        {
            return entries.SelectMany(e => e.LabelList)
                .Distinct()
                .ToArray();
        }

        private static string GetLabelNameFuzzy(string label, int threshold = 80)
        {
            return GetAllLabelNames()
                .Select(l => new
                {
                    Name = l,
                    Like = fuzzyFunc(l, label, PreprocessMode.Full)
                })
                .Where(l => l.Like >= threshold)
                .OrderByDescending(e => e.Like)
                .FirstOrDefault()
                ?.Name;
        }

        private static string[] GetAllSubgenres()
        {
            return entries.SelectMany(e => e.SubgenresList)
                .Distinct()
                .ToArray();
        }

        private async Task SendTrackEmbed(Entry track)
        {
            var fields = new List<EmbedFieldBuilder>
            {
                new EmbedFieldBuilder().WithName("Artists")
                    .WithValue(track.Artists)
                    .WithIsInline(true),
                new EmbedFieldBuilder().WithName("Song Title")
                    .WithValue(track.Title)
                    .WithIsInline(true)
            };
            if (track.Length != null)
                fields.Add(new EmbedFieldBuilder().WithName("Length")
                    .WithValue(track.Length.Value.ToString(TimeFormat[0]))
                    .WithIsInline(true));

            fields.Add(new EmbedFieldBuilder().WithName("Primary Label")
                .WithValue(track.Label)
                .WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Date")
                .WithValue(track.Date.ToString(DateFormat[0]))
                .WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Genre")
                .WithValue(track.Subgenres)
                .WithIsInline(true));

            if (!string.IsNullOrWhiteSpace(track.Bpm))
            {
                fields.Add(new EmbedFieldBuilder().WithName("BPM")
                    .WithValue($"{track.Bpm} {BoolToEmoji(track.CorrectBpm)}")
                    .WithIsInline(true));
            }

            if (!string.IsNullOrWhiteSpace(track.Key))
            {
                fields.Add(new EmbedFieldBuilder().WithName("Key")
                    .WithValue($"{track.Key} {BoolToEmoji(track.CorrectKey)}")
                    .WithIsInline(true));
            }

            fields.Add(new EmbedFieldBuilder().WithName("Spotify")
                .WithValue(BoolToEmoji(track.Spotify))
                .WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("SoundCloud")
                .WithValue(BoolToEmoji(track.SoundCloud))
                .WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Beatport")
                .WithValue(BoolToEmoji(track.Beatport))
                .WithIsInline(true));

            var builder = new EmbedBuilder().WithColor(GetGenreColor(track.Genre))
                .WithFields(fields)
                .Build();
            await ReplyAsync(null, false, builder);
        }

        private string BoolToEmoji(bool value)
        {
            if (value)
            {
                return "✅";
            }

            return "❌";
        }

        private async Task SendTrackInfoEmbed(TrackInfo info)
        {
            var fields = new List<EmbedFieldBuilder>
            {
                new EmbedFieldBuilder().WithName("Artists")
                    .WithValue(string.Join(", ", info.Artists))
                    .WithIsInline(true),
                new EmbedFieldBuilder().WithName("Song Title")
                    .WithValue(info.ProcessedTitle)
                    .WithIsInline(true),
            };
            if (info.Features.Count > 0)
                fields.Add(new EmbedFieldBuilder().WithName("Features")
                    .WithValue(string.Join(", ", info.Features))
                    .WithIsInline(true));
            if (info.Remixers.Count > 0)
                fields.Add(new EmbedFieldBuilder().WithName("Remixers")
                    .WithValue(string.Join(", ", info.Remixers))
                    .WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Date")
                .WithValue(info.ScrobbledDate.ToString(DateFormat[0]))
                .WithIsInline(true));

            var builder = new EmbedBuilder().WithFields(fields)
                .Build();
            await ReplyAsync(null, false, builder);
        }

        private async Task SendTrackList(string search, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
        {
            var sb = BuildTrackList(search, tracks, includeGenreless, numLatest, numEarliest, includeIndex, includeArtist, includeTitle, includeLabel, includeDate);
            await ReplyAsync($"`{search}` has {tracks.Count} tracks" + sb);
        }

        private static StringBuilder BuildTrackList(string search, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
        {
            var genrelessCount = 0;

            if (!includeGenreless)
            {
                genrelessCount = tracks.Count(e => e.Sheet == "Genreless");
                tracks = tracks.Where(t => t.Sheet != "Genreless")
                    .ToList();
            }

            var latestTracks = tracks.Where(e => e.Date <= DateTime.UtcNow)
                .Take(numLatest)
                .ToArray();
            var earliestTracks = ((IEnumerable<Entry>) tracks).Reverse()
                .Take(numEarliest)
                .ToArray();
            var cutoffThreshold = numLatest + numEarliest;

            var sb = new StringBuilder();

            var futureTracks = tracks.Where(e => e.Date > DateTime.UtcNow)
                .ToArray();

            if (futureTracks.Length > 0)
            {
                sb.Append($", and {futureTracks.Length} tracks upcoming in the next {Math.Ceiling(futureTracks.Max(e => e.Date).Subtract(DateTime.UtcNow).TotalDays)} days");
            }

            if (genrelessCount > 0)
            {
                sb.Append($", and {genrelessCount} tracks have been excluded from the Genreless tab");
            }

            sb.AppendLine("\r\n");

            if (tracks.Count < cutoffThreshold)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    sb.AppendLine(FormatTrack(tracks, track, includeIndex, includeArtist, includeTitle, includeLabel, includeDate));
                }
            }
            else
            {
                for (var i = 0; i < latestTracks.Length; i++)
                {
                    var track = latestTracks[i];
                    sb.AppendLine(FormatTrack(tracks, track, includeIndex, includeArtist, includeTitle, includeLabel, includeDate));
                }

                sb.AppendLine("...");

                for (var i = earliestTracks.Length - 1; i >= 0; i--)
                {
                    var track = earliestTracks[i];
                    sb.AppendLine(FormatTrack(tracks, track, includeIndex, includeArtist, includeTitle, includeLabel, includeDate));
                }
            }

            return sb;
        }

        private static string FormatTrack(List<Entry> tracks, Entry track, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
        {
            var sb = new StringBuilder();

            if (includeIndex)
            {
                sb.Append($"{tracks.IndexOf(track) + 1}. ");
            }

            if (includeArtist && includeTitle)
            {
                sb.Append($"{track.Artists} - {track.Title} ");
            }
            else if (includeArtist)
            {
                sb.Append($"{track.Artists} ");
            }
            else if (includeTitle)
            {
                sb.Append($"{track.Title} ");
            }

            if (includeLabel)
            {
                sb.Append($"[{track.Label}] ");
            }

            if (includeDate)
            {
                sb.Append($"{track.Date.ToString(DateFormat[0])}");
            }

            return sb.ToString()
                .Trim();
        }

        private static string IsWas(DateTime date, DateTime compare) { return date.CompareTo(compare) > 0 ? "is" : "was"; }

        private async Task SendArtistInfo(string artist, List<Entry> tracks)
        {
            var latest = tracks.First();
            var earliest = tracks.Last();
            var now = DateTime.UtcNow;
            var days = Math.Floor(now.Date.Subtract(earliest.Date)
                .TotalDays);

            var embed = new EmbedBuilder().WithTitle(artist)
                .WithDescription($"{artist}'s latest track {IsWas(latest.Date, now)} **{latest.Title} ({latest.Date:Y})**, and their first track {IsWas(earliest.Date, now)} **{earliest.Title} ({earliest.Date:Y})**")
                .AddField("Tracks", BuildTrackList(artist, tracks, includeArtist: false)
                    .ToString());

            await ReplyAsync(embed: embed.Build());
        }

        private async Task<List<Entry>> BatchRequest(params string[] ranges)
        {
            var request = sheetService.Spreadsheets.Values.BatchGet(SPREADSHEET_ID);
            request.Ranges = ranges;
            var response = await request.ExecuteAsync();

            var valueRanges = response.ValueRanges;
            if (valueRanges == null)
                return null;
            if (valueRanges.Count == 0)
                return null;

            var entries = new List<Entry>();

            foreach (var range in valueRanges)
            {
                Log.Verbose($"{range.Range} | {range.ETag} | {range.MajorDimension}");
                if (range.Values == null)
                    continue;
                if (range.Values.Count == 0)
                    continue;

                var sheet = range.Range;
                var index = sheet.IndexOf("!", StringComparison.Ordinal);
                if (index >= 0)
                    sheet = sheet.Substring(0, index);

                foreach (var row in range.Values)
                {
                    try
                    {
                        if (Entry.TryParse(row, sheet, out var entry))
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        await ReplyAsync($"({row.Count}) {string.Join(", ", row)}");
                        await ReplyAsync(ex.ToString());
                        return null;
                    }
                }
            }

            return entries;
        }
    }

    public class Entry
    {
        public string Sheet { get; set; }

        public DateTime Date { get; set; }

        public bool Spotify { get; set; }

        public bool SoundCloud { get; set; }

        public bool Beatport { get; set; }

        public string Genre { get; set; }

        public string Subgenres { get; set; }

        public List<string> SubgenresList => MusicTools.Subgenres.SplitSubgenres(Subgenres);

        public string Artists { get; set; }

        public string[] ArtistsList => Artist.SplitArtists(Artists)
            .ToArray();

        public string Title { get; set; }

        public string Label { get; set; }

        public List<string> LabelList => MusicTools.Subgenres.SplitSubgenres(Label);

        public TimeSpan? Length { get; set; }

        public bool CorrectBpm { get; set; }

        public string Bpm { get; set; } // todo: cant be decimal at the moment because '98.5 > 95'

        public bool CorrectKey { get; set; }

        public string Key { get; set; }

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
        private const int L = K + 1;
        private const int M = L + 1;
        private const int N = M + 1;

        public static bool TryParse(IList<object> row, string sheet, out Entry entry)
        {
            var date = GetDateArgument(row, A, null);

            if (date == null)
            {
                entry = null;
                return false;
            }

            var spotify = GetBoolArgument(row, B, false);
            var soundcloud = GetBoolArgument(row, C, false);
            var beatport = GetBoolArgument(row, D, false);
            var genre = GetStringArgument(row, E, null);
            var subgenre = GetStringArgument(row, F, null);
            var artists = GetStringArgument(row, G, null);
            var title = GetStringArgument(row, H, null);
            var label = GetStringArgument(row, I, null);
            var length = GetTimeArgument(row, J, null);
            var correctBpm = GetBoolArgument(row, K, false);
            var bpmStr = GetStringArgument(row, L, null);
            var correctKey = GetBoolArgument(row, M, false);
            var key = GetStringArgument(row, N, null);

            entry = new Entry
            {
                Sheet = sheet,
                Date = date.Value,
                Spotify = spotify,
                SoundCloud = soundcloud,
                Beatport = beatport,
                Genre = genre,
                Subgenres = subgenre,
                Artists = artists,
                Title = title,
                Label = label,
                Length = length,
                CorrectBpm = correctBpm,
                Bpm = bpmStr,
                CorrectKey = correctKey,
                Key = key
            };

            return true;
        }

        private static string GetStringArgument(IList<object> row, int index, string def)
        {
            if (index >= row.Count)
                return def;

            var str = (string) row[index];
            if (string.IsNullOrWhiteSpace(str))
                return def;

            return str;
        }

        private static DateTime? GetDateArgument(IList<object> row, int index, DateTime? def)
        {
            if (index >= row.Count)
                return def;

            var str = (string) row[index];
            if (string.IsNullOrWhiteSpace(str))
                return def;

            if (!DateTime.TryParseExact(str, SpreadsheetModule.DateFormat, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            {
                Log.Error($"cannot parse {str}");
                return def;
            }

            return date;
        }

        private static TimeSpan? GetTimeArgument(IList<object> row, int index, TimeSpan? def)
        {
            if (index >= row.Count)
                return def;

            var str = (string) row[index];
            if (string.IsNullOrWhiteSpace(str) || str == "--:--")
                return def;

            if (!TimeSpan.TryParseExact(str, SpreadsheetModule.TimeFormat, CultureInfo.CurrentCulture, TimeSpanStyles.None, out var time))
            {
                Log.Error($"cannot parse {str}");
                return def;
            }

            return time;
        }

        private static bool GetBoolArgument(IList<object> row, int index, bool def)
        {
            if (index >= row.Count)
                return def;

            var str = (string) row[index];
            if (string.IsNullOrWhiteSpace(str))
                return def;

            if (string.Equals(str, "TRUE", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(str, "FALSE", StringComparison.OrdinalIgnoreCase))
                return false;

            return def;
        }
    }

    public struct KeyCount<T>
    {
        public string Key;
        public int Count;
        public List<T> Elements;
    }
}