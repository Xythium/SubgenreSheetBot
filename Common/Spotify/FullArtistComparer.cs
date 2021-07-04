using System;
using System.Collections.Generic;
using SpotifyAPI.Web;

namespace Common.Spotify
{
    public class FullArtistComparer : IEqualityComparer<FullArtist>
    {
        public bool Equals(FullArtist x, FullArtist y)
        {
            if (x == null || y == null)
                return false;

            return x.Id == y.Id;
        }

        public int GetHashCode(FullArtist obj) { return obj.Id.GetHashCode(); }
    }
}