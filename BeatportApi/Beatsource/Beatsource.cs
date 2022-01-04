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
            var request = new RestRequest("https://www.beatsource.com/api/auth/login", Method.POST);
            request.AddJsonBody(new
            {
                data = new
                {
                    username,
                    password
                }
            });
            var response = await client.ExecuteAsync(request);

            if (response.Content == "{\"message\": \"Internal server error\"}")
            {
                throw new InvalidDataException("Internal server error in GetReleasesByLabelId");
            }

            BeatsourceLogin result = null;

            try
            {
                result = JsonConvert.DeserializeObject<BeatsourceLogin>(response.Content, serializerSettings);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.login.txt", response.Content);
                throw new Exception($"oofie owwie Login: {ex.Message}");
            }

            if (result.TokenType != "Bearer")
                throw new InvalidDataException("unknown token type");

            _bearerToken = result.AccessToken;
            client.AddDefaultHeader("authorization", $"Bearer {_bearerToken}");

            return result;
        }

        public Task<BeatsourceResponse<BeatsourceRelease>> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1) { return GetReleasesByLabelId(labelId.ToString(), itemsPerPage, page); }

        public async Task<BeatsourceResponse<BeatsourceRelease>> GetReleasesByLabelId(string labelId, int itemsPerPage = 200, int page = 1)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/releases/?label_id={labelId}&per_page={itemsPerPage}&page={page}", Method.GET);

            var response = await client.ExecuteAsync(request);

            if (response.Content == "{\"message\": \"Internal server error\"}")
            {
                throw new InvalidDataException("Internal server error in GetReleasesByLabelId");
            }

            BeatsourceResponse<BeatsourceRelease> result = null;

            try
            {
                result = JsonConvert.DeserializeObject<BeatsourceResponse<BeatsourceRelease>>(response.Content, serializerSettings);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.{labelId}.txt", response.Content);
                throw new Exception($"oofie owwie GetReleasesByLabelId: {ex.Message}");
            }

            return result;
        }

        public async Task<BeatsourceResponse<BeatsourceTrack>> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/releases/{releaseId}/tracks/?per_page={itemsPerPage}&page={page}/", Method.GET);

            var response = await client.ExecuteAsync(request);

            if (response.Content == "{\"message\": \"Internal server error\"}")
            {
                throw new InvalidDataException("Internal server error in GetTracksByReleaseId");
            }

            BeatsourceResponse<BeatsourceTrack> result = null;

            try
            {
                result = JsonConvert.DeserializeObject<BeatsourceResponse<BeatsourceTrack>>(response.Content, serializerSettings);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.{releaseId}.txt", response.Content);
                throw new Exception($"oofie owwie GetTracksByReleaseId: {ex.Message}");
            }

            return result;
        }

        public async Task<BeatsourceRelease> GetReleaseById(int releaseId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/releases/{releaseId}/", Method.GET);

            var response = await client.ExecuteAsync(request);

            if (response.Content == "{\"message\": \"Internal server error\"}")
            {
                throw new InvalidDataException("Internal server error in GetReleaseById");
            }

            BeatsourceRelease result;

            try
            {
                result = JsonConvert.DeserializeObject<BeatsourceRelease>(response.Content, serializerSettings);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.{releaseId}.txt", response.Content);
                throw new Exception($"oofie owwie GetReleaseById: {ex.Message}");
            }

            return result;
        }

        public async Task<BeatsourceTrack> GetTrackByTrackId(int trackId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/tracks/{trackId}/", Method.GET);

            var response = await client.ExecuteAsync(request);

            if (response.Content == "{\"message\": \"Internal server error\"}")
            {
                throw new InvalidDataException("Internal server error in GetTrackByTrackId");
            }

            return JsonConvert.DeserializeObject<BeatsourceTrack>(response.Content, serializerSettings);
        }

        public async Task<BeatsourceTrack> GetTrackByTrackId(string trackId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/tracks/{trackId}/", Method.GET);

            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatsourceTrack>(response.Content, serializerSettings);
        }
    }
}