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
                await Context.Message.ReplyAsync($"{text} is not a valid URL");
                return;
            }

            var api = new AppleMusic();
            using var session = SubgenreSheetBot.AppleMusicStore.OpenSession();
            var apiCache = await AppleMusicDbUtils.GetAlbumOrCache(api, session, uri);

            var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(apiCache.Catalog));

            if (file != null)
            {
                await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
                file.Close();
            }
            else
                await Context.Message.ReplyAsync(embed: embed.Build());
        }
    }
}