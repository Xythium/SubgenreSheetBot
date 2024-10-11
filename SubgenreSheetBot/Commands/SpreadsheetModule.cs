using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common;
using Discord;
using Discord.Commands;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

public class SpreadsheetModule : ModuleBase
{
    private readonly SheetService sheet;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    private readonly SheetService.MatchOptions defaultFuzzyMatchOptions = new()
    {
        MatchMode = MatchMode.Fuzzy,
        Threshold = 80
    };

    private readonly SheetService.MatchOptions defaultExactMatchOptions = new()
    {
        MatchMode = MatchMode.Exact
    };

    public SpreadsheetModule(SheetService sheet)
    {
        this.sheet = sheet;
    }

    [Command(SheetService.CMD_TRACK_NAME), Alias("t"), Summary(SheetService.CMD_TRACK_DESCRIPTION)]
    public async Task Track([Remainder, Summary(SheetService.CMD_TRACK_SEARCH_DESCRIPTION)]string search)
    {
        var context = new DynamicContext(Context);
        var split = search.Split(new[]
        {
            " - "
        }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length != 2)
        {
            await context.ErrorAsync($"cannot parse `{search}` into `Artist - Title` or `Title`");
            return;
        }

        var artist = split[0];
        var title = split[1];
        
        await sheet.TrackCommand(artist, title, defaultFuzzyMatchOptions, context, false, defaultOptions);
    }

    [Command(SheetService.CMD_TRACK_EXACT_NAME), Alias("te"), Summary(SheetService.CMD_TRACK_DESCRIPTION)]
    public async Task TrackExact([Remainder, Summary(SheetService.CMD_TRACK_SEARCH_DESCRIPTION)]string search)
    {
        var context = new DynamicContext(Context);
        var split = search.Split(new[]
        {
            " - "
        }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length != 2)
        {
            await context.ErrorAsync($"cannot parse `{search}` into `Artist - Title` or `Title`");
            return;
        }

        var artist = split[0];
        var title = split[1];
        
        await sheet.TrackCommand(artist, title, defaultExactMatchOptions, context, false, defaultOptions);
    }

    [Command(SheetService.CMD_TRACK_INFO_EXACT_NAME), Alias("tie"), Summary(SheetService.CMD_TRACK_INFO_EXACT_DESCRIPTION)]
    public async Task TrackInfoExact([Remainder, Summary(SheetService.CMD_TRACK_INFO_EXACT_SEARCH_DESCRIPTION)]string search)
    {
        await sheet.TrackInfoExactCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_TRACK_INFO_FORCE_NAME), Alias("tif"), Summary(SheetService.CMD_TRACK_INFO_FORCE_DESCRIPTION)]
    public async Task TrackInfoForce([Remainder, Summary(SheetService.CMD_TRACK_INFO_FORCE_SEARCH_DESCRIPTION)]string search)
    {
        await sheet.TrackInfoForceCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_ARTIST_NAME), Alias("a"), Summary(SheetService.CMD_ARTIST_DESCRIPTION)]
    public async Task Artist([Remainder, Summary(SheetService.CMD_ARTIST_SEARCH_DESCRIPTION)]string artist)
    {
        await sheet.ArtistCommand(artist, defaultFuzzyMatchOptions, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_ARTIST_EXACT_NAME), Alias("ae"), Summary(SheetService.CMD_ARTIST_DESCRIPTION)]
    public async Task ArtistExact([Remainder, Summary(SheetService.CMD_ARTIST_SEARCH_DESCRIPTION)]string artist)
    {
        await sheet.ArtistCommand(artist, defaultExactMatchOptions, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_ARTIST_DEBUG_NAME), Alias("ad"), Summary(SheetService.CMD_ARTIST_DEBUG_DESCRIPTION)]
    public async Task ArtistDebug([Remainder, Summary(SheetService.CMD_ARTIST_DEBUG_SEARCH_DESCRIPTION)]string artist)
    {
        await sheet.ArtistDebugCommand(artist, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_GENRE_NAME), Alias("g"), Summary(SheetService.CMD_GENRE_DESCRIPTION)]
    public async Task Genre([Remainder, Summary(SheetService.CMD_GENRE_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.GenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_GENRE_INFO_NAME), Alias("gi"), Summary(SheetService.CMD_GENRE_INFO_DESCRIPTION)]
    public async Task GenreInfo([Remainder, Summary(SheetService.CMD_GENRE_INFO_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.GenreInfoCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_SUBGENRE_NAME), Alias("sg"), Summary(SheetService.CMD_SUBGENRE_DESCRIPTION)]
    public async Task Subgenre([Remainder, Summary(SheetService.CMD_SUBGENRE_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.SubgenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }  
    
    [Command(SheetService.CMD_SUBGENRE_INFO_NAME), Alias("sgi"), Summary(SheetService.CMD_SUBGENRE_INFO_DESCRIPTION)]
    public async Task SubgenreInfo([Remainder, Summary(SheetService.CMD_SUBGENRE_INFO_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.SubgenreInfoCommand(genre, defaultExactMatchOptions, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_SUBGENRE_EXACT_NAME), Alias("sge"), Summary(SheetService.CMD_SUBGENRE_EXACT_DESCRIPTION)]
    public async Task SubgenreExact([Remainder, Summary(SheetService.CMD_SUBGENRE_EXACT_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.SubgenreExactCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_LABELS_NAME), Alias("ls"), Summary(SheetService.CMD_LABELS_DESCRIPTION)]
    public async Task Labels()
    {
        await sheet.LabelsCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_LABEL_NAME), Alias("l"), Summary(SheetService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Remainder, Summary(SheetService.CMD_LABEL_SEARCH_DESCRIPTION)]string label)
    {
        await sheet.LabelCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_LABEL_ARTISTS_NAME), Alias("la"), Summary(SheetService.CMD_LABEL_ARTISTS_DESCRIPTION)]
    public async Task LabelArtists([Remainder, Summary(SheetService.CMD_LABEL_ARTISTS_SEARCH_DESCRIPTION)]string label)
    {
        await sheet.LabelArtistsCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_DEBUG_NAME)]
    public async Task Debug()
    {
        await sheet.DebugCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_MARKWHEN_NAME)]
    public async Task Markwhen()
    {
        await sheet.MarkwhenCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SheetService.CMD_QUERY_NAME), Alias("q")]
    public async Task Query(SheetService.QueryArguments arguments)
    {
        await sheet.QueryCommand(arguments, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("mbsubmit")]
    public async Task MusicBrainzSubmit()
    {
        if (Context.User.Discriminator != "0001")
            return;

        await sheet.MusicBrainzSubmitCommand(new DynamicContext(Context), false, defaultOptions);
    }
    

    [Command(SheetService.CMD_NOTINTREE_NAME)]
    public async Task NotInTree()
    {
      await sheet.NotInTreeCommand(new DynamicContext(Context), false, defaultOptions);
    }

    /*[Command("say")]
    public async Task Say(string server, string channel, [Remainder] string message)
    {
        if (Context.User.Discriminator != "0001")
            return;

        var guilds = await Context.Client.GetGuildsAsync();
        var guild = guilds.FirstOrDefault(g => string.Equals(g.Name, server, StringComparison.OrdinalIgnoreCase));

        if (guild is null)
        {
            await Context.Message.ReplyAsync($"server not found. {string.Join(", ", guilds.Select(g => g.Name))}");
            return;
        }

        var channels = await guild.GetChannelsAsync();
        var chl = (IMessageChannel) channels.FirstOrDefault(c => string.Equals(c.Name, channel, StringComparison.OrdinalIgnoreCase));

        if (chl is null)
        {
            await Context.Message.ReplyAsync($"channel not found. {string.Join(", ", channels.Select(g => g.Name))}");
            return;
        }

        await chl.SendMessageAsync(message);
    }*/
}

public class MusicBrainzTrackComparer : IComparer<IRecording>, IEqualityComparer<IRecording>
{
    public int Compare(IRecording x, IRecording y)
    {
        return x.Id.CompareTo(y.Id);
    }

    public bool Equals(IRecording? x, IRecording? y)
    {
        if (x is null || y is null)
            return false;

        return x.Id == y.Id;
    }

    public int GetHashCode(IRecording obj)
    {
        return obj.Id.GetHashCode();
    }
}