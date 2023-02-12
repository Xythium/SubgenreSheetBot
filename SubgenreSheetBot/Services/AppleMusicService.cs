using System;
using System.Threading.Tasks;
using Common;
using Common.AppleMusic;
using Discord;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Services;

public class AppleMusicService
{
    private readonly AppleMusic api;

    public AppleMusicService()
    {
        if (api != null) throw new Exception();

        api = new AppleMusic();
    }

    public async Task AlbumCommand(string text, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);
        
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            await context.FollowupAsync($"{text} is not a valid URL");
            return;
        }

     
        using var session = SubgenreSheetBot.AppleMusicStore.OpenSession();
        var apiCache = await AppleMusicDbUtils.GetAlbumOrCache(api, session, uri);

        var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(apiCache.Catalog));

        if (file != null)
        {
            await context.FollowupWithFileAsync(file, "tracklist.txt", embed: embed.Build());
            //await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
            file.Close();
        }
        else
            await context.FollowupAsync(embed: embed.Build());
    }
}