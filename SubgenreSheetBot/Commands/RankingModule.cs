using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

[Group("Ranking"), Alias("r")]
public class RankingModule : ModuleBase
{
    private readonly RankingService rankingService;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public RankingModule(RankingService rankingService)
    {
        this.rankingService = rankingService;
    }

    [Command("numtracks")]
    public async Task Track()
    {
        await rankingService.TestCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command("addfrom")]
    public async Task AddFrom(string url)
    {
        await rankingService.AddFromCommand(url, new DynamicContext(Context), false, defaultOptions);
    }
    
    [Command("random")]
    public async Task Random()
    {
        await rankingService.RandomCommand(new DynamicContext(Context), false, defaultOptions);
    }
}