using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client;
using Serilog;
using SpotifyAPI.Web;

namespace Common.Spotify;

public static class SpotifyDbUtils
{
    public static async Task<FullAlbum> GetAlbumOrCache(SpotifyClient api, IDocumentSession session, string albumId)
    {
        var t = session.Load<FullAlbum>($"Album/{albumId}");

        if (t is null)
        {
            t = await api.Albums.Get(albumId);

            await GetTracksOrCache(api, session, t.Tracks.Items);
            await GetFeaturesOrCache(api, session, t.Tracks.Items);
        }

        session.Store(t, $"Album/{albumId}");
        session.SaveChanges();

        return t;
    }

    public static async Task<List<FullTrack>> GetTracksOrCache(SpotifyClient api, IDocumentSession session, List<SimpleTrack> tracksItems)
    {
        var test = session.Load<FullTrack>(tracksItems.Select(t => $"Track/{t.Id}"));

        if (tracksItems.Count != test.Length)
            throw new Exception("Not all tracks loaded");

        var tracks = new List<FullTrack>();
        var missing = new List<string>();

        for (var i = 0; i < tracksItems.Count; i++)
        {
            if (test[i] != null)
            {
                tracks.Add(test[i]);
                continue;
            }

            missing.Add(tracksItems[i]
                .Id);
        }

        if (missing.Count > 0)
        {
            var missingTracks = await api.Tracks.GetSeveral(new TracksRequest(missing));

            foreach (var track in missingTracks.Tracks)
            {
                session.Store(track, $"Track/{track.Id}");
                tracks.Add(track);
            }
        }

        if (tracksItems.Count != test.Length)
            throw new Exception("Not all tracks loaded. Something went wrong");

        return tracks.OrderBy(t => t.DiscNumber)
            .ThenBy(t => t.TrackNumber)
            .ToList();
    }

    public static Task<List<TrackAudioFeatures>> GetFeaturesOrCache(SpotifyClient api, IDocumentSession session, List<SimpleTrack> tracksItems)
    {
        return GetFeaturesOrCache(api, session, tracksItems.Select(tr => tr.Id)
            .ToList());
    }

    public static async Task<List<TrackAudioFeatures>> GetFeaturesOrCache(SpotifyClient api, IDocumentSession session, List<string> tracksItems)
    {
        var test = session.Load<TrackAudioFeatures>(tracksItems.Select(t => $"Feature/{t}"));

        if (tracksItems.Count != test.Length)
            throw new Exception("Not all tracks loaded");

        var tracks = new List<TrackAudioFeatures>();
        var missing = new List<string>();

        for (var i = 0; i < tracksItems.Count; i++)
        {
            if (test[i] != null)
            {
                tracks.Add(test[i]);
                continue;
            }

            missing.Add(tracksItems[i]);
        }

        if (missing.Count > 100)
            throw new NotImplementedException("Too many tracks to get features for");

        if (missing.Count > 0)
        {
            var missingTracks = await api.Tracks.GetSeveralAudioFeatures(new TracksAudioFeaturesRequest(missing));

            foreach (var track in missingTracks.AudioFeatures)
            {
                if (track is null)
                    continue;

                session.Store(track, $"Feature/{track.Id}");
                tracks.Add(track);
            }
        }

        if (tracksItems.Count != test.Length)
            throw new Exception("Not all tracks loaded. Something went wrong");

        return tracks;
    }
}