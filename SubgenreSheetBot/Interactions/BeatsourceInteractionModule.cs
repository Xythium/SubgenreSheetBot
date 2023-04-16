using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Interactions;

[Group("beatsource", "Beatsource")]
public class BeatsourceInteractionModule : InteractionModuleBase
{
    private readonly BeatsourceService beatsource;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public BeatsourceInteractionModule(BeatsourceService beatsource)
    {
        this.beatsource = beatsource;
    }

    [SlashCommand(BeatsourceService.CMD_TRACKS_NAME, BeatsourceService.CMD_TRACKS_DESCRIPTION)]
    public async Task Tracks([Summary(nameof(albumUrl), BeatsourceService.CMD_TRACKS_SEARCH_DESCRIPTION)]string albumUrl)
    {
        await beatsource.TracksCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatsourceService.CMD_ALBUM_NAME, BeatsourceService.CMD_ALBUM_DESCRIPTION)]
    public async Task Album([Summary(nameof(albumUrl), BeatsourceService.CMD_ALBUM_SEARCH_DESCRIPTION)]string albumUrl)
    {
        await beatsource.AlbumCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatsourceService.CMD_ISRC_NAME, BeatsourceService.CMD_ISRC_DESCRIPTION)]
    public async Task Isrc([Summary(nameof(isrc), BeatsourceService.CMD_ISRC_SEARCH_DESCRIPTION)]string isrc)
    {
        await beatsource.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatsourceService.CMD_LABEL_NAME, BeatsourceService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Summary(nameof(labelName), BeatsourceService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatsource.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(BeatsourceService.CMD_LABEL_CACHED_NAME, BeatsourceService.CMD_LABEL_CACHED_DESCRIPTION)]
    public async Task LabelCached([Summary(nameof(labelName), BeatsourceService.CMD_LABEL_CACHED_SEARCH_DESCRIPTION)]string labelName)
    {
        await beatsource.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}