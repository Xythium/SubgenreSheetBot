// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using Common.Beatport.Api;
using Raven.Imports.Newtonsoft.Json;

/*var client = new BeatportClient();
client.Login(File.ReadAllText("beatport_user"), File.ReadAllText("beatport_pass")).GetAwaiter().GetResult();
client.Refresh().GetAwaiter().GetResult();
var api = new Beatport(client);

var release = api.GetReleaseById(4179283).GetAwaiter().GetResult();

Console.WriteLine(JsonConvert.SerializeObject(release, Formatting.Indented));*/

var releaseList = JsonConvert.DeserializeObject<List<BeatportRelease>>(File.ReadAllText("releases.json"));
var trackList = JsonConvert.DeserializeObject<List<BeatportTrack>>(File.ReadAllText("tracks.json"));
var releases = releaseList.ToDictionary(t => t.Id);
var tracks = trackList.ToDictionary(t => t.Id);

var client = new TestBeatportClient(tracks, releases);
var api = new Beatport(client);

var release = api.GetReleaseById(4179283).GetAwaiter().GetResult();

Console.WriteLine(JsonConvert.SerializeObject(release, Formatting.Indented));