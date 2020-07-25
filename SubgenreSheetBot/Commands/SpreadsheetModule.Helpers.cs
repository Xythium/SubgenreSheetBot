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
using MusicTools.Parsing.Track;
using MusicTools.Utils;
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
            {
                "Country", new Color(181, 104, 12)
            },
            {
                "Jazz", new Color(135, 206, 250)
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

        private static readonly IRatioScorer scorer = new TokenSetScorer();

        private static Color GetGenreColor(string genre)
        {
            if (!genreColors.TryGetValue(genre, out var color))
                color = Color.Default;
            return color;
        }

        private static List<Entry> GetAllTracksByArtistExact(string artist)
        {
            return entries.Where(e => string.Equals(e.OriginalArtists, artist, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="artist"></param>
        /// <param name="includeRemixes">Include XXX (artist Remix)</param>
        /// <param name="includeRemixed">Include artist (XXX Remix)</param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        private static List<Entry> GetAllTracksByArtistFuzzy(string artist, bool includeRemixes = true, bool includeRemixed = false, int threshold = 80)
        {
            var tracks = new List<Entry>();

            foreach (var entry in entries)
            {
                // include featured artists
                if (entry.Info.Features.Any(s => fuzzyFunc(s, artist, PreprocessMode.Full) >= threshold))
                {
                    tracks.Add(entry);
                }

                // remix by searching artist should be including
                if (includeRemixes)
                {
                    // track is a remix
                    if (entry.Info.Remixers.Count > 0)
                    {
                        // remixers include searching artist
                        if (entry.Info.Remixers.Any(s => fuzzyFunc(s, artist, PreprocessMode.Full) >= threshold))
                        {
                            tracks.Add(entry);
                        }
                    }
                }

                // remixes of searching artist should be included
                if (includeRemixed)
                {
                    // track is a remix
                    if (entry.Info.Remixers.Count > 0)
                    {
                        // track is by searching artist
                        if (entry.Info.Artists.Any(s => fuzzyFunc(s, artist, PreprocessMode.Full) >= threshold))
                        {
                            tracks.Add(entry);
                        }
                    }
                }

                // track is not a remix
                if (entry.Info.Remixers.Count < 1)
                {
                    // track is by searching artist
                    if (entry.Info.Artists.Any(s => fuzzyFunc(s, artist, PreprocessMode.Full) >= threshold))
                    {
                        tracks.Add(entry);
                    }
                }
            }

            return tracks.OrderByDescending(e => e.Date)
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

        private static Entry[] GetAllTracksByLabelFuzzy(string label, int threshold = 80)
        {
            var test = GetLabelNameFuzzy(label, threshold);
            return entries.Where(e => e.LabelList.Any(s => string.Equals(s, test, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(e => e.Date)
                .ToArray();
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
                    .WithValue(track.FormattedArtists)
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
                .WithValue(string.Join(", ", track.LabelList))
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

        private async Task SendTrackList(string search, string[] artists, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
        {
            var sb = BuildTrackList(search, artists, tracks, includeGenreless, numLatest, numEarliest, includeIndex, includeArtist, includeTitle, includeLabel, includeDate);
            await ReplyAsync(sb.ToString());
        }

        private static StringBuilder BuildTrackList(string search, string[] artists, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
        {
            var genrelessCount = 0;

            if (!includeGenreless)
            {
                genrelessCount = tracks.Count(e => e.Sheet == "Genreless");
                tracks = tracks.Where(t => t.Sheet != "Genreless")
                    .ToList();
            }

            var latestTracks = tracks.Where(e => e.Date <= DateTime.UtcNow)
                .OrderByDescending(e => e.Date)
                .Take(numLatest)
                .ToArray();
            var earliestTracks = tracks.Where(e => e.Date <= DateTime.UtcNow)
                .Reverse()
                .Take(numEarliest)
                .ToArray();

            var sb = new StringBuilder($"`{search}` has {tracks.Count} tracks");

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

            var trackCount = latestTracks.Length + earliestTracks.Length;
            var cutoffThreshold = numLatest + numEarliest;

            if (tracks.Count < cutoffThreshold)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    sb.AppendLine(FormatTrack(search, artists, tracks, track, includeIndex, includeArtist, includeTitle, includeLabel, includeDate));
                }
            }
            else
            {
                for (var i = 0; i < latestTracks.Length; i++)
                {
                    var track = latestTracks[i];
                    sb.AppendLine(FormatTrack(search, artists, tracks, track, includeIndex, includeArtist, includeTitle, includeLabel, includeDate));
                }

                if (trackCount >= cutoffThreshold)
                {
                    sb.AppendLine("...");
                }

                if (!(latestTracks.Length < numLatest))
                {
                    for (var i = earliestTracks.Length - 1; i >= 0; i--)
                    {
                        var track = earliestTracks[i];
                        sb.AppendLine(FormatTrack(search, artists, tracks, track, includeIndex, includeArtist, includeTitle, includeLabel, includeDate));
                    }
                }
            }

            return sb;
        }

        private static StringBuilder BuildBpmList(Entry[] tracks, int range = 10)
        {
            var bpms = tracks.SelectMany(t => t.BpmList)
                .Select(d => new
                {
                    From = (int) (d / range) * range + 1,
                    //To = (int)(d / 10) * 10 + 10,
                })
                .GroupBy(d => d, d => d, (d, e) => new
                {
                    d.From,
                    //d.To,
                    Count = e.Count()
                })
                .OrderBy(d => d.From)
                .ToArray();

            var bpmList = new StringBuilder();

            foreach (var grouping in bpms)
            {
                bpmList.AppendLine($"{grouping.From}-{grouping.From + (range - 1)}: {grouping.Count}");
            }

            return bpmList;
        }

        private static StringBuilder BuildTopGenreList(Entry[] tracks, int top = 10, bool ignoreUnknown = true)
        {
            var genres = tracks.Select(t => t.Genre)
                .GroupBy(d => d, d => d, (d, e) => new KeyCount
                {
                    Key = d,
                    Count = e.Count()
                })
                .Where(d => ignoreUnknown && d.Key != "?")
                .OrderByDescending(d => d.Count)
                .ThenBy(d => d.Key)
                .Take(top)
                .ToArray();

            var genreList = new StringBuilder();

            foreach (var grouping in genres)
            {
                genreList.AppendLine($"{grouping.Key}: {grouping.Count}");
            }

            return genreList;
        }

        private static StringBuilder BuildTopNumberOfTracksList(Entry[] tracks, int top, out int actualTop, out int numArtists)
        {
            var artistCount = tracks.SelectMany(t => t.ActualArtistsNoFeatures)
                .GroupBy(a => a, s => s, (s, e) => new KeyCount
                {
                    Key = s,
                    Count = e.Count()
                })
                .OrderByDescending(a => a.Count)
                .ThenBy(a => a.Key)
                .ToArray();

            numArtists = artistCount.Length;
            var topList = artistCount.Take(top)
                .ToArray();
            actualTop = topList.Length;

            var description = new StringBuilder();

            foreach (var artist in topList)
            {
                description.AppendLine($"{artist.Key} - {artist.Count} tracks");
            }

            return description;
        }

        private static string FormatTrack(string search, string[] artists, List<Entry> tracks, Entry track, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
        {
            var sb = new StringBuilder();

            if (includeIndex)
            {
                sb.Append($"{tracks.IndexOf(track) + 1}. ");
            }

            if (includeArtist && includeTitle)
            {
                sb.Append($"{track.FormattedArtists} - {track.Title} ");
            }
            else if (includeArtist)
            {
                sb.Append($"{track.FormattedArtists} ");
            }
            else if (includeTitle)
            {
                if (track.ArtistsList.Length > 1)
                {
                    var notFound = track.ArtistsList.Where(artist => !artists.Contains(artist))
                        .ToArray();

                    if (notFound.Length > 0)
                        sb.Append($"{track.Title} (w/ {string.Join(" & ", notFound)}) ");
                }
                else
                    sb.Append($"{track.Title} ");
            }

            if (includeLabel)
            {
                sb.Append($"[{string.Join(", ", track.LabelList)}] ");
            }

            if (includeDate)
            {
                sb.Append($"{track.Date.ToString(DateFormat[0])}");
            }

            return sb.ToString()
                .Trim();
        }

        private static string IsWas(DateTime date, DateTime compare) { return date.CompareTo(compare) > 0 ? "is" : "was"; }

        private async Task SendArtistInfo(string search, string[] artists, List<Entry> tracks)
        {
            var latest = tracks.First();
            var earliest = tracks.Last();
            var now = DateTime.UtcNow;

            var embed = new EmbedBuilder().WithTitle(string.Join(", ", artists))
                .WithDescription($"`{search}` matches the artists {string.Join(", ", artists)}. The first latest track {IsWas(latest.Date, now)} **{latest.Title} ({latest.Date:Y})**, and the first track {IsWas(earliest.Date, now)} **{earliest.Title} ({earliest.Date:Y})**")
                .AddField("Tracks", BuildTrackList(search, artists, tracks, includeArtist: false)
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

        private async Task SendOrAttachment(string str)
        {
            if (str.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(str));
                await Context.Channel.SendFileAsync(writer, "content.txt", $"Message too long");
            }
            else
            {
                await ReplyAsync(str);
            }
        }
    }

    public class Entry
    {
        public string Sheet { get; private set; }

        public DateTime Date { get; private set; }

        public bool Spotify { get; private set; }

        public bool SoundCloud { get; private set; }

        public bool Beatport { get; private set; }

        public string Genre { get; private set; }

        public string Subgenres { get; private set; }

        public string[] SubgenresList { get; private set; }

        public string OriginalArtists { get; private set; }

        public string[] ArtistsList { get; private set; }

        public string FormattedArtists => string.Join(" x ", ArtistsList);

        /// <summary>
        /// All artists of a track (remixers instead of original artists if remix, includes featured artists)
        /// </summary>
        public string[] ActualArtists
        {
            get
            {
                var artists = new SortedSet<string>();

                foreach (var feature in Info.Features)
                {
                    artists.Add(feature);
                }

                if (Info.Remixers.Count > 0)
                {
                    foreach (var remixer in Info.Remixers)
                    {
                        artists.Add(remixer);
                    }
                }
                else
                {
                    foreach (var artist in Info.Artists)
                    {
                        artists.Add(artist);
                    }
                }

                return artists.ToArray();
            }
        }

        /// <summary>
        /// All artists of a track (remixers instead of original artists if remix)
        /// </summary>
        public string[] ActualArtistsNoFeatures
        {
            get
            {
                var artists = new SortedSet<string>();

                if (Info.Remixers.Count > 0)
                {
                    foreach (var remixer in Info.Remixers)
                    {
                        artists.Add(remixer);
                    }
                }
                else
                {
                    foreach (var artist in Info.Artists)
                    {
                        artists.Add(artist);
                    }
                }

                return artists.ToArray();
            }
        }

        public string Title { get; private set; }

        private string Label { get; set; } // todo: unused

        public List<string> LabelList { get; private set; }

        public TimeSpan? Length { get; private set; }

        public bool CorrectBpm { get; private set; }

        public string Bpm { get; private set; } // todo: cant be decimal at the moment because '98.5 > 95'

        public List<decimal> BpmList
        {
            get
            {
                var list = new List<decimal>();
                if (string.IsNullOrWhiteSpace(Bpm))
                    return list;

                var split = Bpm.Split(new[]
                {
                    '/', '>'
                }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 1)
                    return list;

                foreach (var s in split)
                {
                    if (decimal.TryParse(s, out var dec))
                        list.Add(dec);
                }

                return list;
            }
        }

        public bool CorrectKey { get; private set; }

        public string Key { get; private set; }

        public TrackInfo Info { get; set; }

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
                SubgenresList = SubgenresUtils.SplitSubgenres(subgenre)
                    .ToArray(),
                OriginalArtists = artists,
                ArtistsList = ArtistUtils.SplitArtists(artists)
                    .ToArray(),
                Title = title,
                Label = label,
                LabelList = SubgenresUtils.SplitSubgenres(label),
                Length = length,
                CorrectBpm = correctBpm,
                Bpm = bpmStr,
                CorrectKey = correctKey,
                Key = key
            };
            entry.Info = TrackParser.GetTrackInfo(entry.FormattedArtists, entry.Title, "", "", entry.Date);

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

    public struct KeyCount
    {
        public string Key;
        public int Count;
    }
}