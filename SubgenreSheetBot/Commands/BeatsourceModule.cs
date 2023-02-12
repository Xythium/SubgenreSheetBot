using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

[Group("Beatsource"), Alias("bs")]
public class BeatsourceModule : ModuleBase
{
    private readonly BeatsourceService beatsource;
    
    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public BeatsourceModule(BeatsourceService beatsource) { this.beatsource = beatsource; }

    [Command("tracks"), Alias("t"), Summary("Get all tracks from an album")]
    public async Task Tracks([Remainder, Summary("Album ID to search for")] string text)
    {
        await beatsource.TracksCommand(text, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("album"), Alias("a", "release"), Summary("Get all tracks from an album")]
    public async Task Album([Remainder, Summary("Album ID to search for")] string text)
    {
        await beatsource.AlbumCommand(text, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("isrc"), Alias("i"), Summary("Search by ISRC")]
    public async Task Isrc([Remainder, Summary("ISRC to search for")] string isrc)
    {
        await beatsource.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("labelcached"), Alias("labelc", "lc"), Summary("Get all releases from a label")]
    public async Task LabelCached([Remainder, Summary("Label name to search for")] string labelName)
    {
        await beatsource.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}