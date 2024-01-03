using System;
using System.Threading.Tasks;
using Common.AppleMusic.Api;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Common.AppleMusic;

public class AppleMusic
{
    public async Task<ApiCache> GetFromUrl(Uri uri)
    {
        var client = new RestClient();
        var request = new RestRequest(uri, Method.Get);
        var response = await client.ExecuteAsync(request);

        var document = new HtmlDocument();
        document.LoadHtml(response.Content);

        var elem = document.GetElementbyId("shoebox-media-api-cache-amp-music");

        if (elem is null)
        {
            throw new Exception("Could not find shoebox-media-api-cache-amp-music");
        }

        var apiCache = new ApiCache();
        var obj = JObject.Parse(elem.InnerHtml);

        foreach (var property in obj.Properties())
        {
            if (property.Name.Contains("storefronts"))
            {
                apiCache.StoreFronts = JsonConvert.DeserializeObject<StoreFronts>(property.Value.ToString());
            }
            else if (property.Name.Contains("catalog"))
            {
                apiCache.Catalog = JsonConvert.DeserializeObject<Catalog>(property.Value.ToString());
            }
            else
            {
                throw new Exception($"Unknown property name '{property.Name}'");
            }
        }

        return apiCache;
    }
}