using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

[Group("Apple"), Alias("a", "am")]
public  class AppleModule : ModuleBase
{
    private readonly AppleMusicService apple;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public AppleModule(AppleMusicService apple) { this.apple = apple; }
    
    [Command("album"), Summary("Get all ISRCs from an album")]
    public async Task Album([Remainder, Summary("Album ID to search for")] string text)
    {
        await apple.AlbumCommand(text, new DynamicContext(Context), false, defaultOptions);
    }
}