using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace BeatportApi.Beatport
{
    public class Beatport
    {
        private string _bearerToken;

        private static JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error
        };

        public Beatport(string bearerToken) { _bearerToken = bearerToken; }

        public Task<BeatportResponse<BeatportRelease>> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1) { return GetReleasesByLabelId(labelId.ToString(), itemsPerPage, page); }

        public async Task<BeatportResponse<BeatportRelease>?> GetReleasesByLabelId(string labelId, int itemsPerPage = 200, int page = 1)
        {
            var client = new RestClient();

            var request = new RestRequest($"https://www.beatport.com/api/v4/catalog/releases?label_id={labelId}&per_page={itemsPerPage}&page={page}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            var result = Deserialize<BeatportResponse<BeatportRelease>>(response.Content, $"label{labelId}");

            return result;
        }

        private static T? Deserialize<T>(string json, string identifier, [CallerMemberName] string memberName = "")
        {
            try
            {
                var res = JsonConvert.DeserializeObject<T>(json, serializerSettings);
                if (res == null)
                    throw new Exception($"Deserialization failed in {memberName}");

                return res;
            }
            catch (Exception ex)
            {
                var error = JsonConvert.DeserializeObject<BeatportError>(json);

                switch (error.Detail)
                {
                    case "Internal server error":
                        File.WriteAllText($"error-internal.{identifier}.txt", json);
                        throw new InvalidDataException("Internal Beatport in " + memberName);

                    case "Not found.":
                        File.WriteAllText($"error-notfound.{identifier}.txt", json);
                        return default;

                    case "Territory Restricted.":
                        File.WriteAllText($"error-territory.{identifier}.txt", json);
                        throw new InvalidDataException("Territory restricted in " + memberName);

                    default:
                        File.WriteAllText($"error.{identifier}.txt", json);
                        throw new InvalidDataException("Unknown error in " + memberName);
                }
            }

            return default;
        }

        public async Task<BeatportResponse<BeatportTrack>?> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
        {
            var client = new RestClient();

            var request = new RestRequest($"https://www.beatport.com/api/v4/catalog/releases/{releaseId}/tracks/?per_page={itemsPerPage}&page={page}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            var result = Deserialize<BeatportResponse<BeatportTrack>>(response.Content, $"release{releaseId}");

            return result;
        }

        public async Task<BeatportRelease?> GetReleaseById(int releaseId)
        {
            var client = new RestClient();

            var request = new RestRequest($"https://www.beatport.com/api/v4/catalog/releases/{releaseId}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            var result = Deserialize<BeatportRelease>(response.Content, $"release{releaseId}");

            return result;
        }

        public async Task<BeatportTrack?> GetTrackByTrackId(int trackId)
        {
            var client = new RestClient();
            var request = new RestRequest($"https://www.beatport.com/api/v4/catalog/tracks/{trackId}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            var result = Deserialize<BeatportTrack>(response.Content, $"track{trackId}");

            return result;
        }

        public async Task<BeatportTrack> GetTrackByTrackId(string trackId)
        {
            var client = new RestClient();
            var request = new RestRequest($"https://www.beatport.com/api/v4/catalog/tracks/{trackId}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatportTrack>(response.Content, serializerSettings);
        }
    }
}