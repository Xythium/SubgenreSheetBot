using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Interactions;

[Group("apple", "Apple Music")]
public class AppleMusicInteractionModule : InteractionModuleBase
{
    private readonly AppleMusicService apple;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public AppleMusicInteractionModule(AppleMusicService apple)
    {
        this.apple = apple;
    }

    [SlashCommand(AppleMusicService.CMD_ALBUM_NAME, AppleMusicService.CMD_ALBUM_DESCRIPTION)]
    public async Task Album([Summary(nameof(url), AppleMusicService.CMD_ALBUM_SEARCH_DESCRIPTION)]string url)
    {
        await apple.AlbumCommand(url, new DynamicContext(Context), false, defaultOptions);
    }
}