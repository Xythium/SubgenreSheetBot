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

    public BeatportModule(BeatportService beatport)
    {
        this.beatport = beatport;
    }

    [Command(BeatportService.CMD_TRACKS_NAME), Alias("t"), Summary(BeatportService.CMD_TRACKS_DESCRIPTION)]
    public async Task Tracks([Remainder, Summary(BeatportService.CMD_TRACKS_SEARCH_DESCRIPTION)]string albumUrl)
    {
        await beatport.TracksCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatportService.CMD_ALBUM_NAME), Alias("a", "release"), Summary(BeatportService.CMD_ALBUM_DESCRIPTION)]
    public async Task Album([Remainder, Summary(BeatportService.CMD_ALBUM_SEARCH_DESCRIPTION)]string albumUrl)
    {
        await beatport.AlbumCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatportService.CMD_ISRC_NAME), Alias("i"), Summary(BeatportService.CMD_ISRC_DESCRIPTION)]
    public async Task Isrc([Remainder, Summary(BeatportService.CMD_ISRC_SEARCH_DESCRIPTION)]string isrc)
    {
        await beatport.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatportService.CMD_LABEL_NAME), Summary(BeatportService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Remainder, Summary(BeatportService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatport.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(BeatportService.CMD_LABEL_CACHED_NAME), Alias("labelc", "lc"), Summary(BeatportService.CMD_LABEL_CACHED_DESCRIPTION)]
    public async Task LabelCached([Remainder, Summary(BeatportService.CMD_LABEL_CACHED_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatport.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}