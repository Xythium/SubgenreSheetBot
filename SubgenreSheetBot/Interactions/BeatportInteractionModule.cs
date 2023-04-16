using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Interactions;

[Group("beatport", "Beatport")]
public class BeatportInteractionModule : InteractionModuleBase
{
    private readonly BeatportService beatport;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public BeatportInteractionModule(BeatportService beatport)
    {
        this.beatport = beatport;
    }

    [SlashCommand(BeatportService.CMD_TRACKS_NAME, BeatportService.CMD_TRACKS_DESCRIPTION)]
    public async Task Tracks([Summary(nameof(albumUrl), BeatportService.CMD_TRACKS_SEARCH_DESCRIPTION)]string albumUrl)
    {
        await beatport.TracksCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatportService.CMD_ALBUM_NAME, BeatportService.CMD_ALBUM_DESCRIPTION)]
    public async Task Album([Summary(nameof(albumUrl), BeatportService.CMD_ALBUM_SEARCH_DESCRIPTION)]string albumUrl)
    {
        await beatport.AlbumCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatportService.CMD_ISRC_NAME, BeatportService.CMD_ISRC_DESCRIPTION)]
    public async Task Isrc([Summary(nameof(isrc), BeatportService.CMD_ISRC_SEARCH_DESCRIPTION)]string isrc)
    {
        await beatport.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatportService.CMD_LABEL_NAME, BeatportService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Summary(nameof(labelName), BeatportService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatport.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatportService.CMD_LABEL_CACHED_NAME, BeatportService.CMD_LABEL_CACHED_DESCRIPTION)]
    public async Task LabelCached([Summary(nameof(labelName), BeatportService.CMD_LABEL_CACHED_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatport.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}