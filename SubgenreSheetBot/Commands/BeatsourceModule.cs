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

    public BeatsourceModule(BeatsourceService beatsource)
    {
        this.beatsource = beatsource;
    }

    [Command(BeatsourceService.CMD_TRACKS_NAME), Alias("t"), Summary(BeatsourceService.CMD_TRACKS_DESCRIPTION)]
    public async Task Tracks([Remainder, Summary(BeatsourceService.CMD_TRACKS_SEARCH_DESCRIPTION)]string url)
    {
        await beatsource.TracksCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatsourceService.CMD_ALBUM_NAME), Alias("a", "release"), Summary(BeatsourceService.CMD_ALBUM_DESCRIPTION)]
    public async Task Album([Remainder, Summary(BeatsourceService.CMD_ALBUM_SEARCH_DESCRIPTION)]string url)
    {
        await beatsource.AlbumCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatsourceService.CMD_ISRC_NAME), Alias("i"), Summary(BeatsourceService.CMD_ISRC_DESCRIPTION)]
    public async Task Isrc([Remainder, Summary(BeatsourceService.CMD_ISRC_SEARCH_DESCRIPTION)]string isrc)
    {
        await beatsource.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatsourceService.CMD_LABEL_NAME), Summary(BeatsourceService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Remainder, Summary(BeatsourceService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatsource.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatsourceService.CMD_LABEL_CACHED_NAME), Alias("labelc", "lc"), Summary(BeatsourceService.CMD_LABEL_CACHED_DESCRIPTION)]
    public async Task LabelCached([Remainder, Summary(BeatsourceService.CMD_LABEL_CACHED_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatsource.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}