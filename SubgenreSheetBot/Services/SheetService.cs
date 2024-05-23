using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Common;
using Common.SubgenreSheet;
using Discord;
using FuzzySharp;
using FuzzySharp.PreProcess;
using FuzzySharp.SimilarityRatio.Scorer;
using FuzzySharp.SimilarityRatio.Scorer.StrategySensitive;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using MetaBrainz.MusicBrainz;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using MusicTools.Parsing.Track;
using Newtonsoft.Json;
using Serilog;
using SubgenreSheetBot.Commands;
using Color = Discord.Color;

namespace SubgenreSheetBot.Services;

public class SheetService
{
    private readonly SheetsService api;
    private const string SPREADSHEET_ID = "13reh863zpVJEnFR8vFJ7dRhaln86ETk9etbE7tFHS2g";

    private static readonly Func<string, string, PreprocessMode, int> fuzzyFunc = Fuzz.TokenSetRatio;

    private readonly GraphService graphService;
    private readonly MusicBrainzService mbService;

    public SheetService(GraphService graphService, MusicBrainzService mbService)
    {
        if (api != null)
            throw new Exception("API already initialized");

        UserCredential credential;

        using (var stream = new FileStream(new FileInfo("credentials.json").FullName, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(GoogleClientSecrets.Load(stream).Secrets, new[]
                                                     {
                                                         SheetsService.Scope.SpreadsheetsReadonly
                                                     }, "user", CancellationToken.None, new FileDataStore("token", true))
                                                     .Result;
        }

        api = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Subgenre Sheet Bot"
        });

        this.graphService = graphService;
        this.mbService = mbService;
    }

    private static List<Entry> _entries = new();
    private static DateTime? _lastTime = null;
    private static readonly TimeSpan expiryTime = TimeSpan.FromMinutes(5);

    private async Task CheckIfCacheExpired(DynamicContext context)
    {
        var now = DateTime.UtcNow;
        if (_lastTime is null || now.Subtract(_lastTime.Value).TotalSeconds > expiryTime.TotalSeconds)
        {
            mostCommonSubgenres = null;
            mostCommonArtists = null;
            GenreNode.All = new List<GenreNode>();

            await GetValuesFromSheet(context);

            _lastTime = DateTime.UtcNow;
            Log.Information("Cache revalidation took {Milliseconds}ms", DateTime.UtcNow.Subtract(now).TotalMilliseconds);
        }
    }

    private static GenreNode? rootNode;

    private async Task GetValuesFromSheet(DynamicContext context)
    {
        _entries = new List<Entry>();
        var values = await GetEntriesFromSheets(context, "'2020-2024'!A2:O", "'2015-2019'!A2:O", "'2010-2014'!A2:O", "'Pre-2010s'!A2:O", "'Genreless'!A2:O", "'Genre Colors'!A2:B", "'Genre Tree'!A2:E");
        if (values != null)
            _entries.AddRange(values);
    }

    private async Task<List<Entry>?> GetEntriesFromSheets(DynamicContext context, params string[] ranges)
    {
        var request = api.Spreadsheets.Values.BatchGet(SPREADSHEET_ID);
        request.Ranges = ranges;
        var response = await request.ExecuteAsync();

        var valueRanges = response.ValueRanges;
        if (valueRanges is null)
            return null;
        if (valueRanges.Count == 0)
            return null;

        var entries = new List<Entry>();

        foreach (var range in valueRanges)
        {
            if (range.Values is null)
                continue;
            if (range.Values.Count == 0)
                continue;

            var sheet = range.Range;
            var index = sheet.IndexOf("!", StringComparison.Ordinal);
            if (index >= 0)
                sheet = sheet[..index];

            if (sheet.StartsWith("'Genre Tree'"))
            {
                rootNode = graphService.ParseTree(range.Values, _genreColors);
                File.WriteAllText("tree.json", JsonConvert.SerializeObject(rootNode, Formatting.Indented));
            }
            else if (sheet.StartsWith("'Genre Colors"))
            {
                _genreColors.Clear();
                foreach (var row in range.Values)
                {
                    var genre = row[0] as string;
                    if (string.IsNullOrWhiteSpace(genre))
                    {
                        Log.Error("genre is null");
                        continue;
                    }

                    var hex = row[1] as string;
                    if (string.IsNullOrWhiteSpace(hex))
                    {
                        Log.Error("hex is null");
                        continue;
                    }

                    if (hex.StartsWith("#"))
                        hex = hex[1..];

                    if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexColor))
                    {
                        Log.Error("number is not a color");
                        continue;
                    }

                    var color = new Color(hexColor);
                    _genreColors.Add(genre, color);
                }
            }
            else
            {
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
                        await context.FollowupAsync($"parsing error at ({row.Count}) {string.Join(", ", row)}: {ex}");
                        return null;
                    }
                }
            }


        }

        return entries.Where(e => e.Genre != "Release").ToList();
    }

    private static readonly Dictionary<string, Color> _genreColors = new()
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

    private static Color GetGenreColor(string genre)
    {
        if (!_genreColors.TryGetValue(genre, out var color))
            color = Color.Default;
        return color;
    }

    public static readonly string[] DateFormat =
    {
        "yyyy'-'MM'-'dd"
    };

    public static readonly string[] TimeFormat =
    {
        "m':'ss" /*, "h:mm:ss"*/
    };

    private static readonly IRatioScorer _scorer = new TokenSetScorer();

    private static string[] GetArtists(string artist, MatchOptions matchOptions)
    {
        return matchOptions.MatchMode switch
        {
            MatchMode.Exact => _entries.SelectMany(e => e.ActualArtists).Distinct().Where(a => string.Equals(artist, a, StringComparison.OrdinalIgnoreCase)).ToArray(),
            MatchMode.Fuzzy => Process.ExtractTop(artist, _entries.SelectMany(e => e.ActualArtists).Distinct(), scorer: _scorer, cutoff: matchOptions.Threshold).OrderByDescending(a => a.Score).ThenBy(a => a.Value).Select(a => a.Value).ToArray(),
            _               => throw new ArgumentOutOfRangeException(nameof(matchOptions), matchOptions, null)
        };
    }

    private static bool EntryMatchFeatures(Entry entry, string[] toMatch, MatchOptions matchOptions)
    {
        return matchOptions.MatchMode switch
        {
            MatchMode.Exact => entry.Info.Features.Any(feature => toMatch.Any(artist => string.Equals(artist, feature, StringComparison.OrdinalIgnoreCase))),
            MatchMode.Fuzzy => entry.Info.Features.Any(feature => toMatch.Any(artist => fuzzyFunc(artist, feature, PreprocessMode.Full) >= matchOptions.Threshold)),
            _               => false
        };
    }

    private static bool EntryMatchRemixers(Entry entry, string[] toMatch, MatchOptions matchOptions)
    {
        return matchOptions.MatchMode switch
        {
            MatchMode.Exact => entry.Info.Remixers.Any(feature => toMatch.Any(artist => string.Equals(artist, feature, StringComparison.OrdinalIgnoreCase))),
            MatchMode.Fuzzy => entry.Info.Remixers.Any(feature => toMatch.Any(artist => fuzzyFunc(artist, feature, PreprocessMode.Full) >= matchOptions.Threshold)),
            _               => false
        };
    }

    private static bool EntryMatchArtist(Entry entry, string[] toMatch, MatchOptions matchOptions)
    {
        return matchOptions.MatchMode switch
        {
            MatchMode.Exact => entry.Info.Artists.Any(feature => toMatch.Any(artist => string.Equals(artist, feature, StringComparison.OrdinalIgnoreCase))),
            MatchMode.Fuzzy => entry.Info.Artists.Any(feature => toMatch.Any(artist => fuzzyFunc(artist, feature, PreprocessMode.Full) >= matchOptions.Threshold)),
            _               => false
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="artists"></param>
    /// <param name="matchOptions"></param>
    /// <param name="includeRemixes">Include XXX (artist Remix)</param>
    /// <param name="includeRemixed">Include artist (XXX Remix)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static List<Entry> GetAllTracksByArtist(string[] artists, MatchOptions matchOptions, bool includeRemixes = true, bool includeRemixed = false)
    {
        var tracks = new List<Entry>();

        foreach (var entry in _entries)
        {
            // include featured artists
            if (EntryMatchFeatures(entry, artists, matchOptions))
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
                    if (EntryMatchRemixers(entry, artists, matchOptions))
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
                    if (EntryMatchArtist(entry, artists, matchOptions))
                    {
                        tracks.Add(entry);
                    }
                }
            }

            // track is not a remix
            if (entry.Info.Remixers.Count < 1)
            {
                // track is by searching artist
                if (EntryMatchArtist(entry, artists, matchOptions))
                {
                    tracks.Add(entry);
                }
            }
        }

        return tracks.OrderByDescending(e => e.Date).ToList();
    }

    private static List<Entry> GetAllTracksByArtist(string artist, MatchOptions matchOptions, bool includeRemixes = true, bool includeRemixed = false)
    {
        return GetAllTracksByArtist(new[]
        {
            artist
        }, matchOptions, includeRemixes, includeRemixed);
    }

    private static List<Entry> GetAllTracksByArtistExact(string artist)
    {
        return _entries.Where(e => string.Equals(e.OriginalArtists, artist, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList();
    }

    private static List<Entry> GetTracksByTitle(List<Entry> tracks, string title, MatchOptions matchOptions)
    {
        return matchOptions.MatchMode switch
        {
            MatchMode.Exact => tracks.Where(e => string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList(),
            MatchMode.Fuzzy => tracks.Where(e => Fuzz.Ratio(e.Title, title, PreprocessMode.Full) >= matchOptions.Threshold).OrderByDescending(e => e.Date).ToList(),
            _               => throw new ArgumentOutOfRangeException(nameof(matchOptions), matchOptions, null)
        };
    }

    private static List<Entry> GetTracksByTitle(string title, MatchOptions matchOptions)
    {
        return GetTracksByTitle(_entries, title, matchOptions);
    }

    private static Entry[] GetAllTracksByLabelFuzzy(string label, int threshold = 80)
    {
        var test = GetLabelNameFuzzy(label, threshold);
        return _entries.Where(e => e.LabelList.Any(s => string.Equals(s, test, StringComparison.OrdinalIgnoreCase))).OrderByDescending(e => e.Date).ToArray();
    }

    private static string[] GetAllLabelNames()
    {
        return _entries.SelectMany(e => e.LabelList).Distinct().ToArray();
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

    public static string[] GetAllSubgenres()
    {
        return _entries.SelectMany(e => e.SubgenresList).Distinct().ToArray();
    }

    private static Dictionary<string, int>? mostCommonSubgenres;

    public static string[] GetMostCommonSubgenres()
    {
        if (mostCommonSubgenres != null)
            return mostCommonSubgenres.OrderByDescending(c => c.Value).Select(c => c.Key).ToArray();

        var count = new Dictionary<string, int>();
        foreach (var subgenre in _entries.SelectMany(entry => entry.SubgenresList))
        {
            if (!count.ContainsKey(subgenre))
                count.Add(subgenre, 0);
            count[subgenre]++;
        }

        mostCommonSubgenres = count;
        return mostCommonSubgenres.OrderByDescending(c => c.Value).Select(c => c.Key).ToArray();
    }

    private static Dictionary<string, int>? mostCommonArtists;

    public static string[] GetMostCommonArtists()
    {
        if (mostCommonArtists != null)
            return mostCommonArtists.OrderByDescending(c => c.Value).Select(c => c.Key).ToArray();

        var count = new Dictionary<string, int>();
        foreach (var artist in _entries.SelectMany(entry => entry.ActualArtists))
        {
            if (!count.ContainsKey(artist))
                count.Add(artist, 0);
            count[artist]++;
        }

        mostCommonArtists = count;
        return mostCommonArtists.OrderByDescending(c => c.Value).Select(c => c.Key).ToArray();
    }

    private static async Task SendTrackEmbed(DynamicContext context, Entry[] tracks)
    {
        var embeds = new List<Embed>();
        foreach (var track in tracks)
        {
            var fields = new List<EmbedFieldBuilder>
            {
                new EmbedFieldBuilder().WithName("Artists").WithValue(track.FormattedArtists).WithIsInline(true),
                new EmbedFieldBuilder().WithName("Song Title").WithValue(track.Title).WithIsInline(true)
            };
            if (track.Length != null)
                fields.Add(new EmbedFieldBuilder().WithName("Length").WithValue(track.Length.Value.ToString(TimeFormat[0])).WithIsInline(true));

            fields.Add(new EmbedFieldBuilder().WithName("Primary Label").WithValue(string.Join(", ", track.LabelList)).WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Date").WithValue(track.Date.ToString(DateFormat[0])).WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Genre").WithValue(track.Subgenres).WithIsInline(true));

            if (!string.IsNullOrWhiteSpace(track.Bpm))
            {
                fields.Add(new EmbedFieldBuilder().WithName("BPM").WithValue($"{track.Bpm} {BoolToEmoji(track.CorrectBpm)}").WithIsInline(true));
            }

            if (!string.IsNullOrWhiteSpace(track.Key))
            {
                fields.Add(new EmbedFieldBuilder().WithName("Key").WithValue($"{track.Key} {BoolToEmoji(track.CorrectKey)}").WithIsInline(true));
            }

            fields.Add(new EmbedFieldBuilder().WithName("Spotify").WithValue(BoolToEmoji(track.Spotify)).WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("SoundCloud").WithValue(BoolToEmoji(track.SoundCloud)).WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("Beatport").WithValue(BoolToEmoji(track.Beatport)).WithIsInline(true));

            embeds.Add(new EmbedBuilder().WithColor(GetGenreColor(track.Genre)).WithFields(fields).Build());
        }

        await context.FollowupAsync(embeds: embeds.ToArray());
    }

    private static string BoolToEmoji(bool value)
    {
        if (value)
        {
            return "✅";
        }

        return "❌";
    }

    private static async Task SendTrackInfoEmbed(DynamicContext context, TrackInfo info)
    {
        var fields = new List<EmbedFieldBuilder>
        {
            new EmbedFieldBuilder().WithName("Artists").WithValue(string.Join(", ", info.Artists)).WithIsInline(true),
            new EmbedFieldBuilder().WithName("Song Title").WithValue(info.ProcessedTitle).WithIsInline(true),
        };
        if (info.Features.Count > 0)
            fields.Add(new EmbedFieldBuilder().WithName("Features").WithValue(string.Join(", ", info.Features)).WithIsInline(true));
        if (info.Remixers.Count > 0)
            fields.Add(new EmbedFieldBuilder().WithName("Remixers").WithValue(string.Join(", ", info.Remixers)).WithIsInline(true));
        fields.Add(new EmbedFieldBuilder().WithName("Date").WithValue(info.ScrobbledDate.ToString(DateFormat[0])).WithIsInline(true));

        var builder = new EmbedBuilder().WithFields(fields).Build();

        await context.FollowupAsync(embed: builder);
        //await Context.Message.ReplyAsync(null, false, builder);
    }

    private static async Task SendTrackList(DynamicContext context, string search, string[] artists, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
    {
        var sb = BuildTrackList(search, artists, tracks, includeGenreless, numLatest, numEarliest, includeIndex, includeArtist, includeTitle, includeLabel, includeDate);
        await context.FollowupAsync(sb.ToString());
        //await Context.Message.ReplyAsync(sb.ToString());
    }

    private static StringBuilder BuildTrackList(string search, string[] artists, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
    {
        var genrelessCount = 0;

        if (!includeGenreless)
        {
            genrelessCount = tracks.Count(e => e.Sheet == "Genreless");
            tracks = tracks.Where(t => t.Sheet != "Genreless").ToList();
        }

        var latestTracks = tracks.Where(e => e.Date <= DateTime.UtcNow).OrderByDescending(e => e.Date).Take(numLatest).ToArray();
        var earliestTracks = tracks.Where(e => e.Date <= DateTime.UtcNow).Reverse().Take(numEarliest).ToArray();

        var sb = new StringBuilder($"`{search}` has {tracks.Count} tracks");

        var futureTracks = tracks.Where(e => e.Date > DateTime.UtcNow).ToArray();

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
                             From = (int)(d / range) * range + 1,
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
                           .Where(k => k.Key != "Release")
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

    private static StringBuilder BuildTopSubgenreList(Entry[] tracks, int top, bool ignoreUnknown, out int topSubgenre)
    {
        var genres = tracks.SelectMany(t => t.SubgenresList)
                           .GroupBy(d => d, d => d, (d, e) => new KeyCount
                           {
                               Key = d,
                               Count = e.Count()
                           })
                           .Where(k => k.Key != "Release")
                           .Where(d => ignoreUnknown && d.Key != "?")
                           .OrderByDescending(d => d.Count)
                           .ThenBy(d => d.Key)
                           .Take(top)
                           .ToArray();
        topSubgenre = genres.Length;
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
        var topList = artistCount.Take(top).ToArray();
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
                var notFound = track.ArtistsList.Where(artist => !artists.Contains(artist)).ToArray();

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

        return sb.ToString().Trim();
    }

    private static string IsWas(DateTime date, DateTime compare)
    {
        return date.CompareTo(compare) > 0 ? "is" : "was";
    }

    private static async Task SendArtistInfo(DynamicContext context, string search, string[] artists, List<Entry> tracks)
    {
        var latest = tracks.First();
        var earliest = tracks.Last();
        var now = DateTime.UtcNow;

        var embed = new EmbedBuilder()
                    .WithTitle(string.Join(", ", artists))
                    .WithDescription($"`{search}` matches the artists {string.Join(", ", artists)}. The latest track {IsWas(latest.Date, now)} **{latest.Title} ({latest.Date:Y})**, and the first track {IsWas(earliest.Date, now)} **{earliest.Title} ({earliest.Date:Y})**")
                    .AddField("Tracks", BuildTrackList(search, artists, tracks, includeArtist: false).ToString())
                    .AddField("Genres", BuildTopGenreList(tracks.ToArray(), 5).ToString(), true)
                    .WithFooter(builder => builder.WithText($"refetch in {(int)(_lastTime.Value.Add(expiryTime).Subtract(DateTime.UtcNow)).TotalSeconds}s"));

        await context.FollowupAsync(embed: embed.Build());
    }


    private static (Dictionary<string, List<string>>, Dictionary<string, Entry[]>) ByGenre(string[] subgenres)
    {
        var toMostCommon = new Dictionary<string, List<string>>();
        var toEntries = new Dictionary<string, Entry[]>();

        foreach (var subgenre in subgenres)
        {
            var entries = _entries.Where(e => e.SubgenresList.Contains(subgenre)).ToArray();

            if (!toEntries.ContainsKey(subgenre))
                toEntries.Add(subgenre, entries);

            var genres = entries.Where(e => e.SubgenresList.First() == subgenre).Select(e => e.Genre).ToArray();
            var mostCommon = genres.GroupBy(g => g).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
            if (mostCommon == "?" || string.IsNullOrWhiteSpace(mostCommon))
                mostCommon = "Unknown";
            mostCommon = mostCommon.Replace(' ', '\0');
            mostCommon = mostCommon.Replace('[', '\0');
            mostCommon = mostCommon.Replace(']', '\0');
            mostCommon = mostCommon.Replace('&', 'N');

            if (!toMostCommon.ContainsKey(mostCommon))
                toMostCommon.Add(mostCommon, new List<string>());
            toMostCommon[mostCommon].Add(subgenre);
        }

        return (toMostCommon, toEntries);
    }

    public class MatchOptions
    {
        public MatchMode MatchMode { get; set; }

        public int Threshold { get; set; }
    }

#region Track

    public const string CMD_TRACK_NAME = "track";
    public const string CMD_TRACK_EXACT_NAME = "trackexact";
    public const string CMD_TRACK_DESCRIPTION = "Search for tracks on the sheet";
    public const string CMD_TRACK_SEARCH_DESCRIPTION = "Track to search for";
    public const string CMD_TRACK_TITLE_DESCRIPTION = "Filter all tracks by title";
    public const string CMD_TRACK_ARTIST_DESCRIPTION = "Filter tracks by artist";
    public const string CMD_TRACK_MATCH_DESCRIPTION = "todo";
    public const string CMD_TRACK_THRESHOLD_DESCRIPTION = "todo";

    public async Task TrackCommand(string? artist, string title, MatchOptions matchOptions, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        List<Entry> tracks;

        if (string.IsNullOrWhiteSpace(artist))
        {
            tracks = GetTracksByTitle(title, matchOptions);
        }
        else
        {
            var artists = GetArtists(artist, matchOptions);
            var tracksByArtist = GetAllTracksByArtist(artists, matchOptions);

            if (tracksByArtist.Count == 0)
            {
                await context.ErrorAsync($"no tracks found by artist `{artist}`");
                return;
            }

            tracks = GetTracksByTitle(tracksByArtist, title, matchOptions);

            if (tracks.Count == 0)
            {
                await context.FollowupAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
                return;
            }
        }

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"pissed left pant");
            return;
        }

        foreach (var chunk in tracks.Chunk(10))
        {
            await SendTrackEmbed(context, chunk);
        }
    }

#endregion

#region Track Info Exact (merge)

    public const string CMD_TRACK_INFO_EXACT_NAME = "trackinfoexact";
    public const string CMD_TRACK_INFO_EXACT_DESCRIPTION = "Search for tracks on the sheet";
    public const string CMD_TRACK_INFO_EXACT_SEARCH_DESCRIPTION = "Track to search for";


    public async Task TrackInfoExactCommand(string search, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var split = search.Split(new[]
        {
            " - "
        }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length != 2)
        {
            await context.ErrorAsync($"cannot parse `{search}` into `Artist - Title` [{string.Join(", ", split)}]");
            //await Context.Message.ReplyAsync($"cannot parse `{search}` into `Artist - Title` [{string.Join(", ", split)}]");
            return;
        }

        var artist = split[0];
        var tracksByArtist = GetAllTracksByArtistExact(artist);

        if (tracksByArtist.Count == 0)
        {
            await context.ErrorAsync($"no tracks found by artist `{artist}`");
            //await Context.Message.ReplyAsync($"no tracks found by artist `{artist}`");
            return;
        }

        var title = split[1];
        var tracks = GetTracksByTitle(tracksByArtist, title, null);

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
            //await Context.Message.ReplyAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
            return;
        }

        foreach (var track in tracks)
        {
            var info = TrackParser.GetTrackInfo(track.FormattedArtists, track.Title, null, null, track.Date);
            await SendTrackInfoEmbed(context, info);
        }
    }

#endregion

#region Track Info Force (merge)

    public const string CMD_TRACK_INFO_FORCE_NAME = "trackinfoforce";
    public const string CMD_TRACK_INFO_FORCE_DESCRIPTION = "Get information about a track";
    public const string CMD_TRACK_INFO_FORCE_SEARCH_DESCRIPTION = "Track to search for";

    public async Task TrackInfoForceCommand(string search, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        var split = search.Split(new[]
        {
            " - "
        }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length < 2)
        {
            await context.ErrorAsync($"cannot parse `{search}` into `Artist - Title` [{string.Join(", ", split)}]");
            //await Context.Message.ReplyAsync($"cannot parse `{search}` into `Artist - Title` [{string.Join(", ", split)}]");
            return;
        }

        var index = search.IndexOf(" - ", StringComparison.Ordinal);
        var artist = search.Substring(0, index);
        var title = search.Substring(3 + index);
        var info = TrackParser.GetTrackInfo(artist, title, null, null, DateTime.UtcNow);
        await SendTrackInfoEmbed(context, info);
    }

#endregion

#region Artist

    public const string CMD_ARTIST_NAME = "artist";
    public const string CMD_ARTIST_EXACT_NAME = "artistexact";
    public const string CMD_ARTIST_DESCRIPTION = "Returns info about an artist";
    public const string CMD_ARTIST_SEARCH_DESCRIPTION = "Artist to search for";
    public const string CMD_ARTIST_MATCH_DESCRIPTION = "todo";
    public const string CMD_ARTIST_THRESHOLD_DESCRIPTION = "todo";

    public async Task ArtistCommand(string artist, MatchOptions matchOptions, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var artists = GetArtists(artist, matchOptions);
        var tracksByArtist = GetAllTracksByArtist(artists, matchOptions, true, false);

        if (tracksByArtist.Count == 0)
        {
            await context.ErrorAsync($"no tracks found by artist `{artist}`");
            return;
        }

        await SendArtistInfo(context, artist, artists, tracksByArtist);
    }

#endregion

#region Artist Debug

    public const string CMD_ARTIST_DEBUG_NAME = "artistdebug";
    public const string CMD_ARTIST_DEBUG_DESCRIPTION = "Returns a list of up to 15 artists most similar to the given input";
    public const string CMD_ARTIST_DEBUG_SEARCH_DESCRIPTION = "Artist to search for";

    public async Task ArtistDebugCommand(string artist, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var artists = Process.ExtractTop(artist, _entries.SelectMany(e => e.ActualArtists).Distinct(), scorer: _scorer, cutoff: 80).ToArray();

        var sb = new StringBuilder($"{artists.Length} most similar artists (using {_scorer.GetType().Name})\r\n");

        for (var i = 0; i < artists.Length; i++)
        {
            var track = artists[i];
            sb.AppendLine($"{Array.IndexOf(artists, track) + 1}. `{track.Value}` {track.Score}% similar");
        }

        await context.FollowupAsync(sb.ToString());
        //await Context.Message.ReplyAsync(sb.ToString());
    }

#endregion

#region Genre

    public const string CMD_GENRE_NAME = "genre";
    public const string CMD_GENRE_DESCRIPTION = "Returns a list of up to 8 tracks of a given genre";
    public const string CMD_GENRE_SEARCH_DESCRIPTION = "Genre to search for";

    public async Task GenreCommand(string genre, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var genres = _entries.Select(e => e.Genre).Distinct().ToArray();
        var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

        if (test is null)
        {
            await context.FollowupAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
            //await Context.Message.ReplyAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
            return;
        }

        var tracks = _entries.Where(e => e.Sheet != "Genreless" && string.Equals(e.Genre, test, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList();

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"No tracks with genre `{test}` found");
            //await Context.Message.ReplyAsync($"No tracks with genre `{test}` found");
            return;
        }

        await SendTrackList(context, test, new[]
        {
            test
        }, tracks, false);
    }

#endregion

#region Genre Info

    public const string CMD_GENRE_INFO_NAME = "genreinfo";
    public const string CMD_GENRE_INFO_DESCRIPTION = "Returns information of a genre";
    public const string CMD_GENRE_INFO_SEARCH_DESCRIPTION = "Genre to search for";

    public async Task GenreInfoCommand(string genre, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var genres = _entries.Select(e => e.Genre).Distinct().ToArray();
        var search = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

        if (search is null)
        {
            await context.FollowupAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
            //await Context.Message.ReplyAsync($"Genre `{genre}` not found. Here is every genre I found: {string.Join(", ", genres)}");
            return;
        }

        var tracks = _entries.Where(e => string.Equals(e.Genre, search, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToArray();

        if (tracks.Length == 0)
        {
            await context.FollowupAsync($"No tracks with genre `{search}` found");
            //await Context.Message.ReplyAsync($"No tracks with genre `{search}` found");
            return;
        }

        var description = BuildTopNumberOfTracksList(tracks, 10, out var top, out var numArtists);
        var bpmList = BuildBpmList(tracks, 20);

        var subgenres = BuildTopSubgenreList(tracks, 10, true, out var topSubgenre);

        var earliest = tracks.Last();
        var latest = tracks.First();
        var now = DateTime.Now;

        var color = GetGenreColor(search);
        var embed = new EmbedBuilder().WithTitle(search).WithDescription($"We have {tracks.Length} {search} tracks, from {numArtists} artists.\r\n" + $"The first track {IsWas(earliest.Date, now)} on {earliest.Date:Y} by {earliest.FormattedArtists} and the latest {IsWas(latest.Date, now)} on {latest.Date:Y} by {latest.FormattedArtists}").WithColor(color).AddField($"Top {top} Artists", description.ToString(), true).AddField($"Top {topSubgenre} Subgenres", subgenres.ToString(), true).AddField("BPM", bpmList.ToString(), true);

        await context.FollowupAsync(embed: embed.Build());
        //await Context.Message.ReplyAsync(embed: embed.Build());
    }

#endregion

#region Subgenre (merge)

    public const string CMD_SUBGENRE_NAME = "subgenre";
    public const string CMD_SUBGENRE_DESCRIPTION = "Returns a list of up to 8 tracks of a given subgenre";
    public const string CMD_SUBGENRE_SEARCH_DESCRIPTION = "Subgenre to search for";

    public async Task SubgenreCommand(string genre, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var genres = GetAllSubgenres();
        var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

        if (test is null)
        {
            await context.FollowupAsync($"Subgenre `{genre}` not found");
            //await Context.Message.ReplyAsync($"Subgenre `{genre}` not found");
            return;
        }

        var tracks = _entries.Where(e => e.SubgenresList.Contains(test, StringComparer.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList();

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"No tracks with genre `{test}` found");
            //await Context.Message.ReplyAsync($"No tracks with genre `{test}` found");
            return;
        }

        await SendTrackList(context, test, new[]
        {
            test
        }, tracks, false);
    }

#endregion

#region Subgenre Exact (merge)

    public const string CMD_SUBGENRE_EXACT_NAME = "subgenreexact";
    public const string CMD_SUBGENRE_EXACT_DESCRIPTION = "Returns a list of up to 8 tracks of a given subgenre";
    public const string CMD_SUBGENRE_EXACT_SEARCH_DESCRIPTION = "Subgenre to search for";

    public async Task SubgenreExactCommand(string genre, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var genres = _entries.Select(e => e.Subgenres).Distinct().ToArray();
        var test = genres.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));

        if (test is null)
        {
            await context.FollowupAsync($"Genre `{genre}` not found");
            //await Context.Message.ReplyAsync($"Genre `{genre}` not found");
            return;
        }

        var tracks = _entries.Where(e => string.Equals(e.Subgenres, test, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList();

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"No tracks with genre `{test}` found");
            //await Context.Message.ReplyAsync($"No tracks with genre `{test}` found");
            return;
        }

        await SendTrackList(context, test, new[]
        {
            test
        }, tracks, false);
    }

#endregion

#region Labels

    public const string CMD_LABELS_NAME = "labels";
    public const string CMD_LABELS_DESCRIPTION = "List of every label";

    public async Task LabelsCommand(DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var labels = _entries.SelectMany(e => e.LabelList)
                             .GroupBy(l => l, e => e, (label, ls) =>
                             {
                                 var tracks = _entries.Where(e => e.LabelList.Contains(label)).ToArray();
                                 return new
                                 {
                                     Key = label,
                                     ArtistCount = tracks.SelectMany(e => e.ActualArtists).Distinct().Count(),
                                     TrackCount = tracks.Length
                                 };
                             })
                             .OrderByDescending(a => a.TrackCount)
                             .ThenByDescending(a => a.ArtistCount)
                             .ThenBy(a => a.Key)
                             .ToList();

        await context.SendOrAttachment(string.Join("\r\n", labels.Select(l => $"{l.Key} ({l.TrackCount} tracks, {l.ArtistCount} artists)")));
    }

#endregion

#region Label

    public const string CMD_LABEL_NAME = "label";
    public const string CMD_LABEL_DESCRIPTION = "Embed with information";
    public const string CMD_LABEL_SEARCH_DESCRIPTION = "Label to search for";

    public async Task LabelCommand(string label, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var test = GetLabelNameFuzzy(label);

        if (string.IsNullOrWhiteSpace(test))
        {
            await context.FollowupAsync($"Cannot find the label `{label}`");
            //await Context.Message.ReplyAsync($"Cannot find the label `{label}`");
            return;
        }

        var tracks = GetAllTracksByLabelFuzzy(test);

        if (tracks.Length < 1)
        {
            await context.FollowupAsync($"Cannot find any tracks by the label `{test}`");
            //await Context.Message.ReplyAsync($"Cannot find any tracks by the label `{test}`");
            return;
        }

        var latest = tracks.First();
        var earliest = tracks.Last();
        var now = DateTime.UtcNow;
        var days = Math.Floor(now.Date.Subtract(earliest.Date).TotalDays);

        var numArtists = tracks.SelectMany(t => t.ActualArtists).Distinct().Count();

        var embed = new EmbedBuilder().WithTitle(test).WithDescription($"{test}'s latest release {IsWas(latest.Date, now)} on {latest.Date.ToString(DateFormat[0])} by {latest.FormattedArtists}, and their first release {IsWas(earliest.Date, now)} on {earliest.Date.ToString(DateFormat[0])} by {earliest.FormattedArtists}").AddField("Tracks", tracks.Length, true).AddField("Artists", numArtists, true).AddField("Years active", days <= 0 ? "Not yet active" : $"{Math.Floor(days / 365)} years and {days % 365} days", true).AddField("Genres", BuildTopGenreList(tracks, 5).ToString(), true);

        if (File.Exists($"logo_{test}.jpg"))
            embed = embed.WithThumbnailUrl($"https://raw.githubusercontent.com/Xythium/SubgenreSheetBot/master/SubgenreSheetBot/logo_{HttpUtility.UrlPathEncode(test)}.jpg");

        await context.FollowupAsync(embed: embed.Build());
        //await Context.Message.ReplyAsync(embed: embed.Build());
    }

#endregion

#region Label Artists

    public const string CMD_LABEL_ARTISTS_NAME = "labelartists";
    public const string CMD_LABEL_ARTISTS_DESCRIPTION = "List of artists on a label";
    public const string CMD_LABEL_ARTISTS_SEARCH_DESCRIPTION = "Label to search for";

    public async Task LabelArtistsCommand(string label, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var test = GetLabelNameFuzzy(label);

        if (string.IsNullOrWhiteSpace(test))
        {
            //await Context.Message.ReplyAsync($"Cannot find the label `{label}`");
            return;
        }

        var tracks = GetAllTracksByLabelFuzzy(test);

        if (tracks.Length < 1)
        {
            await context.FollowupAsync($"Cannot find any tracks by the label `{test}`");
            //await Context.Message.ReplyAsync($"Cannot find any tracks by the label `{test}`");
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

        await context.SendOrAttachment(sb.ToString());
    }

#endregion

#region Debug

    public const string CMD_DEBUG_NAME = "debug";
    public const string CMD_DEBUG_DESCRIPTION = "debug";

    public async Task DebugCommand(DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var subgenres = _entries.Where(e => e.SubgenresList.Length > 1)
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

        await context.FollowupAsync(sb.ToString());
        //await Context.Message.ReplyAsync(sb.ToString());
    }

#endregion

#region Markwhen

    public const string CMD_MARKWHEN_NAME = "markwhen";
    public const string CMD_MARKWHEN_DESCRIPTION = "markwhen";

    public async Task MarkwhenCommand(DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var sb = new StringBuilder($"title: Timeline\r\n\r\n");

        var subgenres = _entries.SelectMany(e => e.SubgenresList).Distinct().ToArray();

        var (mostCommonToSubgenre, subgenreToEntry) = ByGenre(subgenres);

        foreach (var (mostCommon, bySubgenres) in mostCommonToSubgenre)
        {
            sb.AppendLine($"group {mostCommon}");

            foreach (var subgenre in bySubgenres)
            {
                var entries = subgenreToEntry[subgenre];

                var dates = entries.Select(e => e.Date).OrderBy(d => d).ToArray();
                var first = dates.First();
                var last = dates.Last();

                if (first.ToString("yyyy-MM") == last.ToString("yyyy-MM"))
                    sb.AppendLine($"{first:yyyy-MM}: {subgenre} #{mostCommon}");
                else if (DateTime.UtcNow.Subtract(last) < TimeSpan.FromDays(90))
                    sb.AppendLine($"{first:yyyy-MM}/now: {subgenre} #{mostCommon}");
                else
                    sb.AppendLine($"{first:yyyy-MM}/{last:yyyy-MM}: {subgenre} #{mostCommon}");
            }

            sb.AppendLine("endGroup");
        }

        await context.SendOrAttachment(sb.ToString());
    }

#endregion

#region Query

    public const string CMD_QUERY_NAME = "query";
    public const string CMD_QUERY_DESCRIPTION = "Query information from the spreadsheet";
    public const string CMD_QUERY_ARTIST_DESCRIPTION = "Filter on artist names (Usage: `Name`)";
    public const string CMD_QUERY_ARTIST_COUNT_DESCRIPTION = "Filter on artist count (Usage: `>Count`, `<Count`, or `Count`)";
    public const string CMD_QUERY_SUBGENRE_DESCRIPTION = "Filter on subgenre (Usage: `-Subgenre` or `!Subgenre` for exclude, `Subgenre` for include)";
    public const string CMD_QUERY_SUBGENRE_COUNT_DESCRIPTION = "Filter on subgenre count (Usage: `>Count`, `<Count`, or `Count`)";
    public const string CMD_QUERY_LABEL_DESCRIPTION = "Filter on label (Usage: `-Name` or `!Name` for exclude, `Name` for include)";
    public const string CMD_QUERY_LABEL_COUNT_DESCRIPTION = "Filter on label count (Usage: `>Count`, `<Count`, or `Count`)";
    public const string CMD_QUERY_BEFORE_DESCRIPTION = "Filter on dates before (Usage: `Any date`)";
    public const string CMD_QUERY_AFTER_DESCRIPTION = "Filter on dates after (Usage: `Any date`)";
    public const string CMD_QUERY_DATE_DESCRIPTION = "Filter on exact date (Usage: `Any date`)";
    public const string CMD_QUERY_SELECT_DESCRIPTION = "What to return (Usage: `track`, `artist`, or `label`)";
    public const string CMD_QUERY_ORDER_DESCRIPTION = "How to order results (Usage: `date`, `title`, `label`, `artist`. `+` for asc, `-` for desc)";

    [Discord.Commands.NamedArgumentType]
    public class QueryArguments
    {
        public string? Artist { get; set; }

        public string? ArtistCount { get; set; }

        public string? Subgenre { get; set; }

        public string? SubgenreCount { get; set; } //todo

        public string? Label { get; set; }

        public string? LabelCount { get; set; }

        public string? Before { get; set; }

        public string? After { get; set; }

        public string? Date { get; set; }

        //meta
        public string? Select { get; set; }

        public string? Order { get; set; }
    }

    public async Task QueryCommand(QueryArguments arguments, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var query = _entries.AsQueryable();

        if (string.IsNullOrWhiteSpace(arguments.Artist) && string.IsNullOrWhiteSpace(arguments.ArtistCount) && string.IsNullOrWhiteSpace(arguments.Subgenre) && string.IsNullOrWhiteSpace(arguments.SubgenreCount) && string.IsNullOrWhiteSpace(arguments.Label) && string.IsNullOrWhiteSpace(arguments.LabelCount) && string.IsNullOrWhiteSpace(arguments.Before) && string.IsNullOrWhiteSpace(arguments.After) && string.IsNullOrWhiteSpace(arguments.Date))
        {
            await context.SendOrAttachment("No arguments specified");
            return;
        }

        if (!string.IsNullOrWhiteSpace(arguments.Artist))
        {
            query = query.Where(e => e.ActualArtists.Any(a => string.Equals(a, arguments.Artist, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(arguments.ArtistCount))
        {
            if (arguments.ArtistCount.StartsWith("<"))
            {
                var count = int.Parse(arguments.ArtistCount.Substring(1));
                query = query.Where(e => e.ActualArtists.Length < count);
            }
            else if (arguments.ArtistCount.StartsWith(">"))
            {
                var count = int.Parse(arguments.ArtistCount.Substring(1));
                query = query.Where(e => e.ActualArtists.Length > count);
            }
            else
            {
                var count = int.Parse(arguments.ArtistCount);
                query = query.Where(e => e.ActualArtists.Length == count);
            }
        }

        if (!string.IsNullOrWhiteSpace(arguments.Label))
        {
            if (arguments.Label.StartsWith("-") || arguments.Label.StartsWith("!"))
            {
                query = query.Where(e => e.LabelList.Any(a => !string.Equals(a, arguments.Label.Substring(1), StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                query = query.Where(e => e.LabelList.Any(a => string.Equals(a, arguments.Label, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (!string.IsNullOrWhiteSpace(arguments.LabelCount))
        {
            if (arguments.LabelCount.StartsWith("<"))
            {
                var count = int.Parse(arguments.LabelCount.Substring(1));
                query = query.Where(e => e.LabelList.Count < count);
            }
            else if (arguments.LabelCount.StartsWith(">"))
            {
                var count = int.Parse(arguments.LabelCount.Substring(1));
                query = query.Where(e => e.LabelList.Count > count);
            }
            else
            {
                var count = int.Parse(arguments.LabelCount);
                query = query.Where(e => e.LabelList.Count == count);
            }
        }

        if (!string.IsNullOrWhiteSpace(arguments.Before))
        {
            if (!DateOnly.TryParse(arguments.Before, out var before))
                throw new ArgumentException("Invalid date");

            query = query.Where(e => e.Date != null && new DateOnly(e.Date.Year, e.Date.Month, e.Date.Day) < before);
        }

        if (!string.IsNullOrWhiteSpace(arguments.After))
        {
            if (!DateOnly.TryParse(arguments.After, out var after))
                throw new ArgumentException("Invalid date");

            query = query.Where(e => e.Date != null && new DateOnly(e.Date.Year, e.Date.Month, e.Date.Day) > after);
        }

        if (!string.IsNullOrWhiteSpace(arguments.Date))
        {
            if (!DateOnly.TryParse(arguments.Date, out var date))
                throw new ArgumentException("Invalid date");

            query = query.Where(e => e.Date != null && new DateOnly(e.Date.Year, e.Date.Month, e.Date.Day) == date);
        }

        if (!string.IsNullOrWhiteSpace(arguments.Subgenre))
        {
            if (arguments.Subgenre.StartsWith("-") || arguments.Subgenre.StartsWith("!"))
            {
                query = query.Where(e => e.SubgenresList.Any(a => !string.Equals(a, arguments.Subgenre.Substring(1), StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                query = query.Where(e => e.SubgenresList.Any(a => string.Equals(a, arguments.Subgenre, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (!string.IsNullOrWhiteSpace(arguments.Order))
        {
            switch (arguments.Order)
            {
                case "+date":
                    query = query.OrderBy(e => e.Date);
                    break;

                case "-date":
                    query = query.OrderByDescending(e => e.Date);
                    break;

                case "+title":
                    query = query.OrderBy(e => e.Title);
                    break;

                case "-title":
                    query = query.OrderByDescending(e => e.Title);
                    break;

                case "+label":
                case "+labels":
                    query = query.OrderBy(e => e.LabelList.FirstOrDefault());
                    break;

                case "-label":
                case "-labels":
                    query = query.OrderByDescending(e => e.LabelList.FirstOrDefault());
                    break;

                case "+artist":
                case "+artists":
                    query = query.OrderBy(e => e.FormattedArtists);
                    break;

                case "-artist":
                case "-artists":
                    query = query.OrderByDescending(e => e.FormattedArtists);
                    break;
            }
        }

        IQueryable<string>? strs = null;

        if (!string.IsNullOrWhiteSpace(arguments.Select))
        {
            switch (arguments.Select)
            {
                case "track": break;

                case "artist":
                case "artists":
                    strs = query.SelectMany(e => e.ActualArtists).Distinct();
                    break;

                case "label":
                case "labels":
                    strs = query.SelectMany(e => e.LabelList).Distinct();
                    break;
            }
        }

        if (strs?.Any() ?? false)
        {
            switch (arguments.Order)
            {
                case "asc":
                case "+":
                    strs = strs.OrderBy(e => e);
                    break;

                case "desc":
                case "-":
                    strs = strs.OrderByDescending(e => e);
                    break;
            }
        }

        var stringResults = strs?.ToArray();
        var entryResults = query.ToArray();

        if (stringResults?.Length > 0)
        {
            var sb = new StringBuilder($"Found {stringResults.Length} results:\r\n");

            sb.AppendLine($"{string.Join(", ", stringResults)}");

            await context.SendOrAttachment(sb.ToString());
        }
        else if (entryResults.Length > 0)
        {
            var sb = new StringBuilder($"Found {entryResults.Length} results:\r\n");

            foreach (var result in entryResults)
            {
                sb.AppendLine($"{string.Join(", ", result.ArtistsList)} - {result.Title} [{string.Join(", ", result.LabelList)}]");
            }

            await context.SendOrAttachment(sb.ToString());
        }
    }

#endregion

#region Subgenre Graph

    public const string CMD_SUBGENRE_GRAPH_NAME = "subgenre-graph";
    public const string CMD_SUBGENRE_GRAPH_DESCRIPTION = "todo";
    public const string CMD_SUBGENRE_GRAPH_SEARCH_DESCRIPTION = "todo";
    public const string CMD_SUBGENRE_GRAPH_ENGINE_DESCRIPTION = "todo";
    public const string CMD_SUBGENRE_GRAPH_MAXDEPTH_DESCRIPTION = "todo";

    public class SheetGraphCommandOptions
    {
        public string Subgenre { get; set; }

        public string Engine { get; set; }

        public int MaxSubgenreDepth { get; set; }
    }

    public async Task SubgenreGraphCommand(SheetGraphCommandOptions graphOptions, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var genres = GetAllSubgenres();
        var test = genres.FirstOrDefault(g => string.Equals(g, graphOptions.Subgenre, StringComparison.OrdinalIgnoreCase));

        if (test is null)
        {
            await context.FollowupAsync($"Subgenre `{graphOptions.Subgenre}` not found");
            return;
        }

        graphOptions.Subgenre = test;

        if (rootNode is null)
            throw new InvalidDataException("rootNode cant be null");

        var imageBytes = graphService.Render(rootNode, graphOptions);
        var image = new MemoryStream(imageBytes);
        await context.FollowupWithFileAsync(image, $"{test}.png");
    }

#endregion

#region Subgenre Debug

    public const string CMD_SUBGENRE_DEBUG_NAME = "subgenre-debug";
    public const string CMD_SUBGENRE_DEBUG_DESCRIPTION = "Search for tracks on the sheet";
    public const string CMD_SUBGENRE_DEBUG_SEARCH_DESCRIPTION = "todo";

    public async Task SubgenreDebugCommand(string subgenre, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        if (rootNode is null)
            throw new InvalidDataException("rootNode cant be null");

        var node = graphService.FindNode(rootNode, subgenre);
        if (node is null)
            throw new InvalidDataException("node not found");

        var res = $"""
Name = {node.Name}
Meta = {node.IsMeta}
Root = {node.IsRoot}
Subgenres = {string.Join(", ", node.Subgenres.Select(sg => sg.Name))}
""";
        await context.FollowupAsync(res);
    }

#endregion

#region Collab Graph

    public const string CMD_COLLAB_GRAPH_NAME = "collab-graph";
    public const string CMD_COLLAB_GRAPH_DESCRIPTION = "todo";
    public const string CMD_COLLAB_GRAPH_SEARCH_DESCRIPTION = "todo";
    public const string CMD_COLLAB_GRAPH_ENGINE_DESCRIPTION = "todo";
    public const string CMD_COLLAB_GRAPH_MAXDEPTH_DESCRIPTION = "todo";

    public class CollabGraphCommandOptions
    {
        public string StartArtist { get; set; }

        public string Engine { get; set; }

        public int MaxSubgenreDepth { get; set; }
    }

    public async Task CollabGraphCommand(CollabGraphCommandOptions graphOptions, MatchOptions matchOptions, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        if (graphOptions.MaxSubgenreDepth < 1)
        {
            await context.ErrorAsync("Depth should be at least 1");
            return;
        }

        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var tracks = GetAllTracksByArtist(graphOptions.StartArtist, matchOptions, false);
        if (tracks.Count == 0)
        {
            await context.FollowupAsync("No tracks by artist");
            return;
        }

        var artists = tracks.SelectMany(t => t.ActualArtists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (artists.Length < 2)
        {
            await context.FollowupAsync("No collaborations");
            return;
        }

        var node = new CollabNode
        {
            Name = graphOptions.StartArtist,
            IsRoot = true
        };

        AddArtistsToNode(artists, node, graphOptions.MaxSubgenreDepth, new Dictionary<string, CollabNode>(StringComparer.OrdinalIgnoreCase)
        {
            {
                graphOptions.StartArtist, node
            }
        });

        try
        {
            var imageBytes = graphService.RenderCollabs(node, graphOptions);
            var image = new MemoryStream(imageBytes);
            await context.FollowupWithFileAsync(image, $"{graphOptions.StartArtist}.png");
        }
        catch (Exception e)
        {
            await context.ErrorAsync(e.ToString());
        }

    }

    private static void AddArtistsToNode(string[] artists, CollabNode node, int depth, Dictionary<string, CollabNode> map)
    {
        if (depth < 1)
            return;

        foreach (var artist in artists)
        {
            if (string.Equals(artist, node.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            var existed = map.TryGetValue(artist, out var collab);
            if (!existed)
            {
                collab = new CollabNode
                {
                    Name = artist
                };

                map.Add(artist, collab);

                if (depth > 1)
                {
                    var tracks = GetAllTracksByArtist(artist, new MatchOptions
                    {
                        MatchMode = MatchMode.Exact
                    });
                    if (tracks.Count == 0)
                        continue;

                    var collabs = tracks.SelectMany(t => t.ActualArtists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    if (collabs.Length < 2)
                        continue;

                    AddArtistsToNode(collabs, collab!, depth - 1, map);
                }
            }

            node.AddSubgenre(collab!);
        }
    }

#endregion

#region MusicBrainz Submit

    static SortedSet<IRecording> _recordings = new(new MusicBrainzTrackComparer());
    static SortedSet<string> _addedLabels = new();

    public async Task MusicBrainzSubmitCommand(DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var sb = new StringBuilder("This would be submitted if Mark enabled it:\r\n");
        sb.AppendLine($"User Agent: {mbService.GetQuery().UserAgent}");

        var message = await context.FollowupAsync("recordings found: 0");
        //var message = await Context.Message.ReplyAsync("recordings found: 0");
        var found = 0;
        var lastSend = new DateTime(1970, 1, 1);

        var labels = GetAllLabelNames();

        foreach (var l in labels)
        {
            if (_addedLabels.Contains(l))
                continue;

            var label = (await mbService.GetQuery().FindLabelsAsync($"label:\"{l}\"", 1)).Results.FirstOrDefault()?.Item;

            if (label is null)
            {
                await context.FollowupAsync($"{l} not found");
                //await Context.Message.ReplyAsync($"{l} not found");
                continue;
            }

            var releases = await mbService.GetQuery().BrowseLabelReleasesAsync(label.Id, 100, inc: Include.Recordings | Include.ArtistCredits);

            foreach (var release in releases.Results)
            {
                if (release.Media is null)
                    continue;

                foreach (var medium in release.Media)
                {
                    if (medium.Tracks is null)
                        continue;

                    foreach (var track in medium.Tracks)
                    {
                        if (track.Recording != null)
                            _recordings.Add(track.Recording);
                    }
                }
            }

            _addedLabels.Add(l);
        }

        await context.FollowupAsync($"{_recordings.Count} tracks");
        //await Context.Message.ReplyAsync($"{_recordings.Count} tracks");

        var notFound = new List<Entry>();

        foreach (var entry in _entries)
        {
            if (entry.SubgenresList.SequenceEqual(new[]
            {
                "?"
            }))
                continue;

            var tags = mbService.GetQuery().SubmitTags(MusicBrainzService.CLIENT_ID);

            var recordings = _recordings.Where(t => string.Equals(t.Title, entry.Title, StringComparison.OrdinalIgnoreCase) && string.Equals(t.ArtistCredit?.First()?.Name, entry.ArtistsList.First(), StringComparison.OrdinalIgnoreCase)).Distinct(new MusicBrainzTrackComparer()).ToArray();

            if (recordings.Length > 0)
            {
                sb.AppendLine($"{entry.OriginalArtists} - {entry.Title}:");

                foreach (var recording in recordings)
                {
                    if (recording.ArtistCredit == null)
                        throw new NullReferenceException();
                    
                    sb.AppendLine($"\t{string.Join(" x ", recording.ArtistCredit.Select(ac => ac.Artist.Name))} - {recording.Title} ({recording.Id})");

                    found++;

                    if (DateTime.UtcNow.Subtract(lastSend).TotalSeconds > 10)
                    {
                        message = await context.UpdateOrSend(message, $"recordings found: {++found}");
                        lastSend = DateTime.UtcNow;
                    }

                    tags.Add(recording, TagVote.Up, entry.SubgenresList);
                    await tags.SubmitAsync();
                }

                sb.AppendLine($"\t\tTags: {string.Join(", ", entry.SubgenresList)}");
            }
            else
            {
                notFound.Add(entry);
            }
        }

        await context.SendOrAttachment(sb.ToString());

        if (notFound.Count > 0)
        {
            await context.SendOrAttachment(string.Join("\r\n", notFound.Select(t => $"{t.OriginalArtists} - {t.Title}")));
        }
    }

#endregion

#region Foobar

    public const string CMD_FOOBAR_NAME = "foobar";

    public const string CMD_FOOBAR_DESCRIPTION = "Foobar style sheet";

    /*
    $set_style(text,$rgb(255,255,255),$rgb(255,255,255))
$set_style(back,$rgb(0,0,0),$rgb(50,50,50))
    $if($or($stricmp($meta(genre,0),Ambient),$stricmp($meta(genre,0),Ambient Pop),$stricmp($meta(genre,0),Dark Ambient),$stricmp($meta(genre,0),New Age),$stricmp($meta(genre,0),Celtic New Age),$stricmp($meta(genre,0),Psybient),$stricmp($meta(genre,0),Space Ambient),$stricmp($meta(genre,0),Tribal Ambient),$stricmp($meta(genre,0),Atmospheric),$stricmp($meta(genre,0),Film Score),$stricmp($meta(genre,0),Lounge),$stricmp($meta(genre,0),Muzak)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Ambient),$stricmp($meta(genre,0),Ambient Pop),$stricmp($meta(genre,0),Dark Ambient),$stricmp($meta(genre,0),New Age),$stricmp($meta(genre,0),Celtic New Age),$stricmp($meta(genre,0),Psybient),$stricmp($meta(genre,0),Space Ambient),$stricmp($meta(genre,0),Tribal Ambient),$stricmp($meta(genre,0),Atmospheric),$stricmp($meta(genre,0),Film Score),$stricmp($meta(genre,0),Lounge),$stricmp($meta(genre,0),Muzak)),$set_style(back,$rgb(240,180,181)))
    $if($or($stricmp($meta(genre,0),Ambient?),$stricmp($meta(genre,0),Ambient Pop?),$stricmp($meta(genre,0),Dark Ambient?),$stricmp($meta(genre,0),New Age?),$stricmp($meta(genre,0),Celtic New Age?),$stricmp($meta(genre,0),Psybient?),$stricmp($meta(genre,0),Space Ambient?),$stricmp($meta(genre,0),Tribal Ambient?),$stricmp($meta(genre,0),Atmospheric?),$stricmp($meta(genre,0),Film Score?),$stricmp($meta(genre,0),Lounge?),$stricmp($meta(genre,0),Muzak?)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Ambient?),$stricmp($meta(genre,0),Ambient Pop?),$stricmp($meta(genre,0),Dark Ambient?),$stricmp($meta(genre,0),New Age?),$stricmp($meta(genre,0),Celtic New Age?),$stricmp($meta(genre,0),Psybient?),$stricmp($meta(genre,0),Space Ambient?),$stricmp($meta(genre,0),Tribal Ambient?),$stricmp($meta(genre,0),Atmospheric?),$stricmp($meta(genre,0),Film Score?),$stricmp($meta(genre,0),Lounge?),$stricmp($meta(genre,0),Muzak?)),$set_style(back,$rgb(240,180,181)))
    $if($or($stricmp($meta(genre,0),Bass),$stricmp($meta(genre,0),Halftime),$stricmp($meta(genre,0),Quartertime),$stricmp($meta(genre,0),Future Beats),$stricmp($meta(genre,0),Midtempo Bass),$stricmp($meta(genre,0),Slimepunk),$stricmp($meta(genre,0),Glitch Hop),$stricmp($meta(genre,0),Neurohop),$stricmp($meta(genre,0),Funkstep),$stricmp($meta(genre,0),Moombahton),$stricmp($meta(genre,0),Moombahcore),$stricmp($meta(genre,0),Freeform Bass)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Bass),$stricmp($meta(genre,0),Halftime),$stricmp($meta(genre,0),Quartertime),$stricmp($meta(genre,0),Future Beats),$stricmp($meta(genre,0),Midtempo Bass),$stricmp($meta(genre,0),Slimepunk),$stricmp($meta(genre,0),Glitch Hop),$stricmp($meta(genre,0),Neurohop),$stricmp($meta(genre,0),Funkstep),$stricmp($meta(genre,0),Moombahton),$stricmp($meta(genre,0),Moombahcore),$stricmp($meta(genre,0),Freeform Bass)),$set_style(back,$rgb(0,129,88)))
    $if($or($stricmp($meta(genre,0),Bass?),$stricmp($meta(genre,0),Halftime?),$stricmp($meta(genre,0),Quartertime?),$stricmp($meta(genre,0),Future Beats?),$stricmp($meta(genre,0),Midtempo Bass?),$stricmp($meta(genre,0),Slimepunk?),$stricmp($meta(genre,0),Glitch Hop?),$stricmp($meta(genre,0),Neurohop?),$stricmp($meta(genre,0),Funkstep?),$stricmp($meta(genre,0),Moombahton?),$stricmp($meta(genre,0),Moombahcore?),$stricmp($meta(genre,0),Freeform Bass?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Bass?),$stricmp($meta(genre,0),Halftime?),$stricmp($meta(genre,0),Quartertime?),$stricmp($meta(genre,0),Future Beats?),$stricmp($meta(genre,0),Midtempo Bass?),$stricmp($meta(genre,0),Slimepunk?),$stricmp($meta(genre,0),Glitch Hop?),$stricmp($meta(genre,0),Neurohop?),$stricmp($meta(genre,0),Funkstep?),$stricmp($meta(genre,0),Moombahton?),$stricmp($meta(genre,0),Moombahcore?),$stricmp($meta(genre,0),Freeform Bass?)),$set_style(back,$rgb(0,129,88)))
    $if($or($stricmp($meta(genre,0),Breaks),$stricmp($meta(genre,0),Acid Breaks),$stricmp($meta(genre,0),Chemical Breaks),$stricmp($meta(genre,0),Baltimore Club),$stricmp($meta(genre,0),Jersey Club),$stricmp($meta(genre,0),Broken Beat),$stricmp($meta(genre,0),Florida Breaks),$stricmp($meta(genre,0),Funky Breaks),$stricmp($meta(genre,0),Big Beat),$stricmp($meta(genre,0),Tribal Breaks),$stricmp($meta(genre,0),Nu-Skool Breaks),$stricmp($meta(genre,0),Progressive Breaks),$stricmp($meta(genre,0),Psybreaks)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Breaks),$stricmp($meta(genre,0),Acid Breaks),$stricmp($meta(genre,0),Chemical Breaks),$stricmp($meta(genre,0),Baltimore Club),$stricmp($meta(genre,0),Jersey Club),$stricmp($meta(genre,0),Broken Beat),$stricmp($meta(genre,0),Florida Breaks),$stricmp($meta(genre,0),Funky Breaks),$stricmp($meta(genre,0),Big Beat),$stricmp($meta(genre,0),Tribal Breaks),$stricmp($meta(genre,0),Nu-Skool Breaks),$stricmp($meta(genre,0),Progressive Breaks),$stricmp($meta(genre,0),Psybreaks)),$set_style(back,$rgb(10,24,87)))
    $if($or($stricmp($meta(genre,0),Breaks?),$stricmp($meta(genre,0),Acid Breaks?),$stricmp($meta(genre,0),Chemical Breaks?),$stricmp($meta(genre,0),Baltimore Club?),$stricmp($meta(genre,0),Jersey Club?),$stricmp($meta(genre,0),Broken Beat?),$stricmp($meta(genre,0),Florida Breaks?),$stricmp($meta(genre,0),Funky Breaks?),$stricmp($meta(genre,0),Big Beat?),$stricmp($meta(genre,0),Tribal Breaks?),$stricmp($meta(genre,0),Nu-Skool Breaks?),$stricmp($meta(genre,0),Progressive Breaks?),$stricmp($meta(genre,0),Psybreaks?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Breaks?),$stricmp($meta(genre,0),Acid Breaks?),$stricmp($meta(genre,0),Chemical Breaks?),$stricmp($meta(genre,0),Baltimore Club?),$stricmp($meta(genre,0),Jersey Club?),$stricmp($meta(genre,0),Broken Beat?),$stricmp($meta(genre,0),Florida Breaks?),$stricmp($meta(genre,0),Funky Breaks?),$stricmp($meta(genre,0),Big Beat?),$stricmp($meta(genre,0),Tribal Breaks?),$stricmp($meta(genre,0),Nu-Skool Breaks?),$stricmp($meta(genre,0),Progressive Breaks?),$stricmp($meta(genre,0),Psybreaks?)),$set_style(back,$rgb(10,24,87)))
    $if($or($stricmp($meta(genre,0),Country),$stricmp($meta(genre,0),Alternative Country),$stricmp($meta(genre,0),Contemporary Country),$stricmp($meta(genre,0),Country Blues),$stricmp($meta(genre,0),Progressive Country),$stricmp($meta(genre,0),Western Swing)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Country),$stricmp($meta(genre,0),Alternative Country),$stricmp($meta(genre,0),Contemporary Country),$stricmp($meta(genre,0),Country Blues),$stricmp($meta(genre,0),Progressive Country),$stricmp($meta(genre,0),Western Swing)),$set_style(back,$rgb(181,104,12)))
    $if($or($stricmp($meta(genre,0),Country?),$stricmp($meta(genre,0),Alternative Country?),$stricmp($meta(genre,0),Contemporary Country?),$stricmp($meta(genre,0),Country Blues?),$stricmp($meta(genre,0),Progressive Country?),$stricmp($meta(genre,0),Western Swing?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Country?),$stricmp($meta(genre,0),Alternative Country?),$stricmp($meta(genre,0),Contemporary Country?),$stricmp($meta(genre,0),Country Blues?),$stricmp($meta(genre,0),Progressive Country?),$stricmp($meta(genre,0),Western Swing?)),$set_style(back,$rgb(181,104,12)))
    $if($or($stricmp($meta(genre,0),Disco),$stricmp($meta(genre,0),Electro-Disco),$stricmp($meta(genre,0),Hi-NRG),$stricmp($meta(genre,0),Eurodisco),$stricmp($meta(genre,0),Italo Disco),$stricmp($meta(genre,0),Afro Disco),$stricmp($meta(genre,0),Latin Disco),$stricmp($meta(genre,0),Post-Disco),$stricmp($meta(genre,0),Boogie),$stricmp($meta(genre,0),Space Disco),$stricmp($meta(genre,0),Pop),$stricmp($meta(genre,0),Acoustic Pop),$stricmp($meta(genre,0),Afrobeats),$stricmp($meta(genre,0),Art Pop),$stricmp($meta(genre,0),Progressive Pop),$stricmp($meta(genre,0),Blue-Eyed Soul),$stricmp($meta(genre,0),Baroque Pop),$stricmp($meta(genre,0),Brill Building),$stricmp($meta(genre,0),Bubblegum Pop),$stricmp($meta(genre,0),Europop),$stricmp($meta(genre,0),K-Pop),$stricmp($meta(genre,0),Country Pop),$stricmp($meta(genre,0),Dancehall Pop),$stricmp($meta(genre,0),Folk Pop),$stricmp($meta(genre,0),Sunshine Pop),$stricmp($meta(genre,0),Indian Pop),$stricmp($meta(genre,0),Indie Pop),$stricmp($meta(genre,0),Chamber Pop),$stricmp($meta(genre,0),Twee Pop),$stricmp($meta(genre,0),Jazz Pop),$stricmp($meta(genre,0),J-Pop),$stricmp($meta(genre,0),Music Hall),$stricmp($meta(genre,0),Vaudeville),$stricmp($meta(genre,0),New Wave),$stricmp($meta(genre,0),Dancepop),$stricmp($meta(genre,0),Eurodance),$stricmp($meta(genre,0),Italo Dance),$stricmp($meta(genre,0),Freestyle),$stricmp($meta(genre,0),Synthpop),$stricmp($meta(genre,0),Bitpop),$stricmp($meta(genre,0),Electropop),$stricmp($meta(genre,0),Indie Dance),$stricmp($meta(genre,0),Piano Pop),$stricmp($meta(genre,0),Pop Ballad),$stricmp($meta(genre,0),Pop Rock),$stricmp($meta(genre,0),Pop Soul),$stricmp($meta(genre,0),Reggae Pop),$stricmp($meta(genre,0),Show Tune),$stricmp($meta(genre,0),Traditional Pop),$stricmp($meta(genre,0),Hyperpop)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Disco),$stricmp($meta(genre,0),Electro-Disco),$stricmp($meta(genre,0),Hi-NRG),$stricmp($meta(genre,0),Eurodisco),$stricmp($meta(genre,0),Italo Disco),$stricmp($meta(genre,0),Afro Disco),$stricmp($meta(genre,0),Latin Disco),$stricmp($meta(genre,0),Post-Disco),$stricmp($meta(genre,0),Boogie),$stricmp($meta(genre,0),Space Disco),$stricmp($meta(genre,0),Pop),$stricmp($meta(genre,0),Acoustic Pop),$stricmp($meta(genre,0),Afrobeats),$stricmp($meta(genre,0),Art Pop),$stricmp($meta(genre,0),Progressive Pop),$stricmp($meta(genre,0),Blue-Eyed Soul),$stricmp($meta(genre,0),Baroque Pop),$stricmp($meta(genre,0),Brill Building),$stricmp($meta(genre,0),Bubblegum Pop),$stricmp($meta(genre,0),Europop),$stricmp($meta(genre,0),K-Pop),$stricmp($meta(genre,0),Country Pop),$stricmp($meta(genre,0),Dancehall Pop),$stricmp($meta(genre,0),Folk Pop),$stricmp($meta(genre,0),Sunshine Pop),$stricmp($meta(genre,0),Indian Pop),$stricmp($meta(genre,0),Indie Pop),$stricmp($meta(genre,0),Chamber Pop),$stricmp($meta(genre,0),Twee Pop),$stricmp($meta(genre,0),Jazz Pop),$stricmp($meta(genre,0),J-Pop),$stricmp($meta(genre,0),Music Hall),$stricmp($meta(genre,0),Vaudeville),$stricmp($meta(genre,0),New Wave),$stricmp($meta(genre,0),Dancepop),$stricmp($meta(genre,0),Eurodance),$stricmp($meta(genre,0),Italo Dance),$stricmp($meta(genre,0),Freestyle),$stricmp($meta(genre,0),Synthpop),$stricmp($meta(genre,0),Bitpop),$stricmp($meta(genre,0),Electropop),$stricmp($meta(genre,0),Indie Dance),$stricmp($meta(genre,0),Piano Pop),$stricmp($meta(genre,0),Pop Ballad),$stricmp($meta(genre,0),Pop Rock),$stricmp($meta(genre,0),Pop Soul),$stricmp($meta(genre,0),Reggae Pop),$stricmp($meta(genre,0),Show Tune),$stricmp($meta(genre,0),Traditional Pop),$stricmp($meta(genre,0),Hyperpop)),$set_style(back,$rgb(22,172,176)))
    $if($or($stricmp($meta(genre,0),Disco?),$stricmp($meta(genre,0),Electro-Disco?),$stricmp($meta(genre,0),Hi-NRG?),$stricmp($meta(genre,0),Eurodisco?),$stricmp($meta(genre,0),Italo Disco?),$stricmp($meta(genre,0),Afro Disco?),$stricmp($meta(genre,0),Latin Disco?),$stricmp($meta(genre,0),Post-Disco?),$stricmp($meta(genre,0),Boogie?),$stricmp($meta(genre,0),Space Disco?),$stricmp($meta(genre,0),Pop?),$stricmp($meta(genre,0),Acoustic Pop?),$stricmp($meta(genre,0),Afrobeats?),$stricmp($meta(genre,0),Art Pop?),$stricmp($meta(genre,0),Progressive Pop?),$stricmp($meta(genre,0),Blue-Eyed Soul?),$stricmp($meta(genre,0),Baroque Pop?),$stricmp($meta(genre,0),Brill Building?),$stricmp($meta(genre,0),Bubblegum Pop?),$stricmp($meta(genre,0),Europop?),$stricmp($meta(genre,0),K-Pop?),$stricmp($meta(genre,0),Country Pop?),$stricmp($meta(genre,0),Dancehall Pop?),$stricmp($meta(genre,0),Folk Pop?),$stricmp($meta(genre,0),Sunshine Pop?),$stricmp($meta(genre,0),Indian Pop?),$stricmp($meta(genre,0),Indie Pop?),$stricmp($meta(genre,0),Chamber Pop?),$stricmp($meta(genre,0),Twee Pop?),$stricmp($meta(genre,0),Jazz Pop?),$stricmp($meta(genre,0),J-Pop?),$stricmp($meta(genre,0),Music Hall?),$stricmp($meta(genre,0),Vaudeville?),$stricmp($meta(genre,0),New Wave?),$stricmp($meta(genre,0),Dancepop?),$stricmp($meta(genre,0),Eurodance?),$stricmp($meta(genre,0),Italo Dance?),$stricmp($meta(genre,0),Freestyle?),$stricmp($meta(genre,0),Synthpop?),$stricmp($meta(genre,0),Bitpop?),$stricmp($meta(genre,0),Electropop?),$stricmp($meta(genre,0),Indie Dance?),$stricmp($meta(genre,0),Piano Pop?),$stricmp($meta(genre,0),Pop Ballad?),$stricmp($meta(genre,0),Pop Rock?),$stricmp($meta(genre,0),Pop Soul?),$stricmp($meta(genre,0),Reggae Pop?),$stricmp($meta(genre,0),Show Tune?),$stricmp($meta(genre,0),Traditional Pop?),$stricmp($meta(genre,0),Hyperpop?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Disco?),$stricmp($meta(genre,0),Electro-Disco?),$stricmp($meta(genre,0),Hi-NRG?),$stricmp($meta(genre,0),Eurodisco?),$stricmp($meta(genre,0),Italo Disco?),$stricmp($meta(genre,0),Afro Disco?),$stricmp($meta(genre,0),Latin Disco?),$stricmp($meta(genre,0),Post-Disco?),$stricmp($meta(genre,0),Boogie?),$stricmp($meta(genre,0),Space Disco?),$stricmp($meta(genre,0),Pop?),$stricmp($meta(genre,0),Acoustic Pop?),$stricmp($meta(genre,0),Afrobeats?),$stricmp($meta(genre,0),Art Pop?),$stricmp($meta(genre,0),Progressive Pop?),$stricmp($meta(genre,0),Blue-Eyed Soul?),$stricmp($meta(genre,0),Baroque Pop?),$stricmp($meta(genre,0),Brill Building?),$stricmp($meta(genre,0),Bubblegum Pop?),$stricmp($meta(genre,0),Europop?),$stricmp($meta(genre,0),K-Pop?),$stricmp($meta(genre,0),Country Pop?),$stricmp($meta(genre,0),Dancehall Pop?),$stricmp($meta(genre,0),Folk Pop?),$stricmp($meta(genre,0),Sunshine Pop?),$stricmp($meta(genre,0),Indian Pop?),$stricmp($meta(genre,0),Indie Pop?),$stricmp($meta(genre,0),Chamber Pop?),$stricmp($meta(genre,0),Twee Pop?),$stricmp($meta(genre,0),Jazz Pop?),$stricmp($meta(genre,0),J-Pop?),$stricmp($meta(genre,0),Music Hall?),$stricmp($meta(genre,0),Vaudeville?),$stricmp($meta(genre,0),New Wave?),$stricmp($meta(genre,0),Dancepop?),$stricmp($meta(genre,0),Eurodance?),$stricmp($meta(genre,0),Italo Dance?),$stricmp($meta(genre,0),Freestyle?),$stricmp($meta(genre,0),Synthpop?),$stricmp($meta(genre,0),Bitpop?),$stricmp($meta(genre,0),Electropop?),$stricmp($meta(genre,0),Indie Dance?),$stricmp($meta(genre,0),Piano Pop?),$stricmp($meta(genre,0),Pop Ballad?),$stricmp($meta(genre,0),Pop Rock?),$stricmp($meta(genre,0),Pop Soul?),$stricmp($meta(genre,0),Reggae Pop?),$stricmp($meta(genre,0),Show Tune?),$stricmp($meta(genre,0),Traditional Pop?),$stricmp($meta(genre,0),Hyperpop?)),$set_style(back,$rgb(22,172,176)))
    $if($or($stricmp($meta(genre,0),Drum and Bass),$stricmp($meta(genre,0),Atmospheric Drum and Bass),$stricmp($meta(genre,0),Drumfunk),$stricmp($meta(genre,0),Minimal Drum and Bass),$stricmp($meta(genre,0),Microfunk),$stricmp($meta(genre,0),Technoid),$stricmp($meta(genre,0),Hardstep),$stricmp($meta(genre,0),Techstep),$stricmp($meta(genre,0),Darkstep),$stricmp($meta(genre,0),Crossbreed),$stricmp($meta(genre,0),Skullstep),$stricmp($meta(genre,0),Deep Drum and Bass),$stricmp($meta(genre,0),Neurofunk),$stricmp($meta(genre,0),Trancestep),$stricmp($meta(genre,0),Jump Up),$stricmp($meta(genre,0),Drumstep),$stricmp($meta(genre,0),Experimental Drum and Bass),$stricmp($meta(genre,0),Experimental Drumstep),$stricmp($meta(genre,0),Jazzstep),$stricmp($meta(genre,0),Liquid Drum and Bass),$stricmp($meta(genre,0),Dancefloor Drum and Bass),$stricmp($meta(genre,0),Sambass),$stricmp($meta(genre,0),Jungle),$stricmp($meta(genre,0),Darkcore),$stricmp($meta(genre,0),Neo-Jungle),$stricmp($meta(genre,0),Ragga Jungle),$stricmp($meta(genre,0),Ragga Drum and Bass)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Drum and Bass),$stricmp($meta(genre,0),Atmospheric Drum and Bass),$stricmp($meta(genre,0),Drumfunk),$stricmp($meta(genre,0),Minimal Drum and Bass),$stricmp($meta(genre,0),Microfunk),$stricmp($meta(genre,0),Technoid),$stricmp($meta(genre,0),Hardstep),$stricmp($meta(genre,0),Techstep),$stricmp($meta(genre,0),Darkstep),$stricmp($meta(genre,0),Crossbreed),$stricmp($meta(genre,0),Skullstep),$stricmp($meta(genre,0),Deep Drum and Bass),$stricmp($meta(genre,0),Neurofunk),$stricmp($meta(genre,0),Trancestep),$stricmp($meta(genre,0),Jump Up),$stricmp($meta(genre,0),Drumstep),$stricmp($meta(genre,0),Experimental Drum and Bass),$stricmp($meta(genre,0),Experimental Drumstep),$stricmp($meta(genre,0),Jazzstep),$stricmp($meta(genre,0),Liquid Drum and Bass),$stricmp($meta(genre,0),Dancefloor Drum and Bass),$stricmp($meta(genre,0),Sambass),$stricmp($meta(genre,0),Jungle),$stricmp($meta(genre,0),Darkcore),$stricmp($meta(genre,0),Neo-Jungle),$stricmp($meta(genre,0),Ragga Jungle),$stricmp($meta(genre,0),Ragga Drum and Bass)),$set_style(back,$rgb(246,26,3)))
    $if($or($stricmp($meta(genre,0),Drum and Bass?),$stricmp($meta(genre,0),Atmospheric Drum and Bass?),$stricmp($meta(genre,0),Drumfunk?),$stricmp($meta(genre,0),Minimal Drum and Bass?),$stricmp($meta(genre,0),Microfunk?),$stricmp($meta(genre,0),Technoid?),$stricmp($meta(genre,0),Hardstep?),$stricmp($meta(genre,0),Techstep?),$stricmp($meta(genre,0),Darkstep?),$stricmp($meta(genre,0),Crossbreed?),$stricmp($meta(genre,0),Skullstep?),$stricmp($meta(genre,0),Deep Drum and Bass?),$stricmp($meta(genre,0),Neurofunk?),$stricmp($meta(genre,0),Trancestep?),$stricmp($meta(genre,0),Jump Up?),$stricmp($meta(genre,0),Drumstep?),$stricmp($meta(genre,0),Experimental Drum and Bass?),$stricmp($meta(genre,0),Experimental Drumstep?),$stricmp($meta(genre,0),Jazzstep?),$stricmp($meta(genre,0),Liquid Drum and Bass?),$stricmp($meta(genre,0),Dancefloor Drum and Bass?),$stricmp($meta(genre,0),Sambass?),$stricmp($meta(genre,0),Jungle?),$stricmp($meta(genre,0),Darkcore?),$stricmp($meta(genre,0),Neo-Jungle?),$stricmp($meta(genre,0),Ragga Jungle?),$stricmp($meta(genre,0),Ragga Drum and Bass?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Drum and Bass?),$stricmp($meta(genre,0),Atmospheric Drum and Bass?),$stricmp($meta(genre,0),Drumfunk?),$stricmp($meta(genre,0),Minimal Drum and Bass?),$stricmp($meta(genre,0),Microfunk?),$stricmp($meta(genre,0),Technoid?),$stricmp($meta(genre,0),Hardstep?),$stricmp($meta(genre,0),Techstep?),$stricmp($meta(genre,0),Darkstep?),$stricmp($meta(genre,0),Crossbreed?),$stricmp($meta(genre,0),Skullstep?),$stricmp($meta(genre,0),Deep Drum and Bass?),$stricmp($meta(genre,0),Neurofunk?),$stricmp($meta(genre,0),Trancestep?),$stricmp($meta(genre,0),Jump Up?),$stricmp($meta(genre,0),Drumstep?),$stricmp($meta(genre,0),Experimental Drum and Bass?),$stricmp($meta(genre,0),Experimental Drumstep?),$stricmp($meta(genre,0),Jazzstep?),$stricmp($meta(genre,0),Liquid Drum and Bass?),$stricmp($meta(genre,0),Dancefloor Drum and Bass?),$stricmp($meta(genre,0),Sambass?),$stricmp($meta(genre,0),Jungle?),$stricmp($meta(genre,0),Darkcore?),$stricmp($meta(genre,0),Neo-Jungle?),$stricmp($meta(genre,0),Ragga Jungle?),$stricmp($meta(genre,0),Ragga Drum and Bass?)),$set_style(back,$rgb(246,26,3)))
    $if($or($stricmp($meta(genre,0),Dubstep),$stricmp($meta(genre,0),Deep Dubstep),$stricmp($meta(genre,0),Liquid Dubstep),$stricmp($meta(genre,0),Melodic Dubstep),$stricmp($meta(genre,0),Psystep),$stricmp($meta(genre,0),Purple Sound),$stricmp($meta(genre,0),Tearout),$stricmp($meta(genre,0),Brostep),$stricmp($meta(genre,0),Deathstep),$stricmp($meta(genre,0),Neurostep),$stricmp($meta(genre,0),Riddim),$stricmp($meta(genre,0),Briddim),$stricmp($meta(genre,0),Experimental Dubstep),$stricmp($meta(genre,0),Chillstep),$stricmp($meta(genre,0),Metalstep)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Dubstep),$stricmp($meta(genre,0),Deep Dubstep),$stricmp($meta(genre,0),Liquid Dubstep),$stricmp($meta(genre,0),Melodic Dubstep),$stricmp($meta(genre,0),Psystep),$stricmp($meta(genre,0),Purple Sound),$stricmp($meta(genre,0),Tearout),$stricmp($meta(genre,0),Brostep),$stricmp($meta(genre,0),Deathstep),$stricmp($meta(genre,0),Neurostep),$stricmp($meta(genre,0),Riddim),$stricmp($meta(genre,0),Briddim),$stricmp($meta(genre,0),Experimental Dubstep),$stricmp($meta(genre,0),Chillstep),$stricmp($meta(genre,0),Metalstep)),$set_style(back,$rgb(148,29,232)))
    $if($or($stricmp($meta(genre,0),Dubstep?),$stricmp($meta(genre,0),Deep Dubstep?),$stricmp($meta(genre,0),Liquid Dubstep?),$stricmp($meta(genre,0),Melodic Dubstep?),$stricmp($meta(genre,0),Psystep?),$stricmp($meta(genre,0),Purple Sound?),$stricmp($meta(genre,0),Tearout?),$stricmp($meta(genre,0),Brostep?),$stricmp($meta(genre,0),Deathstep?),$stricmp($meta(genre,0),Neurostep?),$stricmp($meta(genre,0),Riddim?),$stricmp($meta(genre,0),Briddim?),$stricmp($meta(genre,0),Experimental Dubstep?),$stricmp($meta(genre,0),Chillstep?),$stricmp($meta(genre,0),Metalstep?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Dubstep?),$stricmp($meta(genre,0),Deep Dubstep?),$stricmp($meta(genre,0),Liquid Dubstep?),$stricmp($meta(genre,0),Melodic Dubstep?),$stricmp($meta(genre,0),Psystep?),$stricmp($meta(genre,0),Purple Sound?),$stricmp($meta(genre,0),Tearout?),$stricmp($meta(genre,0),Brostep?),$stricmp($meta(genre,0),Deathstep?),$stricmp($meta(genre,0),Neurostep?),$stricmp($meta(genre,0),Riddim?),$stricmp($meta(genre,0),Briddim?),$stricmp($meta(genre,0),Experimental Dubstep?),$stricmp($meta(genre,0),Chillstep?),$stricmp($meta(genre,0),Metalstep?)),$set_style(back,$rgb(148,29,232)))
    $if($or($stricmp($meta(genre,0),Electronic),$stricmp($meta(genre,0),Bit Music),$stricmp($meta(genre,0),Chiptune),$stricmp($meta(genre,0),Downtempo),$stricmp($meta(genre,0),Downbeat),$stricmp($meta(genre,0),Trip Hop),$stricmp($meta(genre,0),Electronic Dance Music),$stricmp($meta(genre,0),Bubblegum Bass),$stricmp($meta(genre,0),Electroclash),$stricmp($meta(genre,0),Full Flavor),$stricmp($meta(genre,0),Jungle Terror),$stricmp($meta(genre,0),Worlds Vibes),$stricmp($meta(genre,0),Folktronica),$stricmp($meta(genre,0),Footwork),$stricmp($meta(genre,0),Horror Synth),$stricmp($meta(genre,0),IDM),$stricmp($meta(genre,0),Flashcore),$stricmp($meta(genre,0),Breakcore),$stricmp($meta(genre,0),Raggacore),$stricmp($meta(genre,0),Drill and Bass),$stricmp($meta(genre,0),Glitch),$stricmp($meta(genre,0),Wonky),$stricmp($meta(genre,0),Illbient),$stricmp($meta(genre,0),Indietronica),$stricmp($meta(genre,0),Chillwave),$stricmp($meta(genre,0),Nightcore),$stricmp($meta(genre,0),Nu-Jazz),$stricmp($meta(genre,0),Plunderphonics),$stricmp($meta(genre,0),Progressive Electronic),$stricmp($meta(genre,0),Synthwave),$stricmp($meta(genre,0),Darksynth),$stricmp($meta(genre,0),Witch House),$stricmp($meta(genre,0),Dungeon Synth),$stricmp($meta(genre,0),Electroacoustic)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Electronic),$stricmp($meta(genre,0),Bit Music),$stricmp($meta(genre,0),Chiptune),$stricmp($meta(genre,0),Downtempo),$stricmp($meta(genre,0),Downbeat),$stricmp($meta(genre,0),Trip Hop),$stricmp($meta(genre,0),Electronic Dance Music),$stricmp($meta(genre,0),Bubblegum Bass),$stricmp($meta(genre,0),Electroclash),$stricmp($meta(genre,0),Full Flavor),$stricmp($meta(genre,0),Jungle Terror),$stricmp($meta(genre,0),Worlds Vibes),$stricmp($meta(genre,0),Folktronica),$stricmp($meta(genre,0),Footwork),$stricmp($meta(genre,0),Horror Synth),$stricmp($meta(genre,0),IDM),$stricmp($meta(genre,0),Flashcore),$stricmp($meta(genre,0),Breakcore),$stricmp($meta(genre,0),Raggacore),$stricmp($meta(genre,0),Drill and Bass),$stricmp($meta(genre,0),Glitch),$stricmp($meta(genre,0),Wonky),$stricmp($meta(genre,0),Illbient),$stricmp($meta(genre,0),Indietronica),$stricmp($meta(genre,0),Chillwave),$stricmp($meta(genre,0),Nightcore),$stricmp($meta(genre,0),Nu-Jazz),$stricmp($meta(genre,0),Plunderphonics),$stricmp($meta(genre,0),Progressive Electronic),$stricmp($meta(genre,0),Synthwave),$stricmp($meta(genre,0),Darksynth),$stricmp($meta(genre,0),Witch House),$stricmp($meta(genre,0),Dungeon Synth),$stricmp($meta(genre,0),Electroacoustic)),$set_style(back,$rgb(0,194,255)))
    $if($or($stricmp($meta(genre,0),Electronic?),$stricmp($meta(genre,0),Bit Music?),$stricmp($meta(genre,0),Chiptune?),$stricmp($meta(genre,0),Downtempo?),$stricmp($meta(genre,0),Downbeat?),$stricmp($meta(genre,0),Trip Hop?),$stricmp($meta(genre,0),Electronic Dance Music?),$stricmp($meta(genre,0),Bubblegum Bass?),$stricmp($meta(genre,0),Electroclash?),$stricmp($meta(genre,0),Full Flavor?),$stricmp($meta(genre,0),Jungle Terror?),$stricmp($meta(genre,0),Worlds Vibes?),$stricmp($meta(genre,0),Folktronica?),$stricmp($meta(genre,0),Footwork?),$stricmp($meta(genre,0),Horror Synth?),$stricmp($meta(genre,0),IDM?),$stricmp($meta(genre,0),Flashcore?),$stricmp($meta(genre,0),Breakcore?),$stricmp($meta(genre,0),Raggacore?),$stricmp($meta(genre,0),Drill and Bass?),$stricmp($meta(genre,0),Glitch?),$stricmp($meta(genre,0),Wonky?),$stricmp($meta(genre,0),Illbient?),$stricmp($meta(genre,0),Indietronica?),$stricmp($meta(genre,0),Chillwave?),$stricmp($meta(genre,0),Nightcore?),$stricmp($meta(genre,0),Nu-Jazz?),$stricmp($meta(genre,0),Plunderphonics?),$stricmp($meta(genre,0),Progressive Electronic?),$stricmp($meta(genre,0),Synthwave?),$stricmp($meta(genre,0),Darksynth?),$stricmp($meta(genre,0),Witch House?),$stricmp($meta(genre,0),Dungeon Synth?),$stricmp($meta(genre,0),Electroacoustic?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Electronic?),$stricmp($meta(genre,0),Bit Music?),$stricmp($meta(genre,0),Chiptune?),$stricmp($meta(genre,0),Downtempo?),$stricmp($meta(genre,0),Downbeat?),$stricmp($meta(genre,0),Trip Hop?),$stricmp($meta(genre,0),Electronic Dance Music?),$stricmp($meta(genre,0),Bubblegum Bass?),$stricmp($meta(genre,0),Electroclash?),$stricmp($meta(genre,0),Full Flavor?),$stricmp($meta(genre,0),Jungle Terror?),$stricmp($meta(genre,0),Worlds Vibes?),$stricmp($meta(genre,0),Folktronica?),$stricmp($meta(genre,0),Footwork?),$stricmp($meta(genre,0),Horror Synth?),$stricmp($meta(genre,0),IDM?),$stricmp($meta(genre,0),Flashcore?),$stricmp($meta(genre,0),Breakcore?),$stricmp($meta(genre,0),Raggacore?),$stricmp($meta(genre,0),Drill and Bass?),$stricmp($meta(genre,0),Glitch?),$stricmp($meta(genre,0),Wonky?),$stricmp($meta(genre,0),Illbient?),$stricmp($meta(genre,0),Indietronica?),$stricmp($meta(genre,0),Chillwave?),$stricmp($meta(genre,0),Nightcore?),$stricmp($meta(genre,0),Nu-Jazz?),$stricmp($meta(genre,0),Plunderphonics?),$stricmp($meta(genre,0),Progressive Electronic?),$stricmp($meta(genre,0),Synthwave?),$stricmp($meta(genre,0),Darksynth?),$stricmp($meta(genre,0),Witch House?),$stricmp($meta(genre,0),Dungeon Synth?),$stricmp($meta(genre,0),Electroacoustic?)),$set_style(back,$rgb(0,194,255)))
    $if($or($stricmp($meta(genre,0),Experimental),$stricmp($meta(genre,0),Drone),$stricmp($meta(genre,0),Free Improvisation),$stricmp($meta(genre,0),Lowercase),$stricmp($meta(genre,0),Musique Concrète),$stricmp($meta(genre,0),Noise),$stricmp($meta(genre,0),Rhythmic Noise),$stricmp($meta(genre,0),Sound Collage)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Experimental),$stricmp($meta(genre,0),Drone),$stricmp($meta(genre,0),Free Improvisation),$stricmp($meta(genre,0),Lowercase),$stricmp($meta(genre,0),Musique Concrète),$stricmp($meta(genre,0),Noise),$stricmp($meta(genre,0),Rhythmic Noise),$stricmp($meta(genre,0),Sound Collage)),$set_style(back,$rgb(117,124,101)))
    $if($or($stricmp($meta(genre,0),Experimental?),$stricmp($meta(genre,0),Drone?),$stricmp($meta(genre,0),Free Improvisation?),$stricmp($meta(genre,0),Lowercase?),$stricmp($meta(genre,0),Musique Concrète?),$stricmp($meta(genre,0),Noise?),$stricmp($meta(genre,0),Rhythmic Noise?),$stricmp($meta(genre,0),Sound Collage?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Experimental?),$stricmp($meta(genre,0),Drone?),$stricmp($meta(genre,0),Free Improvisation?),$stricmp($meta(genre,0),Lowercase?),$stricmp($meta(genre,0),Musique Concrète?),$stricmp($meta(genre,0),Noise?),$stricmp($meta(genre,0),Rhythmic Noise?),$stricmp($meta(genre,0),Sound Collage?)),$set_style(back,$rgb(117,124,101)))
    $if($or($stricmp($meta(genre,0),Folk),$stricmp($meta(genre,0),Contemporary Folk),$stricmp($meta(genre,0),Freak Folk),$stricmp($meta(genre,0),Anti-Folk),$stricmp($meta(genre,0),Avant-Folk),$stricmp($meta(genre,0),Chamber Folk),$stricmp($meta(genre,0),Folk Baroque),$stricmp($meta(genre,0),Indie Folk),$stricmp($meta(genre,0),Neofolk),$stricmp($meta(genre,0),Progressive Folk),$stricmp($meta(genre,0),Traditional Folk),$stricmp($meta(genre,0),American Folk),$stricmp($meta(genre,0),Ragtime),$stricmp($meta(genre,0),Talking Blues),$stricmp($meta(genre,0),European Folk),$stricmp($meta(genre,0),Celtic Folk),$stricmp($meta(genre,0),English Folk),$stricmp($meta(genre,0),Fanfare),$stricmp($meta(genre,0),Brass Band),$stricmp($meta(genre,0),Spanish Folk),$stricmp($meta(genre,0),Flamenco),$stricmp($meta(genre,0),Folk Ballad),$stricmp($meta(genre,0),Traditional),$stricmp($meta(genre,0),A Cappella),$stricmp($meta(genre,0),Nasheed),$stricmp($meta(genre,0),Ballad),$stricmp($meta(genre,0),Bluegrass),$stricmp($meta(genre,0),Blues),$stricmp($meta(genre,0),Acoustic Blues),$stricmp($meta(genre,0),Boogie Woogie),$stricmp($meta(genre,0),Electric Blues),$stricmp($meta(genre,0),Jump Blues),$stricmp($meta(genre,0),Piano Blues),$stricmp($meta(genre,0),Soul Blues),$stricmp($meta(genre,0),Classical),$stricmp($meta(genre,0),Baroque),$stricmp($meta(genre,0),Modern Classical),$stricmp($meta(genre,0),Minimalism),$stricmp($meta(genre,0),Romanticism),$stricmp($meta(genre,0),Cinematic Classical),$stricmp($meta(genre,0),Impressionism),$stricmp($meta(genre,0),Gospel),$stricmp($meta(genre,0),Opera),$stricmp($meta(genre,0),Instrumental),$stricmp($meta(genre,0),Lullaby),$stricmp($meta(genre,0),Orchestral),$stricmp($meta(genre,0),Waltz),$stricmp($meta(genre,0),World Music)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Folk),$stricmp($meta(genre,0),Contemporary Folk),$stricmp($meta(genre,0),Freak Folk),$stricmp($meta(genre,0),Anti-Folk),$stricmp($meta(genre,0),Avant-Folk),$stricmp($meta(genre,0),Chamber Folk),$stricmp($meta(genre,0),Folk Baroque),$stricmp($meta(genre,0),Indie Folk),$stricmp($meta(genre,0),Neofolk),$stricmp($meta(genre,0),Progressive Folk),$stricmp($meta(genre,0),Traditional Folk),$stricmp($meta(genre,0),American Folk),$stricmp($meta(genre,0),Ragtime),$stricmp($meta(genre,0),Talking Blues),$stricmp($meta(genre,0),European Folk),$stricmp($meta(genre,0),Celtic Folk),$stricmp($meta(genre,0),English Folk),$stricmp($meta(genre,0),Fanfare),$stricmp($meta(genre,0),Brass Band),$stricmp($meta(genre,0),Spanish Folk),$stricmp($meta(genre,0),Flamenco),$stricmp($meta(genre,0),Folk Ballad),$stricmp($meta(genre,0),Traditional),$stricmp($meta(genre,0),A Cappella),$stricmp($meta(genre,0),Nasheed),$stricmp($meta(genre,0),Ballad),$stricmp($meta(genre,0),Bluegrass),$stricmp($meta(genre,0),Blues),$stricmp($meta(genre,0),Acoustic Blues),$stricmp($meta(genre,0),Boogie Woogie),$stricmp($meta(genre,0),Electric Blues),$stricmp($meta(genre,0),Jump Blues),$stricmp($meta(genre,0),Piano Blues),$stricmp($meta(genre,0),Soul Blues),$stricmp($meta(genre,0),Classical),$stricmp($meta(genre,0),Baroque),$stricmp($meta(genre,0),Modern Classical),$stricmp($meta(genre,0),Minimalism),$stricmp($meta(genre,0),Romanticism),$stricmp($meta(genre,0),Cinematic Classical),$stricmp($meta(genre,0),Impressionism),$stricmp($meta(genre,0),Gospel),$stricmp($meta(genre,0),Opera),$stricmp($meta(genre,0),Instrumental),$stricmp($meta(genre,0),Lullaby),$stricmp($meta(genre,0),Orchestral),$stricmp($meta(genre,0),Waltz),$stricmp($meta(genre,0),World Music)),$set_style(back,$rgb(208,173,96)))
    $if($or($stricmp($meta(genre,0),Folk?),$stricmp($meta(genre,0),Contemporary Folk?),$stricmp($meta(genre,0),Freak Folk?),$stricmp($meta(genre,0),Anti-Folk?),$stricmp($meta(genre,0),Avant-Folk?),$stricmp($meta(genre,0),Chamber Folk?),$stricmp($meta(genre,0),Folk Baroque?),$stricmp($meta(genre,0),Indie Folk?),$stricmp($meta(genre,0),Neofolk?),$stricmp($meta(genre,0),Progressive Folk?),$stricmp($meta(genre,0),Traditional Folk?),$stricmp($meta(genre,0),American Folk?),$stricmp($meta(genre,0),Ragtime?),$stricmp($meta(genre,0),Talking Blues?),$stricmp($meta(genre,0),European Folk?),$stricmp($meta(genre,0),Celtic Folk?),$stricmp($meta(genre,0),English Folk?),$stricmp($meta(genre,0),Fanfare?),$stricmp($meta(genre,0),Brass Band?),$stricmp($meta(genre,0),Spanish Folk?),$stricmp($meta(genre,0),Flamenco?),$stricmp($meta(genre,0),Folk Ballad?),$stricmp($meta(genre,0),Traditional?),$stricmp($meta(genre,0),A Cappella?),$stricmp($meta(genre,0),Nasheed?),$stricmp($meta(genre,0),Ballad?),$stricmp($meta(genre,0),Bluegrass?),$stricmp($meta(genre,0),Blues?),$stricmp($meta(genre,0),Acoustic Blues?),$stricmp($meta(genre,0),Boogie Woogie?),$stricmp($meta(genre,0),Electric Blues?),$stricmp($meta(genre,0),Jump Blues?),$stricmp($meta(genre,0),Piano Blues?),$stricmp($meta(genre,0),Soul Blues?),$stricmp($meta(genre,0),Classical?),$stricmp($meta(genre,0),Baroque?),$stricmp($meta(genre,0),Modern Classical?),$stricmp($meta(genre,0),Minimalism?),$stricmp($meta(genre,0),Romanticism?),$stricmp($meta(genre,0),Cinematic Classical?),$stricmp($meta(genre,0),Impressionism?),$stricmp($meta(genre,0),Gospel?),$stricmp($meta(genre,0),Opera?),$stricmp($meta(genre,0),Instrumental?),$stricmp($meta(genre,0),Lullaby?),$stricmp($meta(genre,0),Orchestral?),$stricmp($meta(genre,0),Waltz?),$stricmp($meta(genre,0),World Music?)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Folk?),$stricmp($meta(genre,0),Contemporary Folk?),$stricmp($meta(genre,0),Freak Folk?),$stricmp($meta(genre,0),Anti-Folk?),$stricmp($meta(genre,0),Avant-Folk?),$stricmp($meta(genre,0),Chamber Folk?),$stricmp($meta(genre,0),Folk Baroque?),$stricmp($meta(genre,0),Indie Folk?),$stricmp($meta(genre,0),Neofolk?),$stricmp($meta(genre,0),Progressive Folk?),$stricmp($meta(genre,0),Traditional Folk?),$stricmp($meta(genre,0),American Folk?),$stricmp($meta(genre,0),Ragtime?),$stricmp($meta(genre,0),Talking Blues?),$stricmp($meta(genre,0),European Folk?),$stricmp($meta(genre,0),Celtic Folk?),$stricmp($meta(genre,0),English Folk?),$stricmp($meta(genre,0),Fanfare?),$stricmp($meta(genre,0),Brass Band?),$stricmp($meta(genre,0),Spanish Folk?),$stricmp($meta(genre,0),Flamenco?),$stricmp($meta(genre,0),Folk Ballad?),$stricmp($meta(genre,0),Traditional?),$stricmp($meta(genre,0),A Cappella?),$stricmp($meta(genre,0),Nasheed?),$stricmp($meta(genre,0),Ballad?),$stricmp($meta(genre,0),Bluegrass?),$stricmp($meta(genre,0),Blues?),$stricmp($meta(genre,0),Acoustic Blues?),$stricmp($meta(genre,0),Boogie Woogie?),$stricmp($meta(genre,0),Electric Blues?),$stricmp($meta(genre,0),Jump Blues?),$stricmp($meta(genre,0),Piano Blues?),$stricmp($meta(genre,0),Soul Blues?),$stricmp($meta(genre,0),Classical?),$stricmp($meta(genre,0),Baroque?),$stricmp($meta(genre,0),Modern Classical?),$stricmp($meta(genre,0),Minimalism?),$stricmp($meta(genre,0),Romanticism?),$stricmp($meta(genre,0),Cinematic Classical?),$stricmp($meta(genre,0),Impressionism?),$stricmp($meta(genre,0),Gospel?),$stricmp($meta(genre,0),Opera?),$stricmp($meta(genre,0),Instrumental?),$stricmp($meta(genre,0),Lullaby?),$stricmp($meta(genre,0),Orchestral?),$stricmp($meta(genre,0),Waltz?),$stricmp($meta(genre,0),World Music?)),$set_style(back,$rgb(208,173,96)))
    $if($or($stricmp($meta(genre,0),Future Bass),$stricmp($meta(genre,0),Kawaii Bass),$stricmp($meta(genre,0),Purple House)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Future Bass),$stricmp($meta(genre,0),Kawaii Bass),$stricmp($meta(genre,0),Purple House)),$set_style(back,$rgb(144,144,255)))
    $if($or($stricmp($meta(genre,0),Future Bass?),$stricmp($meta(genre,0),Kawaii Bass?),$stricmp($meta(genre,0),Purple House?)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Future Bass?),$stricmp($meta(genre,0),Kawaii Bass?),$stricmp($meta(genre,0),Purple House?)),$set_style(back,$rgb(144,144,255)))
    $if($or($stricmp($meta(genre,0),Hardcore),$stricmp($meta(genre,0),Acidcore),$stricmp($meta(genre,0),Breakbeat Hardcore),$stricmp($meta(genre,0),4-beat),$stricmp($meta(genre,0),Happy Hardcore),$stricmp($meta(genre,0),Bouncy Techno),$stricmp($meta(genre,0),Freeform),$stricmp($meta(genre,0),UK Hardcore),$stricmp($meta(genre,0),Hardcore Breaks),$stricmp($meta(genre,0),Frenchcore),$stricmp($meta(genre,0),Gabber),$stricmp($meta(genre,0),Gabber House),$stricmp($meta(genre,0),Nu-Style Gabber),$stricmp($meta(genre,0),Hardstyle),$stricmp($meta(genre,0),Dubstyle),$stricmp($meta(genre,0),Euphoric Hardstyle),$stricmp($meta(genre,0),Jumpstyle),$stricmp($meta(genre,0),Rawstyle),$stricmp($meta(genre,0),Reverse Bass),$stricmp($meta(genre,0),Speedcore),$stricmp($meta(genre,0),Extratone),$stricmp($meta(genre,0),Splittercore),$stricmp($meta(genre,0),Terrorcore),$stricmp($meta(genre,0),Hardcore Techno),$stricmp($meta(genre,0),J-Core),$stricmp($meta(genre,0),Industrial Hardcore),$stricmp($meta(genre,0),Doomcore),$stricmp($meta(genre,0),Psycore),$stricmp($meta(genre,0),Hardbass)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Hardcore),$stricmp($meta(genre,0),Acidcore),$stricmp($meta(genre,0),Breakbeat Hardcore),$stricmp($meta(genre,0),4-beat),$stricmp($meta(genre,0),Happy Hardcore),$stricmp($meta(genre,0),Bouncy Techno),$stricmp($meta(genre,0),Freeform),$stricmp($meta(genre,0),UK Hardcore),$stricmp($meta(genre,0),Hardcore Breaks),$stricmp($meta(genre,0),Frenchcore),$stricmp($meta(genre,0),Gabber),$stricmp($meta(genre,0),Gabber House),$stricmp($meta(genre,0),Nu-Style Gabber),$stricmp($meta(genre,0),Hardstyle),$stricmp($meta(genre,0),Dubstyle),$stricmp($meta(genre,0),Euphoric Hardstyle),$stricmp($meta(genre,0),Jumpstyle),$stricmp($meta(genre,0),Rawstyle),$stricmp($meta(genre,0),Reverse Bass),$stricmp($meta(genre,0),Speedcore),$stricmp($meta(genre,0),Extratone),$stricmp($meta(genre,0),Splittercore),$stricmp($meta(genre,0),Terrorcore),$stricmp($meta(genre,0),Hardcore Techno),$stricmp($meta(genre,0),J-Core),$stricmp($meta(genre,0),Industrial Hardcore),$stricmp($meta(genre,0),Doomcore),$stricmp($meta(genre,0),Psycore),$stricmp($meta(genre,0),Hardbass)),$set_style(back,$rgb(0,150,0)))
    $if($or($stricmp($meta(genre,0),Hardcore?),$stricmp($meta(genre,0),Acidcore?),$stricmp($meta(genre,0),Breakbeat Hardcore?),$stricmp($meta(genre,0),4-beat?),$stricmp($meta(genre,0),Happy Hardcore?),$stricmp($meta(genre,0),Bouncy Techno?),$stricmp($meta(genre,0),Freeform?),$stricmp($meta(genre,0),UK Hardcore?),$stricmp($meta(genre,0),Hardcore Breaks?),$stricmp($meta(genre,0),Frenchcore?),$stricmp($meta(genre,0),Gabber?),$stricmp($meta(genre,0),Gabber House?),$stricmp($meta(genre,0),Nu-Style Gabber?),$stricmp($meta(genre,0),Hardstyle?),$stricmp($meta(genre,0),Dubstyle?),$stricmp($meta(genre,0),Euphoric Hardstyle?),$stricmp($meta(genre,0),Jumpstyle?),$stricmp($meta(genre,0),Rawstyle?),$stricmp($meta(genre,0),Reverse Bass?),$stricmp($meta(genre,0),Speedcore?),$stricmp($meta(genre,0),Extratone?),$stricmp($meta(genre,0),Splittercore?),$stricmp($meta(genre,0),Terrorcore?),$stricmp($meta(genre,0),Hardcore Techno?),$stricmp($meta(genre,0),J-Core?),$stricmp($meta(genre,0),Industrial Hardcore?),$stricmp($meta(genre,0),Doomcore?),$stricmp($meta(genre,0),Psycore?),$stricmp($meta(genre,0),Hardbass?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Hardcore?),$stricmp($meta(genre,0),Acidcore?),$stricmp($meta(genre,0),Breakbeat Hardcore?),$stricmp($meta(genre,0),4-beat?),$stricmp($meta(genre,0),Happy Hardcore?),$stricmp($meta(genre,0),Bouncy Techno?),$stricmp($meta(genre,0),Freeform?),$stricmp($meta(genre,0),UK Hardcore?),$stricmp($meta(genre,0),Hardcore Breaks?),$stricmp($meta(genre,0),Frenchcore?),$stricmp($meta(genre,0),Gabber?),$stricmp($meta(genre,0),Gabber House?),$stricmp($meta(genre,0),Nu-Style Gabber?),$stricmp($meta(genre,0),Hardstyle?),$stricmp($meta(genre,0),Dubstyle?),$stricmp($meta(genre,0),Euphoric Hardstyle?),$stricmp($meta(genre,0),Jumpstyle?),$stricmp($meta(genre,0),Rawstyle?),$stricmp($meta(genre,0),Reverse Bass?),$stricmp($meta(genre,0),Speedcore?),$stricmp($meta(genre,0),Extratone?),$stricmp($meta(genre,0),Splittercore?),$stricmp($meta(genre,0),Terrorcore?),$stricmp($meta(genre,0),Hardcore Techno?),$stricmp($meta(genre,0),J-Core?),$stricmp($meta(genre,0),Industrial Hardcore?),$stricmp($meta(genre,0),Doomcore?),$stricmp($meta(genre,0),Psycore?),$stricmp($meta(genre,0),Hardbass?)),$set_style(back,$rgb(0,150,0)))
    $if($or($stricmp($meta(genre,0),Hip Hop),$stricmp($meta(genre,0),Alternative Hip Hop),$stricmp($meta(genre,0),Abstract Hip Hop),$stricmp($meta(genre,0),Cloud Rap),$stricmp($meta(genre,0),Emorap),$stricmp($meta(genre,0),Industrial Hip Hop),$stricmp($meta(genre,0),Jazz Rap),$stricmp($meta(genre,0),Nerdcore),$stricmp($meta(genre,0),Christian Hip Hop),$stricmp($meta(genre,0),Comedy Hip Hop),$stricmp($meta(genre,0),Conscious Hip Hop),$stricmp($meta(genre,0),Dirty Rap),$stricmp($meta(genre,0),East Coast Hip Hop),$stricmp($meta(genre,0),Boom Bap),$stricmp($meta(genre,0),Disco Rap),$stricmp($meta(genre,0),Electro),$stricmp($meta(genre,0),Hardcore Hip Hop),$stricmp($meta(genre,0),Gangsta Rap),$stricmp($meta(genre,0),Horrorcore),$stricmp($meta(genre,0),Instrumental Hip Hop),$stricmp($meta(genre,0),Chillhop),$stricmp($meta(genre,0),Lo-Fi Hip Hop),$stricmp($meta(genre,0),Chopped and Screwed),$stricmp($meta(genre,0),Turntablism),$stricmp($meta(genre,0),Japanese Hip Hop),$stricmp($meta(genre,0),Midwest Hip Hop),$stricmp($meta(genre,0),Chopper Rap),$stricmp($meta(genre,0),Political Hip Hop),$stricmp($meta(genre,0),Pop Rap),$stricmp($meta(genre,0),Southern Hip Hop),$stricmp($meta(genre,0),Bounce),$stricmp($meta(genre,0),Country Rap),$stricmp($meta(genre,0),Crunk),$stricmp($meta(genre,0),Snap),$stricmp($meta(genre,0),Dirty South),$stricmp($meta(genre,0),Miami Bass),$stricmp($meta(genre,0),Trap [Hip Hop]),$stricmp($meta(genre,0),Drill),$stricmp($meta(genre,0),Bop),$stricmp($meta(genre,0),UK Hip Hop),$stricmp($meta(genre,0),West Coast Hip Hop),$stricmp($meta(genre,0),G-Funk),$stricmp($meta(genre,0),Hyphy),$stricmp($meta(genre,0),Mobb Music)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Hip Hop),$stricmp($meta(genre,0),Alternative Hip Hop),$stricmp($meta(genre,0),Abstract Hip Hop),$stricmp($meta(genre,0),Cloud Rap),$stricmp($meta(genre,0),Emorap),$stricmp($meta(genre,0),Industrial Hip Hop),$stricmp($meta(genre,0),Jazz Rap),$stricmp($meta(genre,0),Nerdcore),$stricmp($meta(genre,0),Christian Hip Hop),$stricmp($meta(genre,0),Comedy Hip Hop),$stricmp($meta(genre,0),Conscious Hip Hop),$stricmp($meta(genre,0),Dirty Rap),$stricmp($meta(genre,0),East Coast Hip Hop),$stricmp($meta(genre,0),Boom Bap),$stricmp($meta(genre,0),Disco Rap),$stricmp($meta(genre,0),Electro),$stricmp($meta(genre,0),Hardcore Hip Hop),$stricmp($meta(genre,0),Gangsta Rap),$stricmp($meta(genre,0),Horrorcore),$stricmp($meta(genre,0),Instrumental Hip Hop),$stricmp($meta(genre,0),Chillhop),$stricmp($meta(genre,0),Lo-Fi Hip Hop),$stricmp($meta(genre,0),Chopped and Screwed),$stricmp($meta(genre,0),Turntablism),$stricmp($meta(genre,0),Japanese Hip Hop),$stricmp($meta(genre,0),Midwest Hip Hop),$stricmp($meta(genre,0),Chopper Rap),$stricmp($meta(genre,0),Political Hip Hop),$stricmp($meta(genre,0),Pop Rap),$stricmp($meta(genre,0),Southern Hip Hop),$stricmp($meta(genre,0),Bounce),$stricmp($meta(genre,0),Country Rap),$stricmp($meta(genre,0),Crunk),$stricmp($meta(genre,0),Snap),$stricmp($meta(genre,0),Dirty South),$stricmp($meta(genre,0),Miami Bass),$stricmp($meta(genre,0),Trap [Hip Hop]),$stricmp($meta(genre,0),Drill),$stricmp($meta(genre,0),Bop),$stricmp($meta(genre,0),UK Hip Hop),$stricmp($meta(genre,0),West Coast Hip Hop),$stricmp($meta(genre,0),G-Funk),$stricmp($meta(genre,0),Hyphy),$stricmp($meta(genre,0),Mobb Music)),$set_style(back,$rgb(215,127,125)))
    $if($or($stricmp($meta(genre,0),Hip Hop?),$stricmp($meta(genre,0),Alternative Hip Hop?),$stricmp($meta(genre,0),Abstract Hip Hop?),$stricmp($meta(genre,0),Cloud Rap?),$stricmp($meta(genre,0),Emorap?),$stricmp($meta(genre,0),Industrial Hip Hop?),$stricmp($meta(genre,0),Jazz Rap?),$stricmp($meta(genre,0),Nerdcore?),$stricmp($meta(genre,0),Christian Hip Hop?),$stricmp($meta(genre,0),Comedy Hip Hop?),$stricmp($meta(genre,0),Conscious Hip Hop?),$stricmp($meta(genre,0),Dirty Rap?),$stricmp($meta(genre,0),East Coast Hip Hop?),$stricmp($meta(genre,0),Boom Bap?),$stricmp($meta(genre,0),Disco Rap?),$stricmp($meta(genre,0),Electro?),$stricmp($meta(genre,0),Hardcore Hip Hop?),$stricmp($meta(genre,0),Gangsta Rap?),$stricmp($meta(genre,0),Horrorcore?),$stricmp($meta(genre,0),Instrumental Hip Hop?),$stricmp($meta(genre,0),Chillhop?),$stricmp($meta(genre,0),Lo-Fi Hip Hop?),$stricmp($meta(genre,0),Chopped and Screwed?),$stricmp($meta(genre,0),Turntablism?),$stricmp($meta(genre,0),Japanese Hip Hop?),$stricmp($meta(genre,0),Midwest Hip Hop?),$stricmp($meta(genre,0),Chopper Rap?),$stricmp($meta(genre,0),Political Hip Hop?),$stricmp($meta(genre,0),Pop Rap?),$stricmp($meta(genre,0),Southern Hip Hop?),$stricmp($meta(genre,0),Bounce?),$stricmp($meta(genre,0),Country Rap?),$stricmp($meta(genre,0),Crunk?),$stricmp($meta(genre,0),Snap?),$stricmp($meta(genre,0),Dirty South?),$stricmp($meta(genre,0),Miami Bass?),$stricmp($meta(genre,0),Trap [Hip Hop]?),$stricmp($meta(genre,0),Drill?),$stricmp($meta(genre,0),Bop?),$stricmp($meta(genre,0),UK Hip Hop?),$stricmp($meta(genre,0),West Coast Hip Hop?),$stricmp($meta(genre,0),G-Funk?),$stricmp($meta(genre,0),Hyphy?),$stricmp($meta(genre,0),Mobb Music?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Hip Hop?),$stricmp($meta(genre,0),Alternative Hip Hop?),$stricmp($meta(genre,0),Abstract Hip Hop?),$stricmp($meta(genre,0),Cloud Rap?),$stricmp($meta(genre,0),Emorap?),$stricmp($meta(genre,0),Industrial Hip Hop?),$stricmp($meta(genre,0),Jazz Rap?),$stricmp($meta(genre,0),Nerdcore?),$stricmp($meta(genre,0),Christian Hip Hop?),$stricmp($meta(genre,0),Comedy Hip Hop?),$stricmp($meta(genre,0),Conscious Hip Hop?),$stricmp($meta(genre,0),Dirty Rap?),$stricmp($meta(genre,0),East Coast Hip Hop?),$stricmp($meta(genre,0),Boom Bap?),$stricmp($meta(genre,0),Disco Rap?),$stricmp($meta(genre,0),Electro?),$stricmp($meta(genre,0),Hardcore Hip Hop?),$stricmp($meta(genre,0),Gangsta Rap?),$stricmp($meta(genre,0),Horrorcore?),$stricmp($meta(genre,0),Instrumental Hip Hop?),$stricmp($meta(genre,0),Chillhop?),$stricmp($meta(genre,0),Lo-Fi Hip Hop?),$stricmp($meta(genre,0),Chopped and Screwed?),$stricmp($meta(genre,0),Turntablism?),$stricmp($meta(genre,0),Japanese Hip Hop?),$stricmp($meta(genre,0),Midwest Hip Hop?),$stricmp($meta(genre,0),Chopper Rap?),$stricmp($meta(genre,0),Political Hip Hop?),$stricmp($meta(genre,0),Pop Rap?),$stricmp($meta(genre,0),Southern Hip Hop?),$stricmp($meta(genre,0),Bounce?),$stricmp($meta(genre,0),Country Rap?),$stricmp($meta(genre,0),Crunk?),$stricmp($meta(genre,0),Snap?),$stricmp($meta(genre,0),Dirty South?),$stricmp($meta(genre,0),Miami Bass?),$stricmp($meta(genre,0),Trap [Hip Hop]?),$stricmp($meta(genre,0),Drill?),$stricmp($meta(genre,0),Bop?),$stricmp($meta(genre,0),UK Hip Hop?),$stricmp($meta(genre,0),West Coast Hip Hop?),$stricmp($meta(genre,0),G-Funk?),$stricmp($meta(genre,0),Hyphy?),$stricmp($meta(genre,0),Mobb Music?)),$set_style(back,$rgb(215,127,125)))
    $if($or($stricmp($meta(genre,0),House),$stricmp($meta(genre,0),Chicago House),$stricmp($meta(genre,0),Ballroom),$stricmp($meta(genre,0),Acid House),$stricmp($meta(genre,0),Ambient House),$stricmp($meta(genre,0),Balearic Beat),$stricmp($meta(genre,0),Deep House),$stricmp($meta(genre,0),Funky House),$stricmp($meta(genre,0),Future House),$stricmp($meta(genre,0),Slap House),$stricmp($meta(genre,0),Fake Future),$stricmp($meta(genre,0),French House),$stricmp($meta(genre,0),Outsider House),$stricmp($meta(genre,0),Tropical House),$stricmp($meta(genre,0),Euro House),$stricmp($meta(genre,0),Latin House),$stricmp($meta(genre,0),G-House),$stricmp($meta(genre,0),Ghettotech),$stricmp($meta(genre,0),Juke),$stricmp($meta(genre,0),Hip House),$stricmp($meta(genre,0),Italo House),$stricmp($meta(genre,0),Kwaito),$stricmp($meta(genre,0),Gqom),$stricmp($meta(genre,0),Progressive House),$stricmp($meta(genre,0),Melodic House),$stricmp($meta(genre,0),Commercial House),$stricmp($meta(genre,0),Tech House),$stricmp($meta(genre,0),Chicago Hard House),$stricmp($meta(genre,0),Electro House),$stricmp($meta(genre,0),Experimental Electro House),$stricmp($meta(genre,0),Experimental House),$stricmp($meta(genre,0),Complextro),$stricmp($meta(genre,0),Dutch House),$stricmp($meta(genre,0),Big Room House),$stricmp($meta(genre,0),Melbourne Bounce),$stricmp($meta(genre,0),Future Bounce),$stricmp($meta(genre,0),Psybounce),$stricmp($meta(genre,0),French Electro),$stricmp($meta(genre,0),Fidget House),$stricmp($meta(genre,0),Bass House),$stricmp($meta(genre,0),Brazilian Bass),$stricmp($meta(genre,0),Norwegian House),$stricmp($meta(genre,0),Microhouse),$stricmp($meta(genre,0),Minimal House),$stricmp($meta(genre,0),Tribal House),$stricmp($meta(genre,0),Circuit House),$stricmp($meta(genre,0),UK Hard House),$stricmp($meta(genre,0),Bouncy House),$stricmp($meta(genre,0),Donk),$stricmp($meta(genre,0),Electro Swing),$stricmp($meta(genre,0),Garage House),$stricmp($meta(genre,0),Jackin'' House),$stricmp($meta(genre,0),Nu-Disco),$stricmp($meta(genre,0),Afro House),$stricmp($meta(genre,0),Organic House)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),House),$stricmp($meta(genre,0),Chicago House),$stricmp($meta(genre,0),Ballroom),$stricmp($meta(genre,0),Acid House),$stricmp($meta(genre,0),Ambient House),$stricmp($meta(genre,0),Balearic Beat),$stricmp($meta(genre,0),Deep House),$stricmp($meta(genre,0),Funky House),$stricmp($meta(genre,0),Future House),$stricmp($meta(genre,0),Slap House),$stricmp($meta(genre,0),Fake Future),$stricmp($meta(genre,0),French House),$stricmp($meta(genre,0),Outsider House),$stricmp($meta(genre,0),Tropical House),$stricmp($meta(genre,0),Euro House),$stricmp($meta(genre,0),Latin House),$stricmp($meta(genre,0),G-House),$stricmp($meta(genre,0),Ghettotech),$stricmp($meta(genre,0),Juke),$stricmp($meta(genre,0),Hip House),$stricmp($meta(genre,0),Italo House),$stricmp($meta(genre,0),Kwaito),$stricmp($meta(genre,0),Gqom),$stricmp($meta(genre,0),Progressive House),$stricmp($meta(genre,0),Melodic House),$stricmp($meta(genre,0),Commercial House),$stricmp($meta(genre,0),Tech House),$stricmp($meta(genre,0),Chicago Hard House),$stricmp($meta(genre,0),Electro House),$stricmp($meta(genre,0),Experimental Electro House),$stricmp($meta(genre,0),Experimental House),$stricmp($meta(genre,0),Complextro),$stricmp($meta(genre,0),Dutch House),$stricmp($meta(genre,0),Big Room House),$stricmp($meta(genre,0),Melbourne Bounce),$stricmp($meta(genre,0),Future Bounce),$stricmp($meta(genre,0),Psybounce),$stricmp($meta(genre,0),French Electro),$stricmp($meta(genre,0),Fidget House),$stricmp($meta(genre,0),Bass House),$stricmp($meta(genre,0),Brazilian Bass),$stricmp($meta(genre,0),Norwegian House),$stricmp($meta(genre,0),Microhouse),$stricmp($meta(genre,0),Minimal House),$stricmp($meta(genre,0),Tribal House),$stricmp($meta(genre,0),Circuit House),$stricmp($meta(genre,0),UK Hard House),$stricmp($meta(genre,0),Bouncy House),$stricmp($meta(genre,0),Donk),$stricmp($meta(genre,0),Electro Swing),$stricmp($meta(genre,0),Garage House),$stricmp($meta(genre,0),Jackin'' House),$stricmp($meta(genre,0),Nu-Disco),$stricmp($meta(genre,0),Afro House),$stricmp($meta(genre,0),Organic House)),$set_style(back,$rgb(235,130,0)))
    $if($or($stricmp($meta(genre,0),House?),$stricmp($meta(genre,0),Chicago House?),$stricmp($meta(genre,0),Ballroom?),$stricmp($meta(genre,0),Acid House?),$stricmp($meta(genre,0),Ambient House?),$stricmp($meta(genre,0),Balearic Beat?),$stricmp($meta(genre,0),Deep House?),$stricmp($meta(genre,0),Funky House?),$stricmp($meta(genre,0),Future House?),$stricmp($meta(genre,0),Slap House?),$stricmp($meta(genre,0),Fake Future?),$stricmp($meta(genre,0),French House?),$stricmp($meta(genre,0),Outsider House?),$stricmp($meta(genre,0),Tropical House?),$stricmp($meta(genre,0),Euro House?),$stricmp($meta(genre,0),Latin House?),$stricmp($meta(genre,0),G-House?),$stricmp($meta(genre,0),Ghettotech?),$stricmp($meta(genre,0),Juke?),$stricmp($meta(genre,0),Hip House?),$stricmp($meta(genre,0),Italo House?),$stricmp($meta(genre,0),Kwaito?),$stricmp($meta(genre,0),Gqom?),$stricmp($meta(genre,0),Progressive House?),$stricmp($meta(genre,0),Melodic House?),$stricmp($meta(genre,0),Commercial House?),$stricmp($meta(genre,0),Tech House?),$stricmp($meta(genre,0),Chicago Hard House?),$stricmp($meta(genre,0),Electro House?),$stricmp($meta(genre,0),Experimental Electro House?),$stricmp($meta(genre,0),Experimental House?),$stricmp($meta(genre,0),Complextro?),$stricmp($meta(genre,0),Dutch House?),$stricmp($meta(genre,0),Big Room House?),$stricmp($meta(genre,0),Melbourne Bounce?),$stricmp($meta(genre,0),Future Bounce?),$stricmp($meta(genre,0),Psybounce?),$stricmp($meta(genre,0),French Electro?),$stricmp($meta(genre,0),Fidget House?),$stricmp($meta(genre,0),Bass House?),$stricmp($meta(genre,0),Brazilian Bass?),$stricmp($meta(genre,0),Norwegian House?),$stricmp($meta(genre,0),Microhouse?),$stricmp($meta(genre,0),Minimal House?),$stricmp($meta(genre,0),Tribal House?),$stricmp($meta(genre,0),Circuit House?),$stricmp($meta(genre,0),UK Hard House?),$stricmp($meta(genre,0),Bouncy House?),$stricmp($meta(genre,0),Donk?),$stricmp($meta(genre,0),Electro Swing?),$stricmp($meta(genre,0),Garage House?),$stricmp($meta(genre,0),Jackin'' House?),$stricmp($meta(genre,0),Nu-Disco?),$stricmp($meta(genre,0),Afro House?),$stricmp($meta(genre,0),Organic House?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),House?),$stricmp($meta(genre,0),Chicago House?),$stricmp($meta(genre,0),Ballroom?),$stricmp($meta(genre,0),Acid House?),$stricmp($meta(genre,0),Ambient House?),$stricmp($meta(genre,0),Balearic Beat?),$stricmp($meta(genre,0),Deep House?),$stricmp($meta(genre,0),Funky House?),$stricmp($meta(genre,0),Future House?),$stricmp($meta(genre,0),Slap House?),$stricmp($meta(genre,0),Fake Future?),$stricmp($meta(genre,0),French House?),$stricmp($meta(genre,0),Outsider House?),$stricmp($meta(genre,0),Tropical House?),$stricmp($meta(genre,0),Euro House?),$stricmp($meta(genre,0),Latin House?),$stricmp($meta(genre,0),G-House?),$stricmp($meta(genre,0),Ghettotech?),$stricmp($meta(genre,0),Juke?),$stricmp($meta(genre,0),Hip House?),$stricmp($meta(genre,0),Italo House?),$stricmp($meta(genre,0),Kwaito?),$stricmp($meta(genre,0),Gqom?),$stricmp($meta(genre,0),Progressive House?),$stricmp($meta(genre,0),Melodic House?),$stricmp($meta(genre,0),Commercial House?),$stricmp($meta(genre,0),Tech House?),$stricmp($meta(genre,0),Chicago Hard House?),$stricmp($meta(genre,0),Electro House?),$stricmp($meta(genre,0),Experimental Electro House?),$stricmp($meta(genre,0),Experimental House?),$stricmp($meta(genre,0),Complextro?),$stricmp($meta(genre,0),Dutch House?),$stricmp($meta(genre,0),Big Room House?),$stricmp($meta(genre,0),Melbourne Bounce?),$stricmp($meta(genre,0),Future Bounce?),$stricmp($meta(genre,0),Psybounce?),$stricmp($meta(genre,0),French Electro?),$stricmp($meta(genre,0),Fidget House?),$stricmp($meta(genre,0),Bass House?),$stricmp($meta(genre,0),Brazilian Bass?),$stricmp($meta(genre,0),Norwegian House?),$stricmp($meta(genre,0),Microhouse?),$stricmp($meta(genre,0),Minimal House?),$stricmp($meta(genre,0),Tribal House?),$stricmp($meta(genre,0),Circuit House?),$stricmp($meta(genre,0),UK Hard House?),$stricmp($meta(genre,0),Bouncy House?),$stricmp($meta(genre,0),Donk?),$stricmp($meta(genre,0),Electro Swing?),$stricmp($meta(genre,0),Garage House?),$stricmp($meta(genre,0),Jackin'' House?),$stricmp($meta(genre,0),Nu-Disco?),$stricmp($meta(genre,0),Afro House?),$stricmp($meta(genre,0),Organic House?)),$set_style(back,$rgb(235,130,0)))
    $if($or($stricmp($meta(genre,0),Industrial),$stricmp($meta(genre,0),Post-Industrial),$stricmp($meta(genre,0),Ambient Industrial),$stricmp($meta(genre,0),Deconstructed Club),$stricmp($meta(genre,0),EBM),$stricmp($meta(genre,0),Electro-Industrial),$stricmp($meta(genre,0),Aggrotech),$stricmp($meta(genre,0),Dark Electro),$stricmp($meta(genre,0),Futurepop),$stricmp($meta(genre,0),New Beat),$stricmp($meta(genre,0),Power Noise),$stricmp($meta(genre,0),Power Electronics),$stricmp($meta(genre,0),Death Industrial)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Industrial),$stricmp($meta(genre,0),Post-Industrial),$stricmp($meta(genre,0),Ambient Industrial),$stricmp($meta(genre,0),Deconstructed Club),$stricmp($meta(genre,0),EBM),$stricmp($meta(genre,0),Electro-Industrial),$stricmp($meta(genre,0),Aggrotech),$stricmp($meta(genre,0),Dark Electro),$stricmp($meta(genre,0),Futurepop),$stricmp($meta(genre,0),New Beat),$stricmp($meta(genre,0),Power Noise),$stricmp($meta(genre,0),Power Electronics),$stricmp($meta(genre,0),Death Industrial)),$set_style(back,$rgb(40,40,40)))
    $if($or($stricmp($meta(genre,0),Industrial?),$stricmp($meta(genre,0),Post-Industrial?),$stricmp($meta(genre,0),Ambient Industrial?),$stricmp($meta(genre,0),Deconstructed Club?),$stricmp($meta(genre,0),EBM?),$stricmp($meta(genre,0),Electro-Industrial?),$stricmp($meta(genre,0),Aggrotech?),$stricmp($meta(genre,0),Dark Electro?),$stricmp($meta(genre,0),Futurepop?),$stricmp($meta(genre,0),New Beat?),$stricmp($meta(genre,0),Power Noise?),$stricmp($meta(genre,0),Power Electronics?),$stricmp($meta(genre,0),Death Industrial?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Industrial?),$stricmp($meta(genre,0),Post-Industrial?),$stricmp($meta(genre,0),Ambient Industrial?),$stricmp($meta(genre,0),Deconstructed Club?),$stricmp($meta(genre,0),EBM?),$stricmp($meta(genre,0),Electro-Industrial?),$stricmp($meta(genre,0),Aggrotech?),$stricmp($meta(genre,0),Dark Electro?),$stricmp($meta(genre,0),Futurepop?),$stricmp($meta(genre,0),New Beat?),$stricmp($meta(genre,0),Power Noise?),$stricmp($meta(genre,0),Power Electronics?),$stricmp($meta(genre,0),Death Industrial?)),$set_style(back,$rgb(40,40,40)))
    $if($or($stricmp($meta(genre,0),Jazz),$stricmp($meta(genre,0),Avant-Garde Jazz),$stricmp($meta(genre,0),Spiritual Jazz),$stricmp($meta(genre,0),Bebop),$stricmp($meta(genre,0),Hard Bop),$stricmp($meta(genre,0),Post-Bop),$stricmp($meta(genre,0),Big Band),$stricmp($meta(genre,0),Progressive Jazz),$stricmp($meta(genre,0),Chamber Jazz),$stricmp($meta(genre,0),Cool Jazz),$stricmp($meta(genre,0),Dark Jazz),$stricmp($meta(genre,0),Free Jazz),$stricmp($meta(genre,0),Gypsy Jazz),$stricmp($meta(genre,0),Jazz Fusion),$stricmp($meta(genre,0),Acid Jazz),$stricmp($meta(genre,0),Jazz Funk),$stricmp($meta(genre,0),Smooth Jazz),$stricmp($meta(genre,0),Soul Jazz),$stricmp($meta(genre,0),Modal Jazz),$stricmp($meta(genre,0),New Orleans Jazz),$stricmp($meta(genre,0),Salsa),$stricmp($meta(genre,0),Swing),$stricmp($meta(genre,0),Third Stream),$stricmp($meta(genre,0),Spy Music)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Jazz),$stricmp($meta(genre,0),Avant-Garde Jazz),$stricmp($meta(genre,0),Spiritual Jazz),$stricmp($meta(genre,0),Bebop),$stricmp($meta(genre,0),Hard Bop),$stricmp($meta(genre,0),Post-Bop),$stricmp($meta(genre,0),Big Band),$stricmp($meta(genre,0),Progressive Jazz),$stricmp($meta(genre,0),Chamber Jazz),$stricmp($meta(genre,0),Cool Jazz),$stricmp($meta(genre,0),Dark Jazz),$stricmp($meta(genre,0),Free Jazz),$stricmp($meta(genre,0),Gypsy Jazz),$stricmp($meta(genre,0),Jazz Fusion),$stricmp($meta(genre,0),Acid Jazz),$stricmp($meta(genre,0),Jazz Funk),$stricmp($meta(genre,0),Smooth Jazz),$stricmp($meta(genre,0),Soul Jazz),$stricmp($meta(genre,0),Modal Jazz),$stricmp($meta(genre,0),New Orleans Jazz),$stricmp($meta(genre,0),Salsa),$stricmp($meta(genre,0),Swing),$stricmp($meta(genre,0),Third Stream),$stricmp($meta(genre,0),Spy Music)),$set_style(back,$rgb(135,206,250)))
    $if($or($stricmp($meta(genre,0),Jazz?),$stricmp($meta(genre,0),Avant-Garde Jazz?),$stricmp($meta(genre,0),Spiritual Jazz?),$stricmp($meta(genre,0),Bebop?),$stricmp($meta(genre,0),Hard Bop?),$stricmp($meta(genre,0),Post-Bop?),$stricmp($meta(genre,0),Big Band?),$stricmp($meta(genre,0),Progressive Jazz?),$stricmp($meta(genre,0),Chamber Jazz?),$stricmp($meta(genre,0),Cool Jazz?),$stricmp($meta(genre,0),Dark Jazz?),$stricmp($meta(genre,0),Free Jazz?),$stricmp($meta(genre,0),Gypsy Jazz?),$stricmp($meta(genre,0),Jazz Fusion?),$stricmp($meta(genre,0),Acid Jazz?),$stricmp($meta(genre,0),Jazz Funk?),$stricmp($meta(genre,0),Smooth Jazz?),$stricmp($meta(genre,0),Soul Jazz?),$stricmp($meta(genre,0),Modal Jazz?),$stricmp($meta(genre,0),New Orleans Jazz?),$stricmp($meta(genre,0),Salsa?),$stricmp($meta(genre,0),Swing?),$stricmp($meta(genre,0),Third Stream?),$stricmp($meta(genre,0),Spy Music?)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Jazz?),$stricmp($meta(genre,0),Avant-Garde Jazz?),$stricmp($meta(genre,0),Spiritual Jazz?),$stricmp($meta(genre,0),Bebop?),$stricmp($meta(genre,0),Hard Bop?),$stricmp($meta(genre,0),Post-Bop?),$stricmp($meta(genre,0),Big Band?),$stricmp($meta(genre,0),Progressive Jazz?),$stricmp($meta(genre,0),Chamber Jazz?),$stricmp($meta(genre,0),Cool Jazz?),$stricmp($meta(genre,0),Dark Jazz?),$stricmp($meta(genre,0),Free Jazz?),$stricmp($meta(genre,0),Gypsy Jazz?),$stricmp($meta(genre,0),Jazz Fusion?),$stricmp($meta(genre,0),Acid Jazz?),$stricmp($meta(genre,0),Jazz Funk?),$stricmp($meta(genre,0),Smooth Jazz?),$stricmp($meta(genre,0),Soul Jazz?),$stricmp($meta(genre,0),Modal Jazz?),$stricmp($meta(genre,0),New Orleans Jazz?),$stricmp($meta(genre,0),Salsa?),$stricmp($meta(genre,0),Swing?),$stricmp($meta(genre,0),Third Stream?),$stricmp($meta(genre,0),Spy Music?)),$set_style(back,$rgb(135,206,250)))
    $if($or($stricmp($meta(genre,0),Metal),$stricmp($meta(genre,0),Alternative Metal),$stricmp($meta(genre,0),Funk Metal),$stricmp($meta(genre,0),Nu-Metal),$stricmp($meta(genre,0),Doom Metal),$stricmp($meta(genre,0),Death Doom Metal),$stricmp($meta(genre,0),Drone Metal),$stricmp($meta(genre,0),Gothic Metal),$stricmp($meta(genre,0),Stoner Doom),$stricmp($meta(genre,0),Folk Metal),$stricmp($meta(genre,0),Heavy Metal),$stricmp($meta(genre,0),Celtic Metal),$stricmp($meta(genre,0),Glam Metal),$stricmp($meta(genre,0),Industrial Metal),$stricmp($meta(genre,0),Cyber Metal),$stricmp($meta(genre,0),Neue Deutsche Härte),$stricmp($meta(genre,0),J-Metal),$stricmp($meta(genre,0),Kawaii Metal),$stricmp($meta(genre,0),K-Metal),$stricmp($meta(genre,0),Metalcore),$stricmp($meta(genre,0),Deathcore),$stricmp($meta(genre,0),Mathcore),$stricmp($meta(genre,0),Nintendocore),$stricmp($meta(genre,0),Neoclassical Metal),$stricmp($meta(genre,0),Post-Metal),$stricmp($meta(genre,0),Progressive Metal),$stricmp($meta(genre,0),Djent),$stricmp($meta(genre,0),Rap Metal),$stricmp($meta(genre,0),Speed Metal),$stricmp($meta(genre,0),Power Metal),$stricmp($meta(genre,0),Pirate Metal),$stricmp($meta(genre,0),Thrash Metal),$stricmp($meta(genre,0),Black Metal),$stricmp($meta(genre,0),Atmospheric Black Metal),$stricmp($meta(genre,0),Blackgaze),$stricmp($meta(genre,0),Viking Metal),$stricmp($meta(genre,0),Death Metal),$stricmp($meta(genre,0),Grindcore),$stricmp($meta(genre,0),Melodic Death Metal),$stricmp($meta(genre,0),Tech-Death),$stricmp($meta(genre,0),Groove Metal),$stricmp($meta(genre,0),Symphonic Metal)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Metal),$stricmp($meta(genre,0),Alternative Metal),$stricmp($meta(genre,0),Funk Metal),$stricmp($meta(genre,0),Nu-Metal),$stricmp($meta(genre,0),Doom Metal),$stricmp($meta(genre,0),Death Doom Metal),$stricmp($meta(genre,0),Drone Metal),$stricmp($meta(genre,0),Gothic Metal),$stricmp($meta(genre,0),Stoner Doom),$stricmp($meta(genre,0),Folk Metal),$stricmp($meta(genre,0),Heavy Metal),$stricmp($meta(genre,0),Celtic Metal),$stricmp($meta(genre,0),Glam Metal),$stricmp($meta(genre,0),Industrial Metal),$stricmp($meta(genre,0),Cyber Metal),$stricmp($meta(genre,0),Neue Deutsche Härte),$stricmp($meta(genre,0),J-Metal),$stricmp($meta(genre,0),Kawaii Metal),$stricmp($meta(genre,0),K-Metal),$stricmp($meta(genre,0),Metalcore),$stricmp($meta(genre,0),Deathcore),$stricmp($meta(genre,0),Mathcore),$stricmp($meta(genre,0),Nintendocore),$stricmp($meta(genre,0),Neoclassical Metal),$stricmp($meta(genre,0),Post-Metal),$stricmp($meta(genre,0),Progressive Metal),$stricmp($meta(genre,0),Djent),$stricmp($meta(genre,0),Rap Metal),$stricmp($meta(genre,0),Speed Metal),$stricmp($meta(genre,0),Power Metal),$stricmp($meta(genre,0),Pirate Metal),$stricmp($meta(genre,0),Thrash Metal),$stricmp($meta(genre,0),Black Metal),$stricmp($meta(genre,0),Atmospheric Black Metal),$stricmp($meta(genre,0),Blackgaze),$stricmp($meta(genre,0),Viking Metal),$stricmp($meta(genre,0),Death Metal),$stricmp($meta(genre,0),Grindcore),$stricmp($meta(genre,0),Melodic Death Metal),$stricmp($meta(genre,0),Tech-Death),$stricmp($meta(genre,0),Groove Metal),$stricmp($meta(genre,0),Symphonic Metal)),$set_style(back,$rgb(0,58,0)))
    $if($or($stricmp($meta(genre,0),Metal?),$stricmp($meta(genre,0),Alternative Metal?),$stricmp($meta(genre,0),Funk Metal?),$stricmp($meta(genre,0),Nu-Metal?),$stricmp($meta(genre,0),Doom Metal?),$stricmp($meta(genre,0),Death Doom Metal?),$stricmp($meta(genre,0),Drone Metal?),$stricmp($meta(genre,0),Gothic Metal?),$stricmp($meta(genre,0),Stoner Doom?),$stricmp($meta(genre,0),Folk Metal?),$stricmp($meta(genre,0),Heavy Metal?),$stricmp($meta(genre,0),Celtic Metal?),$stricmp($meta(genre,0),Glam Metal?),$stricmp($meta(genre,0),Industrial Metal?),$stricmp($meta(genre,0),Cyber Metal?),$stricmp($meta(genre,0),Neue Deutsche Härte?),$stricmp($meta(genre,0),J-Metal?),$stricmp($meta(genre,0),Kawaii Metal?),$stricmp($meta(genre,0),K-Metal?),$stricmp($meta(genre,0),Metalcore?),$stricmp($meta(genre,0),Deathcore?),$stricmp($meta(genre,0),Mathcore?),$stricmp($meta(genre,0),Nintendocore?),$stricmp($meta(genre,0),Neoclassical Metal?),$stricmp($meta(genre,0),Post-Metal?),$stricmp($meta(genre,0),Progressive Metal?),$stricmp($meta(genre,0),Djent?),$stricmp($meta(genre,0),Rap Metal?),$stricmp($meta(genre,0),Speed Metal?),$stricmp($meta(genre,0),Power Metal?),$stricmp($meta(genre,0),Pirate Metal?),$stricmp($meta(genre,0),Thrash Metal?),$stricmp($meta(genre,0),Black Metal?),$stricmp($meta(genre,0),Atmospheric Black Metal?),$stricmp($meta(genre,0),Blackgaze?),$stricmp($meta(genre,0),Viking Metal?),$stricmp($meta(genre,0),Death Metal?),$stricmp($meta(genre,0),Grindcore?),$stricmp($meta(genre,0),Melodic Death Metal?),$stricmp($meta(genre,0),Tech-Death?),$stricmp($meta(genre,0),Groove Metal?),$stricmp($meta(genre,0),Symphonic Metal?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Metal?),$stricmp($meta(genre,0),Alternative Metal?),$stricmp($meta(genre,0),Funk Metal?),$stricmp($meta(genre,0),Nu-Metal?),$stricmp($meta(genre,0),Doom Metal?),$stricmp($meta(genre,0),Death Doom Metal?),$stricmp($meta(genre,0),Drone Metal?),$stricmp($meta(genre,0),Gothic Metal?),$stricmp($meta(genre,0),Stoner Doom?),$stricmp($meta(genre,0),Folk Metal?),$stricmp($meta(genre,0),Heavy Metal?),$stricmp($meta(genre,0),Celtic Metal?),$stricmp($meta(genre,0),Glam Metal?),$stricmp($meta(genre,0),Industrial Metal?),$stricmp($meta(genre,0),Cyber Metal?),$stricmp($meta(genre,0),Neue Deutsche Härte?),$stricmp($meta(genre,0),J-Metal?),$stricmp($meta(genre,0),Kawaii Metal?),$stricmp($meta(genre,0),K-Metal?),$stricmp($meta(genre,0),Metalcore?),$stricmp($meta(genre,0),Deathcore?),$stricmp($meta(genre,0),Mathcore?),$stricmp($meta(genre,0),Nintendocore?),$stricmp($meta(genre,0),Neoclassical Metal?),$stricmp($meta(genre,0),Post-Metal?),$stricmp($meta(genre,0),Progressive Metal?),$stricmp($meta(genre,0),Djent?),$stricmp($meta(genre,0),Rap Metal?),$stricmp($meta(genre,0),Speed Metal?),$stricmp($meta(genre,0),Power Metal?),$stricmp($meta(genre,0),Pirate Metal?),$stricmp($meta(genre,0),Thrash Metal?),$stricmp($meta(genre,0),Black Metal?),$stricmp($meta(genre,0),Atmospheric Black Metal?),$stricmp($meta(genre,0),Blackgaze?),$stricmp($meta(genre,0),Viking Metal?),$stricmp($meta(genre,0),Death Metal?),$stricmp($meta(genre,0),Grindcore?),$stricmp($meta(genre,0),Melodic Death Metal?),$stricmp($meta(genre,0),Tech-Death?),$stricmp($meta(genre,0),Groove Metal?),$stricmp($meta(genre,0),Symphonic Metal?)),$set_style(back,$rgb(0,58,0)))
    $if($or($stricmp($meta(genre,0),Psychedelia),$stricmp($meta(genre,0),Acid Rock),$stricmp($meta(genre,0),Psychedelic Folk),$stricmp($meta(genre,0),Psychedelic Pop),$stricmp($meta(genre,0),Psychedelic Rock),$stricmp($meta(genre,0),Heavy Psych),$stricmp($meta(genre,0),Neo-Psychedelia),$stricmp($meta(genre,0),Hypnagogic Pop),$stricmp($meta(genre,0),Raga Rock),$stricmp($meta(genre,0),Space Rock),$stricmp($meta(genre,0),Stoner Rock),$stricmp($meta(genre,0),Psychedelic Soul),$stricmp($meta(genre,0),Psychedelic Funk)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Psychedelia),$stricmp($meta(genre,0),Acid Rock),$stricmp($meta(genre,0),Psychedelic Folk),$stricmp($meta(genre,0),Psychedelic Pop),$stricmp($meta(genre,0),Psychedelic Rock),$stricmp($meta(genre,0),Heavy Psych),$stricmp($meta(genre,0),Neo-Psychedelia),$stricmp($meta(genre,0),Hypnagogic Pop),$stricmp($meta(genre,0),Raga Rock),$stricmp($meta(genre,0),Space Rock),$stricmp($meta(genre,0),Stoner Rock),$stricmp($meta(genre,0),Psychedelic Soul),$stricmp($meta(genre,0),Psychedelic Funk)),$set_style(back,$rgb(120,71,75)))
    $if($or($stricmp($meta(genre,0),Psychedelia?),$stricmp($meta(genre,0),Acid Rock?),$stricmp($meta(genre,0),Psychedelic Folk?),$stricmp($meta(genre,0),Psychedelic Pop?),$stricmp($meta(genre,0),Psychedelic Rock?),$stricmp($meta(genre,0),Heavy Psych?),$stricmp($meta(genre,0),Neo-Psychedelia?),$stricmp($meta(genre,0),Hypnagogic Pop?),$stricmp($meta(genre,0),Raga Rock?),$stricmp($meta(genre,0),Space Rock?),$stricmp($meta(genre,0),Stoner Rock?),$stricmp($meta(genre,0),Psychedelic Soul?),$stricmp($meta(genre,0),Psychedelic Funk?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Psychedelia?),$stricmp($meta(genre,0),Acid Rock?),$stricmp($meta(genre,0),Psychedelic Folk?),$stricmp($meta(genre,0),Psychedelic Pop?),$stricmp($meta(genre,0),Psychedelic Rock?),$stricmp($meta(genre,0),Heavy Psych?),$stricmp($meta(genre,0),Neo-Psychedelia?),$stricmp($meta(genre,0),Hypnagogic Pop?),$stricmp($meta(genre,0),Raga Rock?),$stricmp($meta(genre,0),Space Rock?),$stricmp($meta(genre,0),Stoner Rock?),$stricmp($meta(genre,0),Psychedelic Soul?),$stricmp($meta(genre,0),Psychedelic Funk?)),$set_style(back,$rgb(120,71,75)))
    $if($or($stricmp($meta(genre,0),Punk),$stricmp($meta(genre,0),2 Tone),$stricmp($meta(genre,0),Anarcho-Punk),$stricmp($meta(genre,0),Crust Punk),$stricmp($meta(genre,0),Art Punk),$stricmp($meta(genre,0),Cow Punk),$stricmp($meta(genre,0),Dance-Punk),$stricmp($meta(genre,0),Folk Punk),$stricmp($meta(genre,0),Celtic Punk),$stricmp($meta(genre,0),Hardcore Punk),$stricmp($meta(genre,0),Digital Hardcore),$stricmp($meta(genre,0),Melodic Hardcore),$stricmp($meta(genre,0),Post-Hardcore),$stricmp($meta(genre,0),Emo Punk),$stricmp($meta(genre,0),Emo Pop),$stricmp($meta(genre,0),Rapcore),$stricmp($meta(genre,0),Sludge Metal),$stricmp($meta(genre,0),Oi!),$stricmp($meta(genre,0),Horror Punk),$stricmp($meta(genre,0),Pop Punk),$stricmp($meta(genre,0),Post-Punk),$stricmp($meta(genre,0),Darkwave),$stricmp($meta(genre,0),Ethereal Wave),$stricmp($meta(genre,0),Neoclassical Darkwave),$stricmp($meta(genre,0),Post-Punk Revival),$stricmp($meta(genre,0),Punk Blues),$stricmp($meta(genre,0),Punk Jazz),$stricmp($meta(genre,0),Surf Punk),$stricmp($meta(genre,0),Synth Punk)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Punk),$stricmp($meta(genre,0),2 Tone),$stricmp($meta(genre,0),Anarcho-Punk),$stricmp($meta(genre,0),Crust Punk),$stricmp($meta(genre,0),Art Punk),$stricmp($meta(genre,0),Cow Punk),$stricmp($meta(genre,0),Dance-Punk),$stricmp($meta(genre,0),Folk Punk),$stricmp($meta(genre,0),Celtic Punk),$stricmp($meta(genre,0),Hardcore Punk),$stricmp($meta(genre,0),Digital Hardcore),$stricmp($meta(genre,0),Melodic Hardcore),$stricmp($meta(genre,0),Post-Hardcore),$stricmp($meta(genre,0),Emo Punk),$stricmp($meta(genre,0),Emo Pop),$stricmp($meta(genre,0),Rapcore),$stricmp($meta(genre,0),Sludge Metal),$stricmp($meta(genre,0),Oi!),$stricmp($meta(genre,0),Horror Punk),$stricmp($meta(genre,0),Pop Punk),$stricmp($meta(genre,0),Post-Punk),$stricmp($meta(genre,0),Darkwave),$stricmp($meta(genre,0),Ethereal Wave),$stricmp($meta(genre,0),Neoclassical Darkwave),$stricmp($meta(genre,0),Post-Punk Revival),$stricmp($meta(genre,0),Punk Blues),$stricmp($meta(genre,0),Punk Jazz),$stricmp($meta(genre,0),Surf Punk),$stricmp($meta(genre,0),Synth Punk)),$set_style(back,$rgb(58,0,58)))
    $if($or($stricmp($meta(genre,0),Punk?),$stricmp($meta(genre,0),2 Tone?),$stricmp($meta(genre,0),Anarcho-Punk?),$stricmp($meta(genre,0),Crust Punk?),$stricmp($meta(genre,0),Art Punk?),$stricmp($meta(genre,0),Cow Punk?),$stricmp($meta(genre,0),Dance-Punk?),$stricmp($meta(genre,0),Folk Punk?),$stricmp($meta(genre,0),Celtic Punk?),$stricmp($meta(genre,0),Hardcore Punk?),$stricmp($meta(genre,0),Digital Hardcore?),$stricmp($meta(genre,0),Melodic Hardcore?),$stricmp($meta(genre,0),Post-Hardcore?),$stricmp($meta(genre,0),Emo Punk?),$stricmp($meta(genre,0),Emo Pop?),$stricmp($meta(genre,0),Rapcore?),$stricmp($meta(genre,0),Sludge Metal?),$stricmp($meta(genre,0),Oi!?),$stricmp($meta(genre,0),Horror Punk?),$stricmp($meta(genre,0),Pop Punk?),$stricmp($meta(genre,0),Post-Punk?),$stricmp($meta(genre,0),Darkwave?),$stricmp($meta(genre,0),Ethereal Wave?),$stricmp($meta(genre,0),Neoclassical Darkwave?),$stricmp($meta(genre,0),Post-Punk Revival?),$stricmp($meta(genre,0),Punk Blues?),$stricmp($meta(genre,0),Punk Jazz?),$stricmp($meta(genre,0),Surf Punk?),$stricmp($meta(genre,0),Synth Punk?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Punk?),$stricmp($meta(genre,0),2 Tone?),$stricmp($meta(genre,0),Anarcho-Punk?),$stricmp($meta(genre,0),Crust Punk?),$stricmp($meta(genre,0),Art Punk?),$stricmp($meta(genre,0),Cow Punk?),$stricmp($meta(genre,0),Dance-Punk?),$stricmp($meta(genre,0),Folk Punk?),$stricmp($meta(genre,0),Celtic Punk?),$stricmp($meta(genre,0),Hardcore Punk?),$stricmp($meta(genre,0),Digital Hardcore?),$stricmp($meta(genre,0),Melodic Hardcore?),$stricmp($meta(genre,0),Post-Hardcore?),$stricmp($meta(genre,0),Emo Punk?),$stricmp($meta(genre,0),Emo Pop?),$stricmp($meta(genre,0),Rapcore?),$stricmp($meta(genre,0),Sludge Metal?),$stricmp($meta(genre,0),Oi!?),$stricmp($meta(genre,0),Horror Punk?),$stricmp($meta(genre,0),Pop Punk?),$stricmp($meta(genre,0),Post-Punk?),$stricmp($meta(genre,0),Darkwave?),$stricmp($meta(genre,0),Ethereal Wave?),$stricmp($meta(genre,0),Neoclassical Darkwave?),$stricmp($meta(genre,0),Post-Punk Revival?),$stricmp($meta(genre,0),Punk Blues?),$stricmp($meta(genre,0),Punk Jazz?),$stricmp($meta(genre,0),Surf Punk?),$stricmp($meta(genre,0),Synth Punk?)),$set_style(back,$rgb(58,0,58)))
    $if($or($stricmp($meta(genre,0),Rhythm and Blues),$stricmp($meta(genre,0),British Rhythm and Blues),$stricmp($meta(genre,0),Contemporary RnB),$stricmp($meta(genre,0),Alternative RnB),$stricmp($meta(genre,0),New Jack Swing),$stricmp($meta(genre,0),Doo-Wop),$stricmp($meta(genre,0),New Orleans RnB),$stricmp($meta(genre,0),Ska),$stricmp($meta(genre,0),Rocksteady),$stricmp($meta(genre,0),Soul),$stricmp($meta(genre,0),Country Soul),$stricmp($meta(genre,0),Funk),$stricmp($meta(genre,0),Deep Funk),$stricmp($meta(genre,0),Electro-Funk),$stricmp($meta(genre,0),Nu-Funk),$stricmp($meta(genre,0),Synth Funk),$stricmp($meta(genre,0),Latin Soul),$stricmp($meta(genre,0),Neo-Soul),$stricmp($meta(genre,0),Philly Soul),$stricmp($meta(genre,0),Smooth Soul),$stricmp($meta(genre,0),Southern Soul),$stricmp($meta(genre,0),Deep Soul)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Rhythm and Blues),$stricmp($meta(genre,0),British Rhythm and Blues),$stricmp($meta(genre,0),Contemporary RnB),$stricmp($meta(genre,0),Alternative RnB),$stricmp($meta(genre,0),New Jack Swing),$stricmp($meta(genre,0),Doo-Wop),$stricmp($meta(genre,0),New Orleans RnB),$stricmp($meta(genre,0),Ska),$stricmp($meta(genre,0),Rocksteady),$stricmp($meta(genre,0),Soul),$stricmp($meta(genre,0),Country Soul),$stricmp($meta(genre,0),Funk),$stricmp($meta(genre,0),Deep Funk),$stricmp($meta(genre,0),Electro-Funk),$stricmp($meta(genre,0),Nu-Funk),$stricmp($meta(genre,0),Synth Funk),$stricmp($meta(genre,0),Latin Soul),$stricmp($meta(genre,0),Neo-Soul),$stricmp($meta(genre,0),Philly Soul),$stricmp($meta(genre,0),Smooth Soul),$stricmp($meta(genre,0),Southern Soul),$stricmp($meta(genre,0),Deep Soul)),$set_style(back,$rgb(105,136,162)))
    $if($or($stricmp($meta(genre,0),Rhythm and Blues?),$stricmp($meta(genre,0),British Rhythm and Blues?),$stricmp($meta(genre,0),Contemporary RnB?),$stricmp($meta(genre,0),Alternative RnB?),$stricmp($meta(genre,0),New Jack Swing?),$stricmp($meta(genre,0),Doo-Wop?),$stricmp($meta(genre,0),New Orleans RnB?),$stricmp($meta(genre,0),Ska?),$stricmp($meta(genre,0),Rocksteady?),$stricmp($meta(genre,0),Soul?),$stricmp($meta(genre,0),Country Soul?),$stricmp($meta(genre,0),Funk?),$stricmp($meta(genre,0),Deep Funk?),$stricmp($meta(genre,0),Electro-Funk?),$stricmp($meta(genre,0),Nu-Funk?),$stricmp($meta(genre,0),Synth Funk?),$stricmp($meta(genre,0),Latin Soul?),$stricmp($meta(genre,0),Neo-Soul?),$stricmp($meta(genre,0),Philly Soul?),$stricmp($meta(genre,0),Smooth Soul?),$stricmp($meta(genre,0),Southern Soul?),$stricmp($meta(genre,0),Deep Soul?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Rhythm and Blues?),$stricmp($meta(genre,0),British Rhythm and Blues?),$stricmp($meta(genre,0),Contemporary RnB?),$stricmp($meta(genre,0),Alternative RnB?),$stricmp($meta(genre,0),New Jack Swing?),$stricmp($meta(genre,0),Doo-Wop?),$stricmp($meta(genre,0),New Orleans RnB?),$stricmp($meta(genre,0),Ska?),$stricmp($meta(genre,0),Rocksteady?),$stricmp($meta(genre,0),Soul?),$stricmp($meta(genre,0),Country Soul?),$stricmp($meta(genre,0),Funk?),$stricmp($meta(genre,0),Deep Funk?),$stricmp($meta(genre,0),Electro-Funk?),$stricmp($meta(genre,0),Nu-Funk?),$stricmp($meta(genre,0),Synth Funk?),$stricmp($meta(genre,0),Latin Soul?),$stricmp($meta(genre,0),Neo-Soul?),$stricmp($meta(genre,0),Philly Soul?),$stricmp($meta(genre,0),Smooth Soul?),$stricmp($meta(genre,0),Southern Soul?),$stricmp($meta(genre,0),Deep Soul?)),$set_style(back,$rgb(105,136,162)))
    $if($or($stricmp($meta(genre,0),Reggae),$stricmp($meta(genre,0),Dancehall),$stricmp($meta(genre,0),Digital Dancehall),$stricmp($meta(genre,0),Ragga),$stricmp($meta(genre,0),Reggaeton),$stricmp($meta(genre,0),Dub),$stricmp($meta(genre,0),Experimental Dub),$stricmp($meta(genre,0),Liquid Dub),$stricmp($meta(genre,0),Psydub),$stricmp($meta(genre,0),Lovers Rock),$stricmp($meta(genre,0),Reggae Fusion),$stricmp($meta(genre,0),Roots Reggae),$stricmp($meta(genre,0),Soundclash)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Reggae),$stricmp($meta(genre,0),Dancehall),$stricmp($meta(genre,0),Digital Dancehall),$stricmp($meta(genre,0),Ragga),$stricmp($meta(genre,0),Reggaeton),$stricmp($meta(genre,0),Dub),$stricmp($meta(genre,0),Experimental Dub),$stricmp($meta(genre,0),Liquid Dub),$stricmp($meta(genre,0),Psydub),$stricmp($meta(genre,0),Lovers Rock),$stricmp($meta(genre,0),Reggae Fusion),$stricmp($meta(genre,0),Roots Reggae),$stricmp($meta(genre,0),Soundclash)),$set_style(back,$rgb(120,68,33)))
    $if($or($stricmp($meta(genre,0),Reggae?),$stricmp($meta(genre,0),Dancehall?),$stricmp($meta(genre,0),Digital Dancehall?),$stricmp($meta(genre,0),Ragga?),$stricmp($meta(genre,0),Reggaeton?),$stricmp($meta(genre,0),Dub?),$stricmp($meta(genre,0),Experimental Dub?),$stricmp($meta(genre,0),Liquid Dub?),$stricmp($meta(genre,0),Psydub?),$stricmp($meta(genre,0),Lovers Rock?),$stricmp($meta(genre,0),Reggae Fusion?),$stricmp($meta(genre,0),Roots Reggae?),$stricmp($meta(genre,0),Soundclash?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Reggae?),$stricmp($meta(genre,0),Dancehall?),$stricmp($meta(genre,0),Digital Dancehall?),$stricmp($meta(genre,0),Ragga?),$stricmp($meta(genre,0),Reggaeton?),$stricmp($meta(genre,0),Dub?),$stricmp($meta(genre,0),Experimental Dub?),$stricmp($meta(genre,0),Liquid Dub?),$stricmp($meta(genre,0),Psydub?),$stricmp($meta(genre,0),Lovers Rock?),$stricmp($meta(genre,0),Reggae Fusion?),$stricmp($meta(genre,0),Roots Reggae?),$stricmp($meta(genre,0),Soundclash?)),$set_style(back,$rgb(120,68,33)))
    $if($or($stricmp($meta(genre,0),Rock),$stricmp($meta(genre,0),Acoustic Rock),$stricmp($meta(genre,0),Afro-Rock),$stricmp($meta(genre,0),Alternative Rock),$stricmp($meta(genre,0),Alternative Dance),$stricmp($meta(genre,0),Madchester),$stricmp($meta(genre,0),Dream Pop),$stricmp($meta(genre,0),Grunge),$stricmp($meta(genre,0),Post-Grunge),$stricmp($meta(genre,0),Indie Rock),$stricmp($meta(genre,0),Lo-Fi Indie),$stricmp($meta(genre,0),Math Rock),$stricmp($meta(genre,0),Midwest Emo),$stricmp($meta(genre,0),Noise Pop),$stricmp($meta(genre,0),Slowcore),$stricmp($meta(genre,0),Jangle Pop),$stricmp($meta(genre,0),Shoegaze),$stricmp($meta(genre,0),Arena Rock),$stricmp($meta(genre,0),Blues Rock),$stricmp($meta(genre,0),Boogie Rock),$stricmp($meta(genre,0),Hard Rock),$stricmp($meta(genre,0),Christian Rock),$stricmp($meta(genre,0),Country Rock),$stricmp($meta(genre,0),Dark Cabaret),$stricmp($meta(genre,0),Electronic Rock),$stricmp($meta(genre,0),Experimental Rock),$stricmp($meta(genre,0),Folk Rock),$stricmp($meta(genre,0),Anatolian Rock),$stricmp($meta(genre,0),Celtic Rock),$stricmp($meta(genre,0),Electric Rock),$stricmp($meta(genre,0),Funk Rock),$stricmp($meta(genre,0),Gothic Rock),$stricmp($meta(genre,0),Heartland Rock),$stricmp($meta(genre,0),Industrial Rock),$stricmp($meta(genre,0),Instrumental Rock),$stricmp($meta(genre,0),J-Rock),$stricmp($meta(genre,0),Jazz Rock),$stricmp($meta(genre,0),K-Rock),$stricmp($meta(genre,0),Beat Music),$stricmp($meta(genre,0),Merseybeat),$stricmp($meta(genre,0),Piano Rock),$stricmp($meta(genre,0),Power Pop),$stricmp($meta(genre,0),Soft Rock),$stricmp($meta(genre,0),Vocal Surf),$stricmp($meta(genre,0),Power Ballad),$stricmp($meta(genre,0),Progressive Rock),$stricmp($meta(genre,0),Art Rock),$stricmp($meta(genre,0),Glam Rock),$stricmp($meta(genre,0),Avant-Prog),$stricmp($meta(genre,0),Krautrock),$stricmp($meta(genre,0),Noise Rock),$stricmp($meta(genre,0),Post-Rock),$stricmp($meta(genre,0),Rock Opera),$stricmp($meta(genre,0),Symphonic Prog),$stricmp($meta(genre,0),Symphonic Rock),$stricmp($meta(genre,0),Pub Rock),$stricmp($meta(genre,0),Rap Rock),$stricmp($meta(genre,0),Reggae Rock),$stricmp($meta(genre,0),Rock and Roll),$stricmp($meta(genre,0),Rockabilly),$stricmp($meta(genre,0),Garage Rock),$stricmp($meta(genre,0),Garage Rock Revival),$stricmp($meta(genre,0),Roots Rock),$stricmp($meta(genre,0),Swamp Rock),$stricmp($meta(genre,0),Surf Rock),$stricmp($meta(genre,0),Hot Rod Rock)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Rock),$stricmp($meta(genre,0),Acoustic Rock),$stricmp($meta(genre,0),Afro-Rock),$stricmp($meta(genre,0),Alternative Rock),$stricmp($meta(genre,0),Alternative Dance),$stricmp($meta(genre,0),Madchester),$stricmp($meta(genre,0),Dream Pop),$stricmp($meta(genre,0),Grunge),$stricmp($meta(genre,0),Post-Grunge),$stricmp($meta(genre,0),Indie Rock),$stricmp($meta(genre,0),Lo-Fi Indie),$stricmp($meta(genre,0),Math Rock),$stricmp($meta(genre,0),Midwest Emo),$stricmp($meta(genre,0),Noise Pop),$stricmp($meta(genre,0),Slowcore),$stricmp($meta(genre,0),Jangle Pop),$stricmp($meta(genre,0),Shoegaze),$stricmp($meta(genre,0),Arena Rock),$stricmp($meta(genre,0),Blues Rock),$stricmp($meta(genre,0),Boogie Rock),$stricmp($meta(genre,0),Hard Rock),$stricmp($meta(genre,0),Christian Rock),$stricmp($meta(genre,0),Country Rock),$stricmp($meta(genre,0),Dark Cabaret),$stricmp($meta(genre,0),Electronic Rock),$stricmp($meta(genre,0),Experimental Rock),$stricmp($meta(genre,0),Folk Rock),$stricmp($meta(genre,0),Anatolian Rock),$stricmp($meta(genre,0),Celtic Rock),$stricmp($meta(genre,0),Electric Rock),$stricmp($meta(genre,0),Funk Rock),$stricmp($meta(genre,0),Gothic Rock),$stricmp($meta(genre,0),Heartland Rock),$stricmp($meta(genre,0),Industrial Rock),$stricmp($meta(genre,0),Instrumental Rock),$stricmp($meta(genre,0),J-Rock),$stricmp($meta(genre,0),Jazz Rock),$stricmp($meta(genre,0),K-Rock),$stricmp($meta(genre,0),Beat Music),$stricmp($meta(genre,0),Merseybeat),$stricmp($meta(genre,0),Piano Rock),$stricmp($meta(genre,0),Power Pop),$stricmp($meta(genre,0),Soft Rock),$stricmp($meta(genre,0),Vocal Surf),$stricmp($meta(genre,0),Power Ballad),$stricmp($meta(genre,0),Progressive Rock),$stricmp($meta(genre,0),Art Rock),$stricmp($meta(genre,0),Glam Rock),$stricmp($meta(genre,0),Avant-Prog),$stricmp($meta(genre,0),Krautrock),$stricmp($meta(genre,0),Noise Rock),$stricmp($meta(genre,0),Post-Rock),$stricmp($meta(genre,0),Rock Opera),$stricmp($meta(genre,0),Symphonic Prog),$stricmp($meta(genre,0),Symphonic Rock),$stricmp($meta(genre,0),Pub Rock),$stricmp($meta(genre,0),Rap Rock),$stricmp($meta(genre,0),Reggae Rock),$stricmp($meta(genre,0),Rock and Roll),$stricmp($meta(genre,0),Rockabilly),$stricmp($meta(genre,0),Garage Rock),$stricmp($meta(genre,0),Garage Rock Revival),$stricmp($meta(genre,0),Roots Rock),$stricmp($meta(genre,0),Swamp Rock),$stricmp($meta(genre,0),Surf Rock),$stricmp($meta(genre,0),Hot Rod Rock)),$set_style(back,$rgb(135,192,149)))
    $if($or($stricmp($meta(genre,0),Rock?),$stricmp($meta(genre,0),Acoustic Rock?),$stricmp($meta(genre,0),Afro-Rock?),$stricmp($meta(genre,0),Alternative Rock?),$stricmp($meta(genre,0),Alternative Dance?),$stricmp($meta(genre,0),Madchester?),$stricmp($meta(genre,0),Dream Pop?),$stricmp($meta(genre,0),Grunge?),$stricmp($meta(genre,0),Post-Grunge?),$stricmp($meta(genre,0),Indie Rock?),$stricmp($meta(genre,0),Lo-Fi Indie?),$stricmp($meta(genre,0),Math Rock?),$stricmp($meta(genre,0),Midwest Emo?),$stricmp($meta(genre,0),Noise Pop?),$stricmp($meta(genre,0),Slowcore?),$stricmp($meta(genre,0),Jangle Pop?),$stricmp($meta(genre,0),Shoegaze?),$stricmp($meta(genre,0),Arena Rock?),$stricmp($meta(genre,0),Blues Rock?),$stricmp($meta(genre,0),Boogie Rock?),$stricmp($meta(genre,0),Hard Rock?),$stricmp($meta(genre,0),Christian Rock?),$stricmp($meta(genre,0),Country Rock?),$stricmp($meta(genre,0),Dark Cabaret?),$stricmp($meta(genre,0),Electronic Rock?),$stricmp($meta(genre,0),Experimental Rock?),$stricmp($meta(genre,0),Folk Rock?),$stricmp($meta(genre,0),Anatolian Rock?),$stricmp($meta(genre,0),Celtic Rock?),$stricmp($meta(genre,0),Electric Rock?),$stricmp($meta(genre,0),Funk Rock?),$stricmp($meta(genre,0),Gothic Rock?),$stricmp($meta(genre,0),Heartland Rock?),$stricmp($meta(genre,0),Industrial Rock?),$stricmp($meta(genre,0),Instrumental Rock?),$stricmp($meta(genre,0),J-Rock?),$stricmp($meta(genre,0),Jazz Rock?),$stricmp($meta(genre,0),K-Rock?),$stricmp($meta(genre,0),Beat Music?),$stricmp($meta(genre,0),Merseybeat?),$stricmp($meta(genre,0),Piano Rock?),$stricmp($meta(genre,0),Power Pop?),$stricmp($meta(genre,0),Soft Rock?),$stricmp($meta(genre,0),Vocal Surf?),$stricmp($meta(genre,0),Power Ballad?),$stricmp($meta(genre,0),Progressive Rock?),$stricmp($meta(genre,0),Art Rock?),$stricmp($meta(genre,0),Glam Rock?),$stricmp($meta(genre,0),Avant-Prog?),$stricmp($meta(genre,0),Krautrock?),$stricmp($meta(genre,0),Noise Rock?),$stricmp($meta(genre,0),Post-Rock?),$stricmp($meta(genre,0),Rock Opera?),$stricmp($meta(genre,0),Symphonic Prog?),$stricmp($meta(genre,0),Symphonic Rock?),$stricmp($meta(genre,0),Pub Rock?),$stricmp($meta(genre,0),Rap Rock?),$stricmp($meta(genre,0),Reggae Rock?),$stricmp($meta(genre,0),Rock and Roll?),$stricmp($meta(genre,0),Rockabilly?),$stricmp($meta(genre,0),Garage Rock?),$stricmp($meta(genre,0),Garage Rock Revival?),$stricmp($meta(genre,0),Roots Rock?),$stricmp($meta(genre,0),Swamp Rock?),$stricmp($meta(genre,0),Surf Rock?),$stricmp($meta(genre,0),Hot Rod Rock?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Rock?),$stricmp($meta(genre,0),Acoustic Rock?),$stricmp($meta(genre,0),Afro-Rock?),$stricmp($meta(genre,0),Alternative Rock?),$stricmp($meta(genre,0),Alternative Dance?),$stricmp($meta(genre,0),Madchester?),$stricmp($meta(genre,0),Dream Pop?),$stricmp($meta(genre,0),Grunge?),$stricmp($meta(genre,0),Post-Grunge?),$stricmp($meta(genre,0),Indie Rock?),$stricmp($meta(genre,0),Lo-Fi Indie?),$stricmp($meta(genre,0),Math Rock?),$stricmp($meta(genre,0),Midwest Emo?),$stricmp($meta(genre,0),Noise Pop?),$stricmp($meta(genre,0),Slowcore?),$stricmp($meta(genre,0),Jangle Pop?),$stricmp($meta(genre,0),Shoegaze?),$stricmp($meta(genre,0),Arena Rock?),$stricmp($meta(genre,0),Blues Rock?),$stricmp($meta(genre,0),Boogie Rock?),$stricmp($meta(genre,0),Hard Rock?),$stricmp($meta(genre,0),Christian Rock?),$stricmp($meta(genre,0),Country Rock?),$stricmp($meta(genre,0),Dark Cabaret?),$stricmp($meta(genre,0),Electronic Rock?),$stricmp($meta(genre,0),Experimental Rock?),$stricmp($meta(genre,0),Folk Rock?),$stricmp($meta(genre,0),Anatolian Rock?),$stricmp($meta(genre,0),Celtic Rock?),$stricmp($meta(genre,0),Electric Rock?),$stricmp($meta(genre,0),Funk Rock?),$stricmp($meta(genre,0),Gothic Rock?),$stricmp($meta(genre,0),Heartland Rock?),$stricmp($meta(genre,0),Industrial Rock?),$stricmp($meta(genre,0),Instrumental Rock?),$stricmp($meta(genre,0),J-Rock?),$stricmp($meta(genre,0),Jazz Rock?),$stricmp($meta(genre,0),K-Rock?),$stricmp($meta(genre,0),Beat Music?),$stricmp($meta(genre,0),Merseybeat?),$stricmp($meta(genre,0),Piano Rock?),$stricmp($meta(genre,0),Power Pop?),$stricmp($meta(genre,0),Soft Rock?),$stricmp($meta(genre,0),Vocal Surf?),$stricmp($meta(genre,0),Power Ballad?),$stricmp($meta(genre,0),Progressive Rock?),$stricmp($meta(genre,0),Art Rock?),$stricmp($meta(genre,0),Glam Rock?),$stricmp($meta(genre,0),Avant-Prog?),$stricmp($meta(genre,0),Krautrock?),$stricmp($meta(genre,0),Noise Rock?),$stricmp($meta(genre,0),Post-Rock?),$stricmp($meta(genre,0),Rock Opera?),$stricmp($meta(genre,0),Symphonic Prog?),$stricmp($meta(genre,0),Symphonic Rock?),$stricmp($meta(genre,0),Pub Rock?),$stricmp($meta(genre,0),Rap Rock?),$stricmp($meta(genre,0),Reggae Rock?),$stricmp($meta(genre,0),Rock and Roll?),$stricmp($meta(genre,0),Rockabilly?),$stricmp($meta(genre,0),Garage Rock?),$stricmp($meta(genre,0),Garage Rock Revival?),$stricmp($meta(genre,0),Roots Rock?),$stricmp($meta(genre,0),Swamp Rock?),$stricmp($meta(genre,0),Surf Rock?),$stricmp($meta(genre,0),Hot Rod Rock?)),$set_style(back,$rgb(135,192,149)))
    $if($or($stricmp($meta(genre,0),Techno),$stricmp($meta(genre,0),Belgian Techno),$stricmp($meta(genre,0),Peak Time Techno),$stricmp($meta(genre,0),Detroit Techno),$stricmp($meta(genre,0),Acid Techno),$stricmp($meta(genre,0),Ambient Techno),$stricmp($meta(genre,0),Bleep Techno),$stricmp($meta(genre,0),Eurotechno),$stricmp($meta(genre,0),Dutch Techno),$stricmp($meta(genre,0),Hard Techno),$stricmp($meta(genre,0),'Mákina'),$stricmp($meta(genre,0),Big Room Techno),$stricmp($meta(genre,0),Industrial Techno),$stricmp($meta(genre,0),Tribal Techno),$stricmp($meta(genre,0),Hardgroove Techno),$stricmp($meta(genre,0),Minimal Techno),$stricmp($meta(genre,0),Deep Techno),$stricmp($meta(genre,0),Dub Techno),$stricmp($meta(genre,0),Melodic Techno),$stricmp($meta(genre,0),Experimental Techno),$stricmp($meta(genre,0),Wonky Techno)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Techno),$stricmp($meta(genre,0),Belgian Techno),$stricmp($meta(genre,0),Peak Time Techno),$stricmp($meta(genre,0),Detroit Techno),$stricmp($meta(genre,0),Acid Techno),$stricmp($meta(genre,0),Ambient Techno),$stricmp($meta(genre,0),Bleep Techno),$stricmp($meta(genre,0),Eurotechno),$stricmp($meta(genre,0),Dutch Techno),$stricmp($meta(genre,0),Hard Techno),$stricmp($meta(genre,0),'Mákina'),$stricmp($meta(genre,0),Big Room Techno),$stricmp($meta(genre,0),Industrial Techno),$stricmp($meta(genre,0),Tribal Techno),$stricmp($meta(genre,0),Hardgroove Techno),$stricmp($meta(genre,0),Minimal Techno),$stricmp($meta(genre,0),Deep Techno),$stricmp($meta(genre,0),Dub Techno),$stricmp($meta(genre,0),Melodic Techno),$stricmp($meta(genre,0),Experimental Techno),$stricmp($meta(genre,0),Wonky Techno)),$set_style(back,$rgb(42,63,215)))
    $if($or($stricmp($meta(genre,0),Techno?),$stricmp($meta(genre,0),Belgian Techno?),$stricmp($meta(genre,0),Peak Time Techno?),$stricmp($meta(genre,0),Detroit Techno?),$stricmp($meta(genre,0),Acid Techno?),$stricmp($meta(genre,0),Ambient Techno?),$stricmp($meta(genre,0),Bleep Techno?),$stricmp($meta(genre,0),Eurotechno?),$stricmp($meta(genre,0),Dutch Techno?),$stricmp($meta(genre,0),Hard Techno?),$stricmp($meta(genre,0),'Mákina?'),$stricmp($meta(genre,0),Big Room Techno?),$stricmp($meta(genre,0),Industrial Techno?),$stricmp($meta(genre,0),Tribal Techno?),$stricmp($meta(genre,0),Hardgroove Techno?),$stricmp($meta(genre,0),Minimal Techno?),$stricmp($meta(genre,0),Deep Techno?),$stricmp($meta(genre,0),Dub Techno?),$stricmp($meta(genre,0),Melodic Techno?),$stricmp($meta(genre,0),Experimental Techno?),$stricmp($meta(genre,0),Wonky Techno?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Techno?),$stricmp($meta(genre,0),Belgian Techno?),$stricmp($meta(genre,0),Peak Time Techno?),$stricmp($meta(genre,0),Detroit Techno?),$stricmp($meta(genre,0),Acid Techno?),$stricmp($meta(genre,0),Ambient Techno?),$stricmp($meta(genre,0),Bleep Techno?),$stricmp($meta(genre,0),Eurotechno?),$stricmp($meta(genre,0),Dutch Techno?),$stricmp($meta(genre,0),Hard Techno?),$stricmp($meta(genre,0),'Mákina?'),$stricmp($meta(genre,0),Big Room Techno?),$stricmp($meta(genre,0),Industrial Techno?),$stricmp($meta(genre,0),Tribal Techno?),$stricmp($meta(genre,0),Hardgroove Techno?),$stricmp($meta(genre,0),Minimal Techno?),$stricmp($meta(genre,0),Deep Techno?),$stricmp($meta(genre,0),Dub Techno?),$stricmp($meta(genre,0),Melodic Techno?),$stricmp($meta(genre,0),Experimental Techno?),$stricmp($meta(genre,0),Wonky Techno?)),$set_style(back,$rgb(42,63,215)))
    $if($or($stricmp($meta(genre,0),Trance),$stricmp($meta(genre,0),Acid Trance),$stricmp($meta(genre,0),Hard Trance),$stricmp($meta(genre,0),Hands Up),$stricmp($meta(genre,0),Goa Trance),$stricmp($meta(genre,0),Nitzhonot),$stricmp($meta(genre,0),Psytrance),$stricmp($meta(genre,0),Darkpsy),$stricmp($meta(genre,0),Full-On),$stricmp($meta(genre,0),Progressive Psytrance),$stricmp($meta(genre,0),Ambient Trance),$stricmp($meta(genre,0),Balearic Trance),$stricmp($meta(genre,0),Dream Trance),$stricmp($meta(genre,0),Progressive Trance),$stricmp($meta(genre,0),Trouse),$stricmp($meta(genre,0),Uplifting Trance),$stricmp($meta(genre,0),Breaktrance),$stricmp($meta(genre,0),Tech Trance),$stricmp($meta(genre,0),Electro Trance),$stricmp($meta(genre,0),Big Room Trance),$stricmp($meta(genre,0),Vocal Trance)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Trance),$stricmp($meta(genre,0),Acid Trance),$stricmp($meta(genre,0),Hard Trance),$stricmp($meta(genre,0),Hands Up),$stricmp($meta(genre,0),Goa Trance),$stricmp($meta(genre,0),Nitzhonot),$stricmp($meta(genre,0),Psytrance),$stricmp($meta(genre,0),Darkpsy),$stricmp($meta(genre,0),Full-On),$stricmp($meta(genre,0),Progressive Psytrance),$stricmp($meta(genre,0),Ambient Trance),$stricmp($meta(genre,0),Balearic Trance),$stricmp($meta(genre,0),Dream Trance),$stricmp($meta(genre,0),Progressive Trance),$stricmp($meta(genre,0),Trouse),$stricmp($meta(genre,0),Uplifting Trance),$stricmp($meta(genre,0),Breaktrance),$stricmp($meta(genre,0),Tech Trance),$stricmp($meta(genre,0),Electro Trance),$stricmp($meta(genre,0),Big Room Trance),$stricmp($meta(genre,0),Vocal Trance)),$set_style(back,$rgb(0,106,235)))
    $if($or($stricmp($meta(genre,0),Trance?),$stricmp($meta(genre,0),Acid Trance?),$stricmp($meta(genre,0),Hard Trance?),$stricmp($meta(genre,0),Hands Up?),$stricmp($meta(genre,0),Goa Trance?),$stricmp($meta(genre,0),Nitzhonot?),$stricmp($meta(genre,0),Psytrance?),$stricmp($meta(genre,0),Darkpsy?),$stricmp($meta(genre,0),Full-On?),$stricmp($meta(genre,0),Progressive Psytrance?),$stricmp($meta(genre,0),Ambient Trance?),$stricmp($meta(genre,0),Balearic Trance?),$stricmp($meta(genre,0),Dream Trance?),$stricmp($meta(genre,0),Progressive Trance?),$stricmp($meta(genre,0),Trouse?),$stricmp($meta(genre,0),Uplifting Trance?),$stricmp($meta(genre,0),Breaktrance?),$stricmp($meta(genre,0),Tech Trance?),$stricmp($meta(genre,0),Electro Trance?),$stricmp($meta(genre,0),Big Room Trance?),$stricmp($meta(genre,0),Vocal Trance?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Trance?),$stricmp($meta(genre,0),Acid Trance?),$stricmp($meta(genre,0),Hard Trance?),$stricmp($meta(genre,0),Hands Up?),$stricmp($meta(genre,0),Goa Trance?),$stricmp($meta(genre,0),Nitzhonot?),$stricmp($meta(genre,0),Psytrance?),$stricmp($meta(genre,0),Darkpsy?),$stricmp($meta(genre,0),Full-On?),$stricmp($meta(genre,0),Progressive Psytrance?),$stricmp($meta(genre,0),Ambient Trance?),$stricmp($meta(genre,0),Balearic Trance?),$stricmp($meta(genre,0),Dream Trance?),$stricmp($meta(genre,0),Progressive Trance?),$stricmp($meta(genre,0),Trouse?),$stricmp($meta(genre,0),Uplifting Trance?),$stricmp($meta(genre,0),Breaktrance?),$stricmp($meta(genre,0),Tech Trance?),$stricmp($meta(genre,0),Electro Trance?),$stricmp($meta(genre,0),Big Room Trance?),$stricmp($meta(genre,0),Vocal Trance?)),$set_style(back,$rgb(0,106,235)))
    $if($or($stricmp($meta(genre,0),Trap),$stricmp($meta(genre,0),Festival Trap),$stricmp($meta(genre,0),Heaven Trap),$stricmp($meta(genre,0),Hybrid Trap),$stricmp($meta(genre,0),Hard Trap),$stricmp($meta(genre,0),Lo-Fi Trap),$stricmp($meta(genre,0),Twerk Trap),$stricmp($meta(genre,0),Wave),$stricmp($meta(genre,0),Experimental Trap),$stricmp($meta(genre,0),Acid Trap)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Trap),$stricmp($meta(genre,0),Festival Trap),$stricmp($meta(genre,0),Heaven Trap),$stricmp($meta(genre,0),Hybrid Trap),$stricmp($meta(genre,0),Hard Trap),$stricmp($meta(genre,0),Lo-Fi Trap),$stricmp($meta(genre,0),Twerk Trap),$stricmp($meta(genre,0),Wave),$stricmp($meta(genre,0),Experimental Trap),$stricmp($meta(genre,0),Acid Trap)),$set_style(back,$rgb(129,0,41)))
    $if($or($stricmp($meta(genre,0),Trap?),$stricmp($meta(genre,0),Festival Trap?),$stricmp($meta(genre,0),Heaven Trap?),$stricmp($meta(genre,0),Hybrid Trap?),$stricmp($meta(genre,0),Hard Trap?),$stricmp($meta(genre,0),Lo-Fi Trap?),$stricmp($meta(genre,0),Twerk Trap?),$stricmp($meta(genre,0),Wave?),$stricmp($meta(genre,0),Experimental Trap?),$stricmp($meta(genre,0),Acid Trap?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Trap?),$stricmp($meta(genre,0),Festival Trap?),$stricmp($meta(genre,0),Heaven Trap?),$stricmp($meta(genre,0),Hybrid Trap?),$stricmp($meta(genre,0),Hard Trap?),$stricmp($meta(genre,0),Lo-Fi Trap?),$stricmp($meta(genre,0),Twerk Trap?),$stricmp($meta(genre,0),Wave?),$stricmp($meta(genre,0),Experimental Trap?),$stricmp($meta(genre,0),Acid Trap?)),$set_style(back,$rgb(129,0,41)))
    $if($or($stricmp($meta(genre,0),UK Garage),$stricmp($meta(genre,0),2-Step Garage),$stricmp($meta(genre,0),Breakstep),$stricmp($meta(genre,0),Grime),$stricmp($meta(genre,0),Weightless),$stricmp($meta(genre,0),Future Garage),$stricmp($meta(genre,0),Speed Garage),$stricmp($meta(genre,0),Bassline Garage),$stricmp($meta(genre,0),UK Funky),$stricmp($meta(genre,0),UK Bass)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),UK Garage),$stricmp($meta(genre,0),2-Step Garage),$stricmp($meta(genre,0),Breakstep),$stricmp($meta(genre,0),Grime),$stricmp($meta(genre,0),Weightless),$stricmp($meta(genre,0),Future Garage),$stricmp($meta(genre,0),Speed Garage),$stricmp($meta(genre,0),Bassline Garage),$stricmp($meta(genre,0),UK Funky),$stricmp($meta(genre,0),UK Bass)),$set_style(back,$rgb(191,127,255)))
    $if($or($stricmp($meta(genre,0),UK Garage?),$stricmp($meta(genre,0),2-Step Garage?),$stricmp($meta(genre,0),Breakstep?),$stricmp($meta(genre,0),Grime?),$stricmp($meta(genre,0),Weightless?),$stricmp($meta(genre,0),Future Garage?),$stricmp($meta(genre,0),Speed Garage?),$stricmp($meta(genre,0),Bassline Garage?),$stricmp($meta(genre,0),UK Funky?),$stricmp($meta(genre,0),UK Bass?)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),UK Garage?),$stricmp($meta(genre,0),2-Step Garage?),$stricmp($meta(genre,0),Breakstep?),$stricmp($meta(genre,0),Grime?),$stricmp($meta(genre,0),Weightless?),$stricmp($meta(genre,0),Future Garage?),$stricmp($meta(genre,0),Speed Garage?),$stricmp($meta(genre,0),Bassline Garage?),$stricmp($meta(genre,0),UK Funky?),$stricmp($meta(genre,0),UK Bass?)),$set_style(back,$rgb(191,127,255)))
    $if($or($stricmp($meta(genre,0),Vaporwave),$stricmp($meta(genre,0),Broken Transmission),$stricmp($meta(genre,0),Future Funk),$stricmp($meta(genre,0),Hardvapor),$stricmp($meta(genre,0),Hypnagogic Vaporwave),$stricmp($meta(genre,0),Ocean Grunge),$stricmp($meta(genre,0),Utopian Virtual),$stricmp($meta(genre,0),Mallsoft),$stricmp($meta(genre,0),Vaporhop),$stricmp($meta(genre,0),Vaportrap)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Vaporwave),$stricmp($meta(genre,0),Broken Transmission),$stricmp($meta(genre,0),Future Funk),$stricmp($meta(genre,0),Hardvapor),$stricmp($meta(genre,0),Hypnagogic Vaporwave),$stricmp($meta(genre,0),Ocean Grunge),$stricmp($meta(genre,0),Utopian Virtual),$stricmp($meta(genre,0),Mallsoft),$stricmp($meta(genre,0),Vaporhop),$stricmp($meta(genre,0),Vaportrap)),$set_style(back,$rgb(236,0,219)))
    $if($or($stricmp($meta(genre,0),Vaporwave?),$stricmp($meta(genre,0),Broken Transmission?),$stricmp($meta(genre,0),Future Funk?),$stricmp($meta(genre,0),Hardvapor?),$stricmp($meta(genre,0),Hypnagogic Vaporwave?),$stricmp($meta(genre,0),Ocean Grunge?),$stricmp($meta(genre,0),Utopian Virtual?),$stricmp($meta(genre,0),Mallsoft?),$stricmp($meta(genre,0),Vaporhop?),$stricmp($meta(genre,0),Vaportrap?)),$set_style(text,$rgb(255,255,255),$rgb(255,255,255)))
    $if($or($stricmp($meta(genre,0),Vaporwave?),$stricmp($meta(genre,0),Broken Transmission?),$stricmp($meta(genre,0),Future Funk?),$stricmp($meta(genre,0),Hardvapor?),$stricmp($meta(genre,0),Hypnagogic Vaporwave?),$stricmp($meta(genre,0),Ocean Grunge?),$stricmp($meta(genre,0),Utopian Virtual?),$stricmp($meta(genre,0),Mallsoft?),$stricmp($meta(genre,0),Vaporhop?),$stricmp($meta(genre,0),Vaportrap?)),$set_style(back,$rgb(236,0,219)))
    $if($or($stricmp($meta(genre,0),Miscellaneous),$stricmp($meta(genre,0),Christmas Music),$stricmp($meta(genre,0),Comedic Skit),$stricmp($meta(genre,0),Film Soundtrack),$stricmp($meta(genre,0),Hymn),$stricmp($meta(genre,0),Interlude),$stricmp($meta(genre,0),Intro),$stricmp($meta(genre,0),Mashup),$stricmp($meta(genre,0),Meme Music),$stricmp($meta(genre,0),Musical Theatre),$stricmp($meta(genre,0),Nature Recordings),$stricmp($meta(genre,0),Novelty),$stricmp($meta(genre,0),Outro),$stricmp($meta(genre,0),Sea Shanties),$stricmp($meta(genre,0),Seapunk),$stricmp($meta(genre,0),Sound Effect),$stricmp($meta(genre,0),Spoken Word),$stricmp($meta(genre,0),Poetry),$stricmp($meta(genre,0),Beat Poetry),$stricmp($meta(genre,0),Speeches),$stricmp($meta(genre,0),Television Music),$stricmp($meta(genre,0),Video Game Music),$stricmp($meta(genre,0),Vocaloid),$stricmp($meta(genre,0),Mix)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Miscellaneous),$stricmp($meta(genre,0),Christmas Music),$stricmp($meta(genre,0),Comedic Skit),$stricmp($meta(genre,0),Film Soundtrack),$stricmp($meta(genre,0),Hymn),$stricmp($meta(genre,0),Interlude),$stricmp($meta(genre,0),Intro),$stricmp($meta(genre,0),Mashup),$stricmp($meta(genre,0),Meme Music),$stricmp($meta(genre,0),Musical Theatre),$stricmp($meta(genre,0),Nature Recordings),$stricmp($meta(genre,0),Novelty),$stricmp($meta(genre,0),Outro),$stricmp($meta(genre,0),Sea Shanties),$stricmp($meta(genre,0),Seapunk),$stricmp($meta(genre,0),Sound Effect),$stricmp($meta(genre,0),Spoken Word),$stricmp($meta(genre,0),Poetry),$stricmp($meta(genre,0),Beat Poetry),$stricmp($meta(genre,0),Speeches),$stricmp($meta(genre,0),Television Music),$stricmp($meta(genre,0),Video Game Music),$stricmp($meta(genre,0),Vocaloid),$stricmp($meta(genre,0),Mix)),$set_style(back,$rgb(185,185,185)))
    $if($or($stricmp($meta(genre,0),Miscellaneous?),$stricmp($meta(genre,0),Christmas Music?),$stricmp($meta(genre,0),Comedic Skit?),$stricmp($meta(genre,0),Film Soundtrack?),$stricmp($meta(genre,0),Hymn?),$stricmp($meta(genre,0),Interlude?),$stricmp($meta(genre,0),Intro?),$stricmp($meta(genre,0),Mashup?),$stricmp($meta(genre,0),Meme Music?),$stricmp($meta(genre,0),Musical Theatre?),$stricmp($meta(genre,0),Nature Recordings?),$stricmp($meta(genre,0),Novelty?),$stricmp($meta(genre,0),Outro?),$stricmp($meta(genre,0),Sea Shanties?),$stricmp($meta(genre,0),Seapunk?),$stricmp($meta(genre,0),Sound Effect?),$stricmp($meta(genre,0),Spoken Word?),$stricmp($meta(genre,0),Poetry?),$stricmp($meta(genre,0),Beat Poetry?),$stricmp($meta(genre,0),Speeches?),$stricmp($meta(genre,0),Television Music?),$stricmp($meta(genre,0),Video Game Music?),$stricmp($meta(genre,0),Vocaloid?),$stricmp($meta(genre,0),Mix?)),$set_style(text,$rgb(0,0,0),$rgb(0,0,0)))
    $if($or($stricmp($meta(genre,0),Miscellaneous?),$stricmp($meta(genre,0),Christmas Music?),$stricmp($meta(genre,0),Comedic Skit?),$stricmp($meta(genre,0),Film Soundtrack?),$stricmp($meta(genre,0),Hymn?),$stricmp($meta(genre,0),Interlude?),$stricmp($meta(genre,0),Intro?),$stricmp($meta(genre,0),Mashup?),$stricmp($meta(genre,0),Meme Music?),$stricmp($meta(genre,0),Musical Theatre?),$stricmp($meta(genre,0),Nature Recordings?),$stricmp($meta(genre,0),Novelty?),$stricmp($meta(genre,0),Outro?),$stricmp($meta(genre,0),Sea Shanties?),$stricmp($meta(genre,0),Seapunk?),$stricmp($meta(genre,0),Sound Effect?),$stricmp($meta(genre,0),Spoken Word?),$stricmp($meta(genre,0),Poetry?),$stricmp($meta(genre,0),Beat Poetry?),$stricmp($meta(genre,0),Speeches?),$stricmp($meta(genre,0),Television Music?),$stricmp($meta(genre,0),Video Game Music?),$stricmp($meta(genre,0),Vocaloid?),$stricmp($meta(genre,0),Mix?)),$set_style(back,$rgb(185,185,185)))
    $if(_is_group,$set_style(back,$rgb(100,100,100),$rgb(50,50,50)),$set_style(text,$rgb(255,255,255)))

     */


    // extras:
    // acid trap
    // 7-step
    // ambient americana
    // baile funk
    // drum and bass? no workie
    // jingle
    // mix
    // outro
    // sound effect
    public async Task FoobarCommand(DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        try
        {
            await CheckIfCacheExpired(context);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error");
        }


        var meta = FoobarFunction("meta", "genre", 0);

        var sb = new StringBuilder();

        var white = rgb(255, 255, 255);
        var black = rgb(0, 0, 0);

        sb.AppendLine(FoobarFunction("set_style", "text", white, white));
        sb.AppendLine(FoobarFunction("set_style", "back", black, rgb(50, 50, 50)));

        var grouped = GenreNode.All.Where(g => !g.IsRoot).GroupBy(g => g.Color);

        foreach (var group in grouped)
        {
            var compares = group.Select(g =>
                                {
                                    if (g.Name.Contains(" & "))
                                        return $"{compare(SafeString(g.Name), meta)}{compare(SafeString(g.Name.Replace(" & ", " and ")), meta)}";
                                    if (g.Name.Contains("R&B"))
                                        return $"{compare(SafeString(g.Name), meta)}{compare(SafeString(g.Name.Replace("R&B", "RnB")), meta)}";
                                    return $"{compare(SafeString(g.Name), meta)}";
                                })
                                .ToArray<object>();
            var color = group.Key;
            var textColor = Color.Default;

            //sb.AppendLine(@if(FoobarFunction("or", compares), FoobarFunction("set_style", "text", rgb(textColor.R, textColor.G, textColor.B), rgb(textColor.R, textColor.G, textColor.B))));
            sb.AppendLine(@if(FoobarFunction("or", compares), FoobarFunction("set_style", "back", rgb(color.R, color.G, color.B))));

            compares = group.Select(g =>
                            {
                                if (g.Name.Contains(" & "))
                                    return $"{compare(SafeString(g.Name), meta)}{compare(SafeString(g.Name.Replace(" & ", " and ") + "?"), meta)}";
                                if (g.Name.Contains("R&B"))
                                    return $"{compare(SafeString(g.Name), meta)}{compare(SafeString(g.Name.Replace("R&B", "RnB") + "?"), meta)}";
                                return $"{compare(SafeString(g.Name + "?"), meta)}";
                            })
                            .ToArray<object>();
            //sb.AppendLine(@if(FoobarFunction("or", compares), FoobarFunction("set_style", "text", rgb(textColor.R, textColor.G, textColor.B), rgb(textColor.R, textColor.G, textColor.B))));
            sb.AppendLine(@if(FoobarFunction("or", compares), FoobarFunction("set_style", "back", rgb(color.R, color.G, color.B))));
        }

        await context.SendOrAttachment(sb.ToString());
    }

    private static string FoobarFunction(string name, params object?[] parameters)
    {
        var sb = new StringBuilder($"${name}(");
        sb.Append(string.Join(",", parameters.Where(p => p != null)));
        return sb.Append(')').ToString();
    }

    private static string SafeString(string str)
    {
        str = str.Replace("'", "''");
        return $"'{str}'";
    }

    // ReSharper disable once InconsistentNaming
    static string rgb(int r, int g, int b)
    {
        return FoobarFunction("rgb", r, g, b);
    }

    // ReSharper disable once InconsistentNaming
    static string @if(string condition, string then, string? @else = null)
    {
        return FoobarFunction("if", condition, then, @else);
    }


    // ReSharper disable once InconsistentNaming
    string compare(string genre, string meta)
    {
        // ReSharper disable once StringLiteralTypo
        return FoobarFunction("stricmp", meta, genre);
    }

#endregion
    
#region Not In Tree

    public const string CMD_NOTINTREE_NAME = "not-in-tree";
    public const string CMD_NOTINTREE_DESCRIPTION = "Search for tracks on the sheet";
    public const string CMD_NOTINTREE_SEARCH_DESCRIPTION = "todo";

    public async Task NotInTreeCommand(DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var usedSubgenres = GetAllSubgenres();
        var unusedSubgenres = usedSubgenres.Except(treeSubgenres).ToArray();

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        
        var sb = new StringBuilder();

        foreach (var subgenre in unusedSubgenres.OrderBy(s => s))
        {
            var tracks = _entries.Where(e => e.SubgenresList.Contains(subgenre)).ToArray();

            foreach (var track in tracks)
            {
                csv.WriteRecord(new
                {
                    subgenre,
                    track.OriginalArtists,
                    track.Title,
                    track.Sheet
                });
                csv.NextRecord();
            }
            
            if (tracks.Length > 5)
                sb.AppendLine($"'{subgenre}' ({tracks.Length} usages)");
            else sb.AppendLine($"'{subgenre}' ({string.Join(", ", tracks.Select(t => $"{t.OriginalArtists} - {t.Title} [{t.Sheet}]"))})");
        }
        
        csv.Flush();
        writer.Flush();

        await context.SendOrAttachment(sb.ToString());
        await context.FollowupWithFileAsync(stream, "text.csv");

    }

#endregion
}

public class CollabNode
{
    public string Name { get; init; }

    public bool IsRoot { get; init; }

    [JsonIgnore]
    public CollabNode? Parent { get; set; }

    public List<CollabNode> SubNodes { get; set; } = new List<CollabNode>();

    public void AddSubgenre(CollabNode node)
    {
        /*if (node.Parent is not null)
        {
            if (node.Parent == this)
                throw new Exception("Subgenre already added to this parent");
            throw new Exception("Parent already set");
        }*/
        foreach (var subNode in SubNodes)
            if (string.Equals(subNode.Name, node.Name, StringComparison.OrdinalIgnoreCase))
                return;

        node.Parent = this;
        SubNodes.Add(node);
    }

    public bool ShouldSerializeSubNodes()
    {
        return SubNodes.Count > 0;
    }

    public bool ShouldSerializeIsRoot()
    {
        return IsRoot;
    }
}