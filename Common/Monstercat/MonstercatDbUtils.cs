using System.Linq;
using System.Threading.Tasks;
using Common.AppleMusic.Api;
using Raven.Client;

namespace Common.Monstercat;

public class MonstercatDbUtils
{
    public static async Task<MonstercatFullRelease?> GetAlbumOrCache(MonstercatPlayer api, IDocumentSession session, string catalogId)
    {
        /*var notFound = session.Load<AppleMusicNotFound>($"AppleMusicNotFound/{albumId}");

        if (notFound != null)
            return null;*/

        var fullRelease = session.Load<MonstercatFullRelease>($"MonstercatFullRelease/{catalogId}");

        if (fullRelease is null)
        {
            var release = await api.Album(catalogId);
            fullRelease = new MonstercatFullRelease(release, release.Tracks.First()
                .Release);

            /*if (release is null)
            {
                session.Store(new AppleMusicNotFound(), $"AppleMusicNotFound/{albumId}");
                session.SaveChanges();
                return null;
            }*/
        }

        // outside 'if' to force document changes
        session.Store(fullRelease, $"MonstercatFullRelease/{catalogId}");
        session.SaveChanges();

        return fullRelease;
    }

    public static async Task<MonstercatFullRelease?> GetAlbumOrCache(MonstercatPlayer api, IDocumentSession session, MonstercatReleaseSummary summary)
    {
        /*var notFound = session.Load<AppleMusicNotFound>($"AppleMusicNotFound/{albumId}");

        if (notFound != null)
            return null;*/

        var fullRelease = session.Load<MonstercatFullRelease>($"MonstercatFullRelease/{summary.CatalogId}");

        if (fullRelease is null)
        {
            var release = await api.Album(summary.CatalogId);
            fullRelease = new MonstercatFullRelease(release, summary);

            /*if (release is null)
            {
                session.Store(new AppleMusicNotFound(), $"AppleMusicNotFound/{albumId}");
                session.SaveChanges();
                return null;
            }*/
        }

        // outside 'if' to force document changes
        session.Store(fullRelease, $"MonstercatFullRelease/{summary.CatalogId}");
        session.SaveChanges();

        return fullRelease;
    }
}