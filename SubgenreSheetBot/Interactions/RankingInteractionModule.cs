using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Data.Sqlite;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Interactions;

[Group("rank", "Ranking")]
public class RankingInteractionModule : InteractionModuleBase
{
    private readonly RankingService rankingService;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public RankingInteractionModule(RankingService rankingService)
    {
        this.rankingService = rankingService;
    }

    [SlashCommand("numtracks", "todo")]
    public async Task Track()
    {
        await rankingService.TestCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("addfrom", "todo")]
    public async Task AddFrom([Summary(nameof(url), "todo")]string url)
    {
        await rankingService.AddFromCommand(url, new DynamicContext(Context), false, defaultOptions);
    }
    
    [SlashCommand("random", "todo")]
    public async Task Random()
    {
        await rankingService.RandomCommand(new DynamicContext(Context), false, defaultOptions);
    }
}