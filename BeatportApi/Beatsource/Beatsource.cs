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
        private RestClient client;

        public Beatsource(string bearerToken)
        {
            _bearerToken = bearerToken;
            client = new RestClient();
            client.AddDefaultHeader("origin", "www.beatsource.com");
            client.AddDefaultHeader("authorization", $"Bearer {_bearerToken}");
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
                result = JsonConvert.DeserializeObject<BeatsourceResponse<BeatsourceRelease>>(response.Content);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.{labelId}.txt", response.Content);
                throw new Exception($"oofie owwie: {ex.Message}");
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
                result = JsonConvert.DeserializeObject<BeatsourceResponse<BeatsourceTrack>>(response.Content);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.{releaseId}.txt", response.Content);
                throw new Exception($"oofie owwie: {ex.Message}");
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
                result = JsonConvert.DeserializeObject<BeatsourceRelease>(response.Content);
            }
            catch (Exception ex)
            {
                File.WriteAllText($"error.{releaseId}.txt", response.Content);
                throw new Exception($"oofie owwie: {ex.Message}");
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

            return JsonConvert.DeserializeObject<BeatsourceTrack>(response.Content);
        }

        public async Task<BeatsourceTrack> GetTrackByTrackId(string trackId)
        {
            var request = new RestRequest($"https://api.beatsource.com/v4/catalog/tracks/{trackId}/", Method.GET);

            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatsourceTrack>(response.Content);
        }
    }
}