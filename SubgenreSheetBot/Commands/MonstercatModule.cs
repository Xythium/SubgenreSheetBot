using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.AppleMusic;
using Common.Monstercat;
using Discord;
using Discord.Commands;

namespace SubgenreSheetBot.Commands;

[Group("Monstercat"), Alias("mc", "mcat")]
public class MonstercatModule : ModuleBase
{
    [Command("latest")]
    public async Task Latest()
    {
        var api = new MonstercatPlayer();
        using var session = SubgenreSheetBot.MonstercatStore.OpenSession();
        var tracks = await api.Browse(100, 0);
        var albums = tracks.Data.GroupBy(t => t.Release, new MonstercatComparer())
            .OrderByDescending(g => g.Key.ReleaseDate)
            .First();

        var album = await MonstercatDbUtils.GetAlbumOrCache(api, session, albums.Key);

        var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(album));

        if (file != null)
        {
            await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
            file.Close();
        }
        else
            await Context.Message.ReplyAsync(embed: embed.Build());
    }

    [Command("album")]
    public async Task Album([Remainder, Summary("Album ID to search for")] string text)
    {
        var api = new MonstercatPlayer();
        using var session = SubgenreSheetBot.MonstercatStore.OpenSession();

        var album = await MonstercatDbUtils.GetAlbumOrCache(api, session, text);

        var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(album));

        if (file != null)
        {
            await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
            file.Close();
        }
        else
            await Context.Message.ReplyAsync(embed: embed.Build());
    }
}