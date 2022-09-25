using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace BeatportApi.Beatsource
{
    public class Beatsource
    {
        private string _bearerToken;
        private readonly RestClient client;

        private static JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error
        };

        public Beatsource()
        {
            client = new RestClient();
            client.AddDefaultHeader("origin", "www.beatsource.com");
            //client.AddDefaultHeader("authorization", $"Bearer {_bearerToken}");
        }

        public async Task<BeatsourceLogin> Login(string username, string password)
        {
            var request = new RestRequest("https://www.beatsource.com/api/auth/login", Method.Post);
            request.AddJsonBody(new
            {
                data = new
                {
                    username,
                    password
                }
            });
            var response = await client.ExecuteAsync(request);

            var result = Deserialize<BeatsourceLogin>(response.Content, $"login{username}");
            if (result is null)
                throw new Exception("Login failed");

            if (result.TokenType != "Bearer")
                throw new InvalidDataException("unknown token type");

            bearerToken = result.AccessToken;
            client.AddDefaultHeader("authorization", $"Bearer {bearerToken}");

            return result;
        }

        public Task<BeatsourceResponse<BeatsourceRelease>> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1) { return GetReleasesByLabelId(labelId.ToString(), itemsPerPage, page); }

        public async Task<BeatsourceResponse<BeatsourceRelease>> GetReleasesByLabelId(string labelId, int itemsPerPage = 200, int page = 1)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/releases/?label_id={labelId}&per_page={itemsPerPage}&page={page}", Method.Get);
            var response = await client.ExecuteAsync(request);

            return Deserialize<BeatsourceResponse<BeatsourceRelease>>(response.Content, $"label{labelId}");
        }

        public async Task<BeatsourceResponse<BeatsourceTrack>> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/releases/{releaseId}/tracks/?per_page={itemsPerPage}&page={page}/", Method.Get);
            var response = await client.ExecuteAsync(request);

            return Deserialize<BeatsourceResponse<BeatsourceTrack>>(response.Content, $"release{releaseId}");
        }

        public async Task<BeatsourceRelease> GetReleaseById(int releaseId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/releases/{releaseId}/", Method.Get);
            var response = await client.ExecuteAsync(request);

            return Deserialize<BeatsourceRelease>(response.Content, $"release{releaseId}");
        }

        public async Task<BeatsourceTrack> GetTrackByTrackId(int trackId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/tracks/{trackId}/", Method.Get);
            var response = await client.ExecuteAsync(request);

            return Deserialize<BeatsourceTrack>(response.Content, $"track{trackId}");
        }

        public async Task<BeatsourceTrack> GetTrackByTrackId(string trackId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/tracks/{trackId}/", Method.Get);
            var response = await client.ExecuteAsync(request);

            return Deserialize<BeatsourceTrack>(response.Content, $"track{trackId}");
        }

        private static T? Deserialize<T>(string? json, string identifier, [CallerMemberName] string memberName = "")
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                var res = JsonConvert.DeserializeObject<T>(json, serializerSettings);
                if (res is null)
                    throw new Exception($"Deserialization failed in {memberName}");

                return res;
            }
            catch (Exception ex)
            {
                var error = JsonConvert.DeserializeObject<BeatsourceError>(json);
                if (error is null)
                    throw new Exception($"Deserialization failed in {memberName}");

                var detail = error.Error;
                if (string.IsNullOrWhiteSpace(detail))
                    detail = error.Detail;

                switch (detail)
                {
                    case "Internal server error":
                        File.WriteAllText($"errorbeatsource-internal.{identifier}.txt", json);
                        throw new InvalidDataException($"Internal Beatport in {memberName}: {ex}");

                    case "Not found.":
                        File.WriteAllText($"errorbeatsource-notfound.{identifier}.txt", json);
                        return default;

                    case "Territory Restricted.":
                        File.WriteAllText($"errorbeatsource-territory.{identifier}.txt", json);
                        throw new InvalidDataException($"Territory restricted in {memberName}: {ex}");

                    case "Authentication credentials were not provided.":
                        File.WriteAllText($"errorbeatsource-creds.{identifier}.txt", json);
                        throw new InvalidDataException($"Authentication in {memberName}: {ex}");

                    case "Server Error (500)":
                        File.WriteAllText($"errorbeatsource-server.{identifier}.txt", json);
                        throw new InvalidDataException($"Server Error in {memberName}: {ex}");

                    default:
                        File.WriteAllText($"errorbeatsource.{identifier}.txt", json);
                        throw new InvalidDataException($"Unknown error in {memberName}: {ex}");
                }
            }

            return default;
        }
    }
}