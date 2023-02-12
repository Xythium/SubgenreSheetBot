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

    public BeatsourceInteractionModule(BeatsourceService beatsource) { this.beatsource = beatsource; }

    [SlashCommand("tracks", "Get all tracks from an album")]
    public async Task Tracks([Summary(nameof(albumUrl), "Album ID to search for")] string albumUrl)
    {
        await beatsource.TracksCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("album", "Get all tracks from an album")]
    public async Task Album([Summary(nameof(albumUrl), "Album ID to search for")] string albumUrl)
    {
        await beatsource.AlbumCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("isrc", "Search by ISRC")]
    public async Task Isrc([Summary(nameof(isrc), "ISRC to search for")] string isrc)
    {
        await beatsource.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("label", "Get all releases from a label")]
    public async Task Label([Summary(nameof(labelName), "Label name to search for")] string labelName)
    {
        await beatsource.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("labelcached", "Get all releases from a label")]
    public async Task LabelCached([Summary(nameof(labelName), "Label name to search for")] string labelName)
    {
        await beatsource.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}