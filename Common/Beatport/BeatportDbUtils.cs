using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BeatportApi;
using BeatportApi.Beatport;
using BeatportApi.Beatsource;
using Raven.Client;
using BAPI = BeatportApi.Beatport.Beatport;

namespace Common.Beatport
{
    public static class BeatportDbUtils
    {
        public static async Task<BeatportRelease?> GetAlbumOrCache(BAPI api, IDocumentSession session, int albumId)
        {
            var notFound = session.Load<BeatportNotFound>($"BeatportNotFound/{albumId}");

            if (notFound != null)
                return null;

            var t = session.Load<BeatportRelease>($"BeatportReleases/{albumId}");

            if (t == null)
            {
                t = await api.GetReleaseById(albumId);

                if (t == null)
                {
                    session.Store(new BeatportNotFound(), $"BeatportNotFound/{albumId}");
                    session.SaveChanges();
                    return null;
                }

                await GetTracksOrCache(api, session, t.TrackUrls);
            }

            // outside 'if' to force document changes
            session.Store(t);
            session.SaveChanges();

            return t;
        }

        public static async Task<BeatsourceRelease> GetAlbumOrCache(Beatsource api, IDocumentSession session, int albumId)
        {
            var t = session.Load<BeatsourceRelease>($"BeatsourceReleases/{albumId}");

            if (t == null)
            {
                t = await api.GetReleaseById(albumId);

                if (t == null)
                {
                    // todo: dont know if this can happen
                    return null;
                }

                await GetTracksOrCache(api, session, t.TrackUrls);
            }

            // outside 'if' to force document changes
            session.Store(t);
            session.SaveChanges();

            return t;
        }

        public static Task<BeatportTrack[]> GetTracksOrCache(this BeatportRelease album, BAPI api, IDocumentSession session) { return GetTracksOrCache(api, session, album.TrackUrls); }

        public static Task<BeatsourceTrack[]> GetTracksOrCache(this BeatsourceRelease album, Beatsource api, IDocumentSession session) { return GetTracksOrCache(api, session, album.TrackUrls); }

        public static async Task<BeatportTrack[]> GetTracksOrCache(BAPI api, IDocumentSession session, string[] trackUrls)
        {
            var tracks = new List<BeatportTrack>();

            if (trackUrls == null || trackUrls.Length == 0)
                return tracks.ToArray();

            foreach (var url in trackUrls)
            {
                var idResult = BeatportUtils.GetIdFromUrl(url);
                if (!string.IsNullOrWhiteSpace(idResult.Error))
                    continue;

                var t = session.Load<BeatportTrack>($"BeatportTracks/{idResult.Id}");

                if (t == null)
                {
                    t = await api.GetTrackByTrackId(idResult.Id);

                    if (t == null)
                    {
                        // todo: silent ignore for now
                        /* return null;
                         throw new Exception("oh nwwoo :( why");*/
                    }

                    session.Store(t);
                }

                tracks.Add(t);
            }

            session.SaveChanges();

            return tracks.OrderBy(t => t.Number)
                .ThenBy(t => t.Isrc)
                .ToArray();
        }

        public static async Task<BeatsourceTrack[]> GetTracksOrCache(Beatsource api, IDocumentSession session, string[] trackUrls)
        {
            var tracks = new List<BeatsourceTrack>();

            foreach (var url in trackUrls)
            {
                var idResult = BeatportUtils.GetIdFromUrl(url);
                if (!string.IsNullOrWhiteSpace(idResult.Error))
                    continue;

                var t = session.Load<BeatsourceTrack>($"BeatsourceTracks/{idResult.Id}");

                if (t == null)
                {
                    t = await api.GetTrackByTrackId(idResult.Id);

                    if (t == null)
                    {
                        throw new Exception("oh nwwoo :( why");
                        // todo: silent ignore for now
                        /* return null;
                         */
                    }

                    session.Store(t);
                }

                tracks.Add(t);
            }

            session.SaveChanges();

            return tracks.OrderBy(t => t.Number)
                .ThenBy(t => t.Isrc)
                .ToArray();
        }
    }
}