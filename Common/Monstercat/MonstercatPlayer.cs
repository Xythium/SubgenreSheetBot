using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace Common.Monstercat;

public class MonstercatPlayer
{
    private readonly RestClient client;

    private static readonly JsonSerializerSettings serializerSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Error
    };

    public MonstercatPlayer() { client = new RestClient(); }

    public async Task<MonstercatBrowseResponse> Browse(int limit, int offset)
    {
        var request = new RestRequest($"https://player.monstercat.app/api/catalog/browse?limit={limit}&offset={offset}&sort=-date&nogold=false&onlyReleased=false&creatorfriendly=false", Method.Get);
        var response = await client.ExecuteAsync(request);

        return JsonConvert.DeserializeObject<MonstercatBrowseResponse>(response.Content, serializerSettings);
    }
    
    public async Task<MonstercatCatalogResponse> Album(string catalodId)
    {
        var request = new RestRequest($"https://player.monstercat.app/api/catalog/release/{catalodId}?idType=catalogId", Method.Get);
        var response = await client.ExecuteAsync(request);

        return JsonConvert.DeserializeObject<MonstercatCatalogResponse>(response.Content, serializerSettings);
    }
}