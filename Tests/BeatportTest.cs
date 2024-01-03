using Common.Beatport.Api;
using Raven.Imports.Newtonsoft.Json;

namespace Tests;

public class BeatportTest
{
    [Fact]
    public async Task TestGetTrackByTrackIdReturnsTrack()
    {
        var releaseList = JsonConvert.DeserializeObject<List<BeatportRelease>>(File.ReadAllText("releases.json"));
        var trackList = JsonConvert.DeserializeObject<List<BeatportTrack>>(File.ReadAllText("tracks.json"));
        var releases = releaseList.ToDictionary(t => t.Id);
        var tracks = trackList.ToDictionary(t => t.Id);
        var client = new TestBeatportClient(tracks, releases);
        var beatport = new Beatport(client);

        var testTrackId = 17208316;

        var track = await beatport.GetTrackByTrackId(testTrackId);

        Assert.NotNull(track);
        Assert.Equal(testTrackId, track.Id);

    }
}