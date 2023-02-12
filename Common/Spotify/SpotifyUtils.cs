using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using SpotifyAPI.Web;

namespace Common.Spotify;

public static class SpotifyUtils
{
#if !NET48
        public static async Task<List<FullArtist>> Peep(SpotifyClient api, string labelName)
        {
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Artist | SearchRequest.Types.Track, $"label:\"{labelName}\"")
            {
                Limit = 50,
                Market = "US"
            });

            var searchedArtists = new HashSet<FullArtist>(new FullArtistComparer());

            try
            {
                await foreach (var artist in api.Paginate(response.Artists, s => s.Artists, new CachingPaginator()))
                {
                    searchedArtists.Add(artist);

                    if (searchedArtists.Count == 2000)
                    {
                        Log.Error("Too many artists found for label {labelName}", labelName);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while searching for artists for label {labelName}", labelName);
            }

            var trackArtists = new HashSet<SimpleArtist>(new SimpleArtistComparer());
            var searchedTrackCount = 0;

            try
            {
                await foreach (var track in api.Paginate(response.Tracks, s => s.Tracks, new CachingPaginator()))
                {
                    var artists = track.Artists.Select(a => a)
                        .ToArray();

                    foreach (var artist in artists)
                        trackArtists.Add(artist);

                    if (++searchedTrackCount == 2000)
                    {
                        Log.Error("Too many tracks found for label {labelName}", labelName);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while searching for tracks for label {labelName}", labelName);
            }

            var notFound = searchedArtists.Where(searchedArtist => trackArtists.FirstOrDefault(trackArtist => trackArtist.Id == searchedArtist.Id) is null)
                .ToList();

            var result = new List<FullArtist>();

            try
            {
                // get FullArtist & double check
                foreach (var artist in notFound)
                {
                    response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"label:\"{labelName}\" \"{artist.Name}\"")
                    {
                        Limit = 50,
                        Market = "US"
                    });

                    if (response.Tracks.Items is null)
                    {
                        throw new Exception($"null items {artist.Name}");
                    }

                    if (!response.Tracks.Items.Any(track => track.Artists.Any(trackArtist => trackArtist.Id == artist.Id)))
                    {
                        result.Add(artist);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while double checking for label {labelName}", labelName);
            }

            return result;
        }


        public static (string trackId, string albumId) GetIdFromUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                var locals = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (locals.Length == 2)
                {
                    if (locals[0] == "album")
                        return (null, locals[1]);

                    if (locals[0] == "track")
                        return (locals[1], null);

                    throw new InvalidDataException($"Unsupported link type: {locals[0]}");
                }

                throw new InvalidDataException($"Invalid link: {string.Join(" / ", locals)}");
            }

            throw new InvalidDataException($"Not a url: {url}");
        }
#endif
    public static string BpmToString(TrackAudioFeatures features)
    {
        var bpm = features.Tempo;
        var floating = bpm - (int)bpm;

        if (floating < 0.3 || floating > 0.7)
        {
            features.Tempo = bpm = (float)Math.Round(bpm);
        }
        else
        {
            features.Tempo = bpm = (float)Math.Round(bpm, 1);
        }

        if (Math.Abs(floating - 0.5) < 0.03 && bpm < 95)
        {
            bpm *= 2;
        }

        var str = bpm.ToString(CultureInfo.GetCultureInfo("en-US"));

        if (bpm != features.Tempo)
        {
            str = $"{features.Tempo} (Mark says {bpm})";
        }

        return str;
    }

    public static string IntToKey(int key)
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

    public static string IntToMode(int mode)
    {
        return mode switch
        {
            0 => "min",
            1 => "maj",
            _ => "?"
        };
    }
}