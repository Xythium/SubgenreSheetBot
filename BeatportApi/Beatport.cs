using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.UI.WebControls.WebParts;
using Newtonsoft.Json;
using RestSharp;

namespace BeatportApi
{
    public class Beatport
    {
        private string _bearerToken;

        public Beatport(string bearerToken) { _bearerToken = bearerToken; }

        public Task<BeatportResponse<BeatportRelease>> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1) { return GetReleasesByLabelId(labelId.ToString(), itemsPerPage, page); }

        public async Task<BeatportResponse<BeatportRelease>> GetReleasesByLabelId(string labelId, int itemsPerPage = 200, int page = 1)
        {
            var client = new RestClient();

            var request = new RestRequest($"https://cors.bridged.cc/https://www.beatport.com/api/v4/catalog/releases?label_id={labelId}&per_page={itemsPerPage}&page={page}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            if (response.Content == "{\"message\": \"Internal server error\"}")
            {
                throw new InvalidDataException("Internal server error");
            }

            BeatportResponse<BeatportRelease> result = null;

            try
            {
                result = JsonConvert.DeserializeObject<BeatportResponse<BeatportRelease>>(response.Content);
            }
            catch (Exception ex)
            {
                ;
                throw new Exception($"oofie owwie: {ex.Message}");
            }

            return result;
        }

        public async Task<BeatportResponse<BeatportTrack>> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
        {
            var client = new RestClient();

            var request = new RestRequest($"https://cors.bridged.cc/https://www.beatport.com/api/v4/catalog/releases/{releaseId}/tracks/?per_page={itemsPerPage}&page={page}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatportResponse<BeatportTrack>>(response.Content);
        }

        public async Task<BeatportRelease> GetReleaseById(int releaseId)
        {
            var client = new RestClient();

            var request = new RestRequest($"https://cors.bridged.cc/https://www.beatport.com/api/v4/catalog/releases/{releaseId}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatportRelease>(response.Content);
        }

        public async Task<BeatportTrack> GetTrackByTrackId(int trackId)
        {
            var client = new RestClient();
            var request = new RestRequest($"https://cors.bridged.cc/https://www.beatport.com/api/v4/catalog/tracks/{trackId}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatportTrack>(response.Content);
        }

        public async Task<BeatportTrack> GetTrackByTrackId(string trackId)
        {
            var client = new RestClient();
            var request = new RestRequest($"https://cors.bridged.cc/https://www.beatport.com/api/v4/catalog/tracks/{trackId}", Method.GET);
            request.AddHeader("origin", "www.beatport.com");
            var response = await client.ExecuteAsync(request);

            return JsonConvert.DeserializeObject<BeatportTrack>(response.Content);
        }
    }
}