using System;
using System.Collections.Generic;
using SpotifyAPI.Web;

namespace Common.Spotify
{
    public class SimpleArtistComparer : IEqualityComparer<SimpleArtist>
    {
        public bool Equals(SimpleArtist x, SimpleArtist y)
        {
            if (x == null || y == null)
                return false;

            return x.Name == y.Name;
        }

        public int GetHashCode(SimpleArtist obj)
        {
            return obj.Name.ToLower()
                .GetHashCode();
        }
    }
}