using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Common.Spotify
{
    public static class SpotifyUtils
    {
        public static async Task<List<FullArtist>> Peep(SpotifyClient api, string labelName)
        {
            var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Artist | SearchRequest.Types.Track, $"label:\"{labelName}\"")
            {
                Limit = 50
            });

            var searchedArtists = new HashSet<FullArtist>(new FullArtistComparer());

            await foreach (var artist in api.Paginate(response.Artists, s => s.Artists, new CachingPaginator()))
            {
                searchedArtists.Add(artist);
                if (searchedArtists.Count == 2000)
                    break;
            }

            var trackArtists = new HashSet<SimpleArtist>(new SimpleArtistComparer());
            var searchedTrackCount = 0;

            await foreach (var track in api.Paginate(response.Tracks, s => s.Tracks, new CachingPaginator()))
            {
                var artists = track.Artists.Select(a => a)
                    .ToArray();

                foreach (var artist in artists)
                    trackArtists.Add(artist);

                if (++searchedTrackCount == 2000)
                    break;
            }

            var notFound = searchedArtists.Where(searchedArtist => trackArtists.FirstOrDefault(trackArtist => trackArtist.Id == searchedArtist.Id) == null)
                .ToList();

            var result = new List<FullArtist>();

            // get FullArtist & double check
            foreach (var artist in notFound)
            {
                response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"label:\"{labelName}\" \"{artist.Name}\"")
                {
                    Limit = 50
                });

                if (response.Tracks.Items == null)
                {
                    throw new Exception($"null items {artist.Name}");
                }

                if (!response.Tracks.Items.Any(track => track.Artists.Any(trackArtist => trackArtist.Id == artist.Id)))
                {
                    result.Add(artist);
                }
            }

            return result;
        }
    }
}