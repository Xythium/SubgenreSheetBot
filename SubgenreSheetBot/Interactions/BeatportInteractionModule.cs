using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BeatportApi.Beatport;
using Common;
using Common.Beatport;
using Discord;
using Discord.Interactions;
using Google.Apis.Sheets.v4.Data;
using Raven.Client;
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

    public BeatportInteractionModule(BeatportService beatport) { this.beatport = beatport; }

    [SlashCommand("tracks", "Get all tracks from an album")]
    public async Task Tracks([Summary(nameof(albumUrl), "Album ID to search for")] string albumUrl)
    {
        await beatport.TracksCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("album", "Get all tracks from an album")]
    public async Task Album([Summary(nameof(albumUrl), "Album ID to search for")] string albumUrl)
    {
        await beatport.AlbumCommand(albumUrl, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("isrc", "Search by ISRC")]
    public async Task Isrc([Summary(nameof(isrc), "ISRC to search for")] string isrc)
    {
        await beatport.IsrcCommand(isrc, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("label", "Get all releases from a label")]
    public async Task Label([Summary(nameof(labelName), "Label name to search for")] string labelName)
    {
        await beatport.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("labelcached", "Get all releases from a label")]
    public async Task LabelCached([Summary(nameof(labelName), "Label name to search for")] string labelName)
    {
        await beatport.LabelCachedCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }
}