using System.Collections.Generic;
using SpotifyAPI.Web;

namespace Common.Spotify;

public class FullAlbumComparer : IEqualityComparer<FullAlbum>
{
    public bool Equals(FullAlbum x, FullAlbum y)
    {
        if (x is null || y is null)
            return false;

        return x.Id == y.Id;
    }

    public int GetHashCode(FullAlbum obj) { return obj.Id.GetHashCode(); }
}