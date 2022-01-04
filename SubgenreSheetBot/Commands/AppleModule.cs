using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.AppleMusic;
using Common.Beatport;
using Discord;
using Discord.Commands;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace SubgenreSheetBot.Commands
{
    [Group("Apple"), Alias("a", "am")]
    public partial class AppleModule : ModuleBase
    {
        [Command("album"), Summary("Get all ISRCs from an album")]
        public async Task Album([Remainder, Summary("Album ID to search for")] string text)
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                await ReplyAsync($"{text} is not a valid URL");
                return;
            }

            var client = new RestClient();
            var request = new RestRequest(uri, Method.GET);
            var response = client.Execute(request);

            var document = new HtmlDocument();
            document.LoadHtml(response.Content);

            var elem = document.GetElementbyId("shoebox-media-api-cache-amp-music");

            if (elem == null)
            {
                await ReplyAsync("Could not find shoebox-media-api-cache-amp-music");
                return;
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
                    await ReplyAsync($"{property.Name}");
                    return;
                }
            }

            var (embed, file) =  AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(apiCache.Catalog));

            if (file != null)
            {
                await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
                file.Close();
            }
            else
                await ReplyAsync(embed: embed.Build());
        }
    }
}