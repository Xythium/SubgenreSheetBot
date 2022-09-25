using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.AppleMusic.Api;
using Raven.Client;

namespace Common.AppleMusic;

public class AppleMusicDbUtils
{
    public static async Task<ApiCache?> GetAlbumOrCache(AppleMusic api, IDocumentSession session, int albumId)
    {
        var notFound = session.Load<AppleMusicNotFound>($"AppleMusicNotFound/{albumId}");

        if (notFound != null)
            return null;

        var t = session.Load<ApiCache>($"AppleMusicReleases/{albumId}");

        if (t is null)
        {
            t = await api.GetFromUrl(new Uri($"https://music.apple.com/us/album/{albumId}"));

            if (t is null)
            {
                session.Store(new AppleMusicNotFound(), $"AppleMusicNotFound/{albumId}");
                session.SaveChanges();
                return null;
            }
        }

        // outside 'if' to force document changes
        session.Store(t, $"AppleMusicReleases/{albumId}");
        session.SaveChanges();

        return t;
    }

    public static Task<ApiCache?> GetAlbumOrCache(AppleMusic api, IDocumentSession session, Uri url)
    {
        var idStr = url.Segments.Last();
        if (idStr.StartsWith("id"))
            idStr = idStr.Substring(2);
        if (!int.TryParse(idStr, out var id))
            throw new ArgumentException($"Invalid album id {url}", nameof(url));

        return GetAlbumOrCache(api, session, id);
    }
}