using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

[Group("Apple"), Alias("a", "am")]
public class AppleModule : ModuleBase
{
    private readonly AppleMusicService apple;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public AppleModule(AppleMusicService apple)
    {
        this.apple = apple;
    }

    [Command(AppleMusicService.CMD_ALBUM_NAME), Summary(AppleMusicService.CMD_ALBUM_DESCRIPTION)]
    public async Task Album([Remainder, Summary(AppleMusicService.CMD_ALBUM_SEARCH_DESCRIPTION)]string text)
    {
        await apple.AlbumCommand(text, new DynamicContext(Context), false, defaultOptions);
    }
}