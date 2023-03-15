using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MusicTools.Parsing.Track;
using MusicTools.Utils;
using Serilog;

namespace Common.SubgenreSheet;

public class Entry
{
    public string Sheet { get; private set; }

    public DateTime Date { get; private set; }

    public bool Spotify { get; private set; }

    public bool SoundCloud { get; private set; }

    public bool Beatport { get; private set; }

    public string Genre { get; private set; }

    /// <summary>
    /// 'Subgenres' field as its written on the sheet
    /// </summary>
    public string Subgenres { get; private set; }

    /// <summary>
    /// 'Subgenres' field as its written on the sheet, but split up
    /// </summary>
    public string[] SubgenresList { get; private set; }

    /// <summary>
    /// 'Artists' field as its written on the sheet
    /// </summary>
    public string OriginalArtists { get; private set; }

    /// <summary>
    /// 'Artists' field as its written on the sheet, but split up
    /// </summary>
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
    private const int O = N + 1;

    public static bool TryParse(IList<object> row, string sheet, out Entry? entry)
    {
        var date = GetDateArgument(row, A, null);

        if (date is null)
        {
            entry = null;
            return false;
        }

        var spotify = GetBoolArgument(row, B, false);
        var soundcloud = GetBoolArgument(row, C, false);
        var beatport = GetBoolArgument(row, D, false);
        var bandcamp = GetBoolArgument(row, E, false);
        var genre = GetStringArgument(row, F, null);
        var subgenre = GetStringArgument(row, G, null);
        var artists = GetStringArgument(row, H, null);
        var title = GetStringArgument(row, I, null);
        var label = GetStringArgument(row, J, null);
        var length = GetTimeArgument(row, K, null);
        var bpmStr = GetStringArgument(row, L, null);
        var key = GetStringArgument(row, M, null);

        entry = new Entry
        {
            Sheet = sheet,
            Date = date.Value,
            Spotify = spotify,
            SoundCloud = soundcloud,
            Beatport = beatport,
            Genre = genre,
            Subgenres = subgenre,
            SubgenresList = SubgenresUtils.SplitSubgenres(subgenre).ToArray(),
            OriginalArtists = artists,
            ArtistsList = ArtistUtils.SplitArtists(artists).ToArray(),
            Title = title,
            Label = label,
            LabelList = SubgenresUtils.SplitSubgenres(label),
            Length = length,
            Bpm = bpmStr,
            Key = key
        };
        entry.Info = TrackParser.GetTrackInfo(entry.FormattedArtists, entry.Title, "", "", entry.Date);

        return true;
    }

    private Entry()
    {

    }

    private static string? GetStringArgument(IList<object> row, int index, string? def)
    {
        if (index >= row.Count)
            return def;

        var str = (string)row[index];
        if (string.IsNullOrWhiteSpace(str))
            return def;

        return str;
    }

    public static readonly string[] DateFormat =
    {
        "yyyy'-'MM'-'dd"
    };

    public static readonly string[] TimeFormat =
    {
        "m':'ss" /*, "h:mm:ss"*/
    };

    private static DateTime? GetDateArgument(IList<object> row, int index, DateTime? def)
    {
        if (index >= row.Count)
            return def;

        var str = (string)row[index];
        if (string.IsNullOrWhiteSpace(str))
            return def;

        if (!DateTime.TryParseExact(str, DateFormat, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
        {
            Log.Error($"cannot parse {str} as Date");
            return def;
        }

        return date;
    }

    private static TimeSpan? GetTimeArgument(IList<object> row, int index, TimeSpan? def)
    {
        if (index >= row.Count)
            return def;

        var str = (string)row[index];
        if (string.IsNullOrWhiteSpace(str) || str == "--:--")
            return def;

        if (!TimeSpan.TryParseExact(str, TimeFormat, CultureInfo.CurrentCulture, TimeSpanStyles.None, out var time))
        {
            Log.Error($"cannot parse {str} as Time");
            return def;
        }

        return time;
    }

    private static bool GetBoolArgument(IList<object> row, int index, bool def)
    {
        if (index >= row.Count)
            return def;

        var str = (string)row[index];
        if (string.IsNullOrWhiteSpace(str))
            return def;

        if (string.Equals(str, "TRUE", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(str, "FALSE", StringComparison.OrdinalIgnoreCase))
            return false;

        return def;
    }
}