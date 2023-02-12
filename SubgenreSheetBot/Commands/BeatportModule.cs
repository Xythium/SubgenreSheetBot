using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

[Group("Beatport"), Alias("b", "bp")]
public class BeatportModule : ModuleBase
{
    private readonly BeatportService beatport;
    
    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public BeatportModule(BeatportService beatport) { this.beatport = beatport; }

    [Command("tracks"), Alias("t"), Summary("Get all tracks from an album")]
    public async Task Tracks([Remainder, Summary("Album ID to search for")] string albumUrl)
    {
        await beatport.TracksCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("album"), Alias("a", "release"), Summary("Get all tracks from an album")]
    public async Task Album([Remainder, Summary("Album ID to search for")] string albumUrl)
    {
        await beatport.AlbumCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("isrc"), Alias("i"), Summary("Search by ISRC")]
    public async Task Isrc([Remainder, Summary("ISRC to search for")] string isrc)
    {
        await beatport.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("label"), Summary("Get all releases from a label")]
    public async Task Label([Remainder, Summary("Label name to search for")] string labelName)
    {
        await beatport.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("labelcached"), Alias("labelc", "lc"), Summary("Get all releases from a label")]
    public async Task LabelCached([Remainder, Summary("Label name to search for")] string labelName)
    {
        await beatport.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}