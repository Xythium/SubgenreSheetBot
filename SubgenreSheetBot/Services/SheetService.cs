using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
            throw new Exception();

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

    private async Task CheckIfCacheExpired(DynamicContext context)
    {
        if (_lastTime is null || DateTime.UtcNow.Subtract(_lastTime.Value).TotalSeconds > TimeSpan.FromMinutes(5).TotalSeconds)
        {
            var now = DateTime.UtcNow;

            await GetValuesFromSheet(context);
            await GetGenreTreeFromSheet(context);

            mostCommonSubgenres = null;

            _lastTime = DateTime.UtcNow;
            Log.Information("Cache revalidation took {Milliseconds}ms", DateTime.UtcNow.Subtract(now).TotalMilliseconds);
        }
    }

    private async Task GetValuesFromSheet(DynamicContext context)
    {
        _entries = new List<Entry>();
        var values = await GetEntriesFromSheets(context, "'2020-2024'!A2:O", "'2015-2019'!A2:O", "'2010-2014'!A2:O", "'Pre-2010s'!A2:O", "'Genreless'!A2:O");
        if (values != null)
            _entries.AddRange(values);
    }

    private static GenreNode? rootNode;

    private async Task GetGenreTreeFromSheet(DynamicContext context)
    {
        var request = api.Spreadsheets.Values.BatchGet(SPREADSHEET_ID);
        request.Ranges = "'Genre Tree'!A2:E";
        var response = await request.ExecuteAsync();

        var valueRanges = response.ValueRanges;
        if (valueRanges is null)
            throw new InvalidDataException("There are no values");
        if (valueRanges.Count < 1)
            throw new InvalidDataException("There are zero values");

        rootNode = graphService.ParseTree(valueRanges);
        File.WriteAllText("tree.json", JsonConvert.SerializeObject(rootNode, Formatting.Indented));
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

    public static readonly string[] DateFormat =
    {
        "yyyy'-'MM'-'dd"
    };

    public static readonly string[] TimeFormat =
    {
        "m':'ss" /*, "h:mm:ss"*/
    };

    private static readonly IRatioScorer _scorer = new TokenSetScorer();

    private static Color GetGenreColor(string genre)
    {
        if (!_genreColors.TryGetValue(genre, out var color))
            color = Color.Default;
        return color;
    }

    private static List<Entry> GetAllTracksByArtistExact(string artist)
    {
        return _entries.Where(e => string.Equals(e.OriginalArtists, artist, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList();
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

        foreach (var entry in _entries)
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

        return tracks.OrderByDescending(e => e.Date).ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="artist"></param>
    /// <param name="includeRemixes">Include XXX (artist Remix)</param>
    /// <param name="includeRemixed">Include artist (XXX Remix)</param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    private static List<Entry> GetAllTracksByArtist(string artist, bool includeRemixes = true, bool includeRemixed = false, int threshold = 80)
    {
        var tracks = new List<Entry>();

        foreach (var entry in _entries)
        {
            // include featured artists
            if (entry.Info.Features.Any(s => string.Equals(s, artist, StringComparison.OrdinalIgnoreCase)))
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
                    if (entry.Info.Remixers.Any(s => string.Equals(s, artist, StringComparison.OrdinalIgnoreCase)))
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
                    if (entry.Info.Artists.Any(s => string.Equals(s, artist, StringComparison.OrdinalIgnoreCase)))
                    {
                        tracks.Add(entry);
                    }
                }
            }

            // track is not a remix
            if (entry.Info.Remixers.Count < 1)
            {
                // track is by searching artist
                if (entry.Info.Artists.Any(s => string.Equals(s, artist, StringComparison.OrdinalIgnoreCase)))
                {
                    tracks.Add(entry);
                }
            }
        }

        return tracks.OrderByDescending(e => e.Date).ToList();
    }

    private static List<Entry> GetTracksByTitleExact(List<Entry> tracksByArtist, string title)
    {
        return tracksByArtist.Where(e => string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Date).ToList();
    }

    private static List<Entry> GetTracksByTitleFuzzy(List<Entry> tracksByArtist, string title, int threshold = 80)
    {
        return tracksByArtist.Where(e => Fuzz.Ratio(e.Title, title, PreprocessMode.Full) >= threshold).OrderByDescending(e => e.Date).ToList();
    }

    private static List<Entry> GetTracksByTitleFuzzy(string title)
    {
        return GetTracksByTitleFuzzy(_entries, title);
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

    public string[] GetAllSubgenres()
    {
        return _entries.SelectMany(e => e.SubgenresList).Distinct().ToArray();
    }

    private static Dictionary<string, int>? mostCommonSubgenres;

    public string[] GetMostCommonSubgenres()
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

    private async Task SendTrackEmbed(DynamicContext context, Entry track)
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

        var builder = new EmbedBuilder().WithColor(GetGenreColor(track.Genre)).WithFields(fields).Build();

        await context.FollowupAsync(embed: builder);
        //await Context.Message.ReplyAsync(null, false, builder);
    }

    private string BoolToEmoji(bool value)
    {
        if (value)
        {
            return "✅";
        }

        return "❌";
    }

    private async Task SendTrackInfoEmbed(DynamicContext context, TrackInfo info)
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

    private async Task SendTrackList(DynamicContext context, string search, string[] artists, List<Entry> tracks, bool includeGenreless = true, int numLatest = 5, int numEarliest = 3, bool includeIndex = true, bool includeArtist = true, bool includeTitle = true, bool includeLabel = true, bool includeDate = true)
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

    private async Task SendArtistInfo(DynamicContext context, string search, string[] artists, List<Entry> tracks)
    {
        var latest = tracks.First();
        var earliest = tracks.Last();
        var now = DateTime.UtcNow;

        var embed = new EmbedBuilder().WithTitle(string.Join(", ", artists)).WithDescription($"`{search}` matches the artists {string.Join(", ", artists)}. The latest track {IsWas(latest.Date, now)} **{latest.Title} ({latest.Date:Y})**, and the first track {IsWas(earliest.Date, now)} **{earliest.Title} ({earliest.Date:Y})**").AddField("Tracks", BuildTrackList(search, artists, tracks, includeArtist: false).ToString()).AddField("Genres", BuildTopGenreList(tracks.ToArray(), 5).ToString(), true);

        await context.FollowupAsync(embed: embed.Build());
        //await Context.Message.ReplyAsync(embed: embed.Build());
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
            //Log.Verbose($"{range.Range} | {range.ETag} | {range.MajorDimension}");
            if (range.Values is null)
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
                    await context.FollowupAsync($"at ({row.Count}) {string.Join(", ", row)}: {ex}");
                    return null;
                }
            }
        }

        return entries.Where(e => e.Genre != "Release").ToList();
    }


    private (Dictionary<string, List<string>>, Dictionary<string, Entry[]>) ByGenre(string[] subgenres)
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

#region Track

    public const string CMD_TRACK_NAME = "track";
    public const string CMD_TRACK_DESC = "Search for a track on the sheet";
    public const string CMD_TRACK_SEARCH_DESC = "Search for a track on the sheet";

    public async Task TrackCommand(string search, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

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
            await context.ErrorAsync($"cannot parse `{search}` into `Artist - Title` or `Title`");
            //await Context.Message.ReplyAsync($"cannot parse `{search}` into `Artist - Title` or `Title`");
            return;
        }
        else
        {
            var artist = split[0];
            var tracksByArtist = GetAllTracksByArtistFuzzy(artist);

            if (tracksByArtist.Count == 0)
            {
                await context.ErrorAsync($"no tracks found by artist `{artist}`");
                //await Context.Message.ReplyAsync($"no tracks found by artist `{artist}`");
                return;
            }

            var title = split[1];
            tracks = GetTracksByTitleFuzzy(tracksByArtist, title);

            if (tracks.Count == 0)
            {
                await context.FollowupAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
                //await Context.Message.ReplyAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
                return;
            }
        }

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"pissed left pant");
            //await Context.Message.ReplyAsync($"pissed left pant");
            return;
        }

        foreach (var track in tracks)
        {
            await SendTrackEmbed(context, track);
        }
    }

#endregion

#region Track Exact

    public async Task TrackExactCommand(string search, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var split = search.Split(new[]
        {
            " - "
        }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length != 2)
        {
            await context.ErrorAsync($"cannot parse `{search}` into `Artist - Title`");
            //await Context.Message.ReplyAsync($"cannot parse `{search}` into `Artist - Title`");
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
        var tracks = GetTracksByTitleExact(tracksByArtist, title);

        if (tracks.Count == 0)
        {
            await context.FollowupAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
            //await Context.Message.ReplyAsync($"i found the artist `{artist}` but i cannot find the track `{title}`");
            return;
        }

        foreach (var track in tracks)
        {
            await SendTrackEmbed(context, track);
        }
    }

#endregion

#region Track Info Exact

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
        var tracks = GetTracksByTitleExact(tracksByArtist, title);

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

#region Track Info Force

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

    public async Task ArtistCommand(string artist, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var artists = Process.ExtractTop(artist, _entries.SelectMany(e => e.ActualArtists).Distinct(), scorer: _scorer, cutoff: 80).OrderByDescending(a => a.Score).ThenBy(a => a.Value).Select(a => a.Value).ToArray();

        var tracksByArtist = GetAllTracksByArtistFuzzy(artist);

        if (tracksByArtist.Count == 0)
        {
            await context.ErrorAsync($"no tracks found by artist `{artist}`");
            //await Context.Message.ReplyAsync($"no tracks found by artist `{artist}`");
            return;
        }

        await SendArtistInfo(context, artist, artists, tracksByArtist);
    }

#endregion

#region Artist Debug

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

#region Subgenre

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

#region Subgenre Exact

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

                if (first.ToString("MM/yyyy") == last.ToString("MM/yyyy"))
                    sb.AppendLine($"{first:MM/yyyy}: {subgenre} #{mostCommon}");
                else if (DateTime.UtcNow.Subtract(last) < TimeSpan.FromDays(90))
                    sb.AppendLine($"{first:MM/yyyy}-now: {subgenre} #{mostCommon}");
                else
                    sb.AppendLine($"{first:MM/yyyy}-{last:MM/yyyy}: {subgenre} #{mostCommon}");
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
            {
                throw new ArgumentException("Invalid date");
            }

            query = query.Where(e => e.Date != null && new DateOnly(e.Date.Year, e.Date.Month, e.Date.Day) < before);
        }

        if (!string.IsNullOrWhiteSpace(arguments.After))
        {
            if (!DateOnly.TryParse(arguments.After, out var after))
            {
                throw new ArgumentException("Invalid date");
            }

            query = query.Where(e => e.Date != null && new DateOnly(e.Date.Year, e.Date.Month, e.Date.Day) > after);
        }

        if (!string.IsNullOrWhiteSpace(arguments.Date))
        {
            if (!DateOnly.TryParse(arguments.Date, out var date))
            {
                throw new ArgumentException("Invalid date");
            }

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

    public async Task CollabGraphCommand(CollabGraphCommandOptions graphOptions, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        await CheckIfCacheExpired(context);

        var tracks = GetAllTracksByArtist(graphOptions.StartArtist);
        if (tracks.Count == 0)
        {
            await context.FollowupAsync("No tracks by artist");
            return;
        }

        var artists = tracks.SelectMany(t => t.ActualArtists).Distinct().ToArray();
        Log.Verbose("artists: {A}", string.Join(", ", artists));
        if (artists.Length < 2)
        {
            await context.FollowupAsync("No collaborations");
            return;
        }

        var imageBytes = graphService.RenderCollabs(artists, graphOptions);
        var image = new MemoryStream(imageBytes);
        await context.FollowupWithFileAsync(image, $"{graphOptions.StartArtist}.png");
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
}

public class GenreNode
{
    public string Name { get; init; }

    public bool IsRoot { get; init; }

    public bool IsMeta { get; set; }

    [JsonIgnore]
    public GenreNode? Parent { get; set; }

    public List<GenreNode> Subgenres { get; set; } = new List<GenreNode>();

    public void AddSubgenre(GenreNode node)
    {
        /*if (node.Parent is not null)
        {
            if (node.Parent == this)
                throw new Exception("Subgenre already added to this parent");
            throw new Exception("Parent already set");
        }*/

        node.Parent = this;
        Subgenres.Add(node);
    }

    public bool ShouldSerializeSubgenres()
    {
        return Subgenres.Count > 0;
    }

    public bool ShouldSerializeIsRoot()
    {
        return IsRoot;
    }

    public bool ShouldSerializeIsMeta()
    {
        return IsMeta;
    }
}