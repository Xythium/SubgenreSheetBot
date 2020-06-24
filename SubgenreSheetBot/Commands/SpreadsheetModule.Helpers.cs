﻿using System;
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
                var values = await BatchRequest("'2020-2024'!A2:K", "'2015-2019'!A2:K", "'2010-2014'!A2:K", "'Upcoming'!A2:K", "'Pre-2010s'!A2:K", "'Genreless'!A2:K");
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
                    .WithIsInline(true),
                new EmbedFieldBuilder().WithName("Genre")
                    .WithValue(track.Subgenres)
                    .WithIsInline(true),
                new EmbedFieldBuilder().WithName("Date")
                    .WithValue(track.Date.ToString("yyyy'-'MM'-'dd"))
                    .WithIsInline(true),
                new EmbedFieldBuilder().WithName("Primary Label")
                    .WithValue(track.Label)
                    .WithIsInline(true),
            };
            if (track.Length != null)
                fields.Add(new EmbedFieldBuilder().WithName("Length")
                    .WithValue($"{(int) track.Length.Value.TotalMinutes}:{track.Length.Value.Seconds.ToString().PadLeft(2, '0')}")
                    .WithIsInline(true));
            if (!string.IsNullOrWhiteSpace(track.Bpm))
                fields.Add(new EmbedFieldBuilder().WithName("BPM")
                    .WithValue(track.Bpm)
                    .WithIsInline(true));
            if (!string.IsNullOrWhiteSpace(track.Key))
                fields.Add(new EmbedFieldBuilder().WithName("Key")
                    .WithValue(track.Key)
                    .WithIsInline(true));

            var builder = new EmbedBuilder().WithColor(GetGenreColor(track.Genre))
                .WithFields(fields)
                .Build();
            await ReplyAsync(null, false, builder);
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
                .WithValue(info.ScrobbledDate.ToString("yyyy'-'MM'-'dd"))
                .WithIsInline(true));

            var builder = new EmbedBuilder().WithFields(fields)
                .Build();
            await ReplyAsync(null, false, builder);
        }

        private async Task SendTrackList(string search, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3)
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

            if (tracks.Count < cutoffThreshold)
            {
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    sb.AppendLine($"{tracks.IndexOf(track) + 1}. {track.Artists} - {track.Title} [{track.Label}] {track.Date:yyyy'-'MM'-'dd}");
                }
            }
            else
            {
                for (var i = 0; i < latestTracks.Length; i++)
                {
                    var track = latestTracks[i];
                    sb.AppendLine($"{tracks.IndexOf(track) + 1}. {track.Artists} - {track.Title} [{track.Label}] {track.Date:yyyy'-'MM'-'dd}");
                }

                sb.AppendLine("...");

                for (var i = earliestTracks.Length - 1; i >= 0; i--)
                {
                    var track = earliestTracks[i];
                    sb.AppendLine($"{tracks.IndexOf(track) + 1}. {track.Artists} - {track.Title} [{track.Label}] {track.Date:yyyy'-'MM'-'dd}");
                }
            }

            await ReplyAsync(sb.ToString());
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

            static string GetStringArgument(IList<object> row, int index, string def)
            {
                if (index >= row.Count)
                    return def;
                if (string.IsNullOrWhiteSpace((string) row[index]))
                    return def;

                return (string) row[index];
            }

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
                        var dateStr = GetStringArgument(row, A, null);
                        if (dateStr == null)
                            continue;

                        if (!DateTime.TryParseExact(dateStr, new[]
                        {
                            "yyyy'-'MM'-'dd"
                        }, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                        {
                            Log.Error($"cannot parse {dateStr}");
                            continue;
                        }

                        var genre = GetStringArgument(row, B, null);
                        var subgenre = GetStringArgument(row, C, null);
                        var artists = GetStringArgument(row, D, null);
                        var title = GetStringArgument(row, E, null);
                        var label = GetStringArgument(row, F, null);

                        var lengthStr = GetStringArgument(row, G, null);
                        TimeSpan? length;

                        if (lengthStr == null || lengthStr == "--:--")
                            length = null;
                        else
                        {
                            if (!TimeSpan.TryParseExact(lengthStr, new[]
                            {
                                "m':'ss" /*, "h:mm:ss"*/
                            }, CultureInfo.CurrentCulture, TimeSpanStyles.None, out var l))
                            {
                                Log.Fatal($"cannot parse {lengthStr}");
                                length = null;
                            }
                            else
                            {
                                length = l;
                            }
                        }

                        var bpmStr = GetStringArgument(row, I, null);
                        var key = GetStringArgument(row, K, null);

                        var entry = new Entry
                        {
                            Sheet = sheet,
                            Date = date,
                            Genre = genre,
                            Subgenres = subgenre,
                            Artists = artists,
                            Title = title,
                            Label = label,
                            Length = length,
                            Bpm = bpmStr,
                            Key = key
                        };
                        entries.Add(entry);
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

    public class Entry : IComparable<Entry>, IComparable
    {
        public string Sheet { get; set; }

        public DateTime Date { get; set; }

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

        public string Bpm { get; set; } // todo: cant be decimal at the moment because '98.5 > 95'

        public string Key { get; set; }

        public int CompareTo(Entry other)
        {
            if (ReferenceEquals(this, other))
                return 0;
            if (ReferenceEquals(null, other))
                return 1;

            var sheetComparison = string.Compare(Sheet, other.Sheet, StringComparison.Ordinal);
            if (sheetComparison != 0)
                return sheetComparison;

            var dateComparison = Date.CompareTo(other.Date);
            if (dateComparison != 0)
                return dateComparison;

            var genreComparison = string.Compare(Genre, other.Genre, StringComparison.Ordinal);
            if (genreComparison != 0)
                return genreComparison;

            var subgenresComparison = string.Compare(Subgenres, other.Subgenres, StringComparison.Ordinal);
            if (subgenresComparison != 0)
                return subgenresComparison;

            var artistsComparison = string.Compare(Artists, other.Artists, StringComparison.Ordinal);
            if (artistsComparison != 0)
                return artistsComparison;

            var titleComparison = string.Compare(Title, other.Title, StringComparison.Ordinal);
            if (titleComparison != 0)
                return titleComparison;

            var labelComparison = string.Compare(Label, other.Label, StringComparison.Ordinal);
            if (labelComparison != 0)
                return labelComparison;

            var lengthComparison = Nullable.Compare(Length, other.Length);
            if (lengthComparison != 0)
                return lengthComparison;

            var bpmComparison = string.Compare(Bpm, other.Bpm, StringComparison.Ordinal);
            if (bpmComparison != 0)
                return bpmComparison;

            return string.Compare(Key, other.Key, StringComparison.Ordinal);
        }

        public int CompareTo(object obj)
        {
            var other = (Entry) obj;
            if (ReferenceEquals(this, other))
                return 0;
            if (ReferenceEquals(null, other))
                return 1;

            var sheetComparison = string.Compare(Sheet, other.Sheet, StringComparison.Ordinal);
            if (sheetComparison != 0)
                return sheetComparison;

            var dateComparison = Date.CompareTo(other.Date);
            if (dateComparison != 0)
                return dateComparison;

            var genreComparison = string.Compare(Genre, other.Genre, StringComparison.Ordinal);
            if (genreComparison != 0)
                return genreComparison;

            var subgenresComparison = string.Compare(Subgenres, other.Subgenres, StringComparison.Ordinal);
            if (subgenresComparison != 0)
                return subgenresComparison;

            var artistsComparison = string.Compare(Artists, other.Artists, StringComparison.Ordinal);
            if (artistsComparison != 0)
                return artistsComparison;

            var titleComparison = string.Compare(Title, other.Title, StringComparison.Ordinal);
            if (titleComparison != 0)
                return titleComparison;

            var labelComparison = string.Compare(Label, other.Label, StringComparison.Ordinal);
            if (labelComparison != 0)
                return labelComparison;

            var lengthComparison = Nullable.Compare(Length, other.Length);
            if (lengthComparison != 0)
                return lengthComparison;

            var bpmComparison = string.Compare(Bpm, other.Bpm, StringComparison.Ordinal);
            if (bpmComparison != 0)
                return bpmComparison;

            return string.Compare(Key, other.Key, StringComparison.Ordinal);
        }
    }

    public struct KeyCount<T>
    {
        public string Key;
        public int Count;
        public List<T> Elements;
    }
}