using System;
using System.Threading.Tasks;
using Common;
using Common.AppleMusic;
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

    public AppleMusicInteractionModule(AppleMusicService apple) { this.apple = apple; }
    
    [SlashCommand("album","Get all ISRCs from an album")]
    public async Task Album([Summary(nameof(text),"Album ID to search for")] string text)
    {
        await apple.AlbumCommand(text, new DynamicContext(Context), false, defaultOptions);
    }
}