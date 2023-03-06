using System.Collections.Generic;
using System.Threading.Tasks;
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

    public SpreadsheetModule(SheetService sheet)
    {
        this.sheet = sheet;
    }

    [Command("track"), Alias("t"), Summary("Search for a track on the sheet")]
    public async Task Track([Remainder, Summary("Track to search for")]string search)
    {
        await sheet.TrackCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("trackexact"), Alias("te"), Summary("Search for a track on the sheet")]
    public async Task TrackExact([Remainder, Summary("Track to search for")]string search)
    {
        await sheet.TrackExactCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("trackinfoexact"), Alias("tie"), Summary("Search for a track on the sheet")]
    public async Task TrackInfoExact([Remainder, Summary("Track to search for")]string search)
    {
        await sheet.TrackInfoExactCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("trackinfoforce"), Alias("tif"), Summary("Get information about a track")]
    public async Task TrackInfoForce([Remainder, Summary("Track to search for")]string search)
    {
        await sheet.TrackInfoForceCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("artist"), Alias("a"), Summary("Returns info about an artist")]
    public async Task Artist([Remainder, Summary("Artist to search for")]string artist)
    {
        await sheet.ArtistCommand(artist, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("artistdebug"), Alias("ad"), Summary("Returns a list of up to 15 artists most similar to the given input")]
    public async Task ArtistDebug([Remainder, Summary("Artist to search for")]string artist)
    {
        await sheet.ArtistDebugCommand(artist, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("genre"), Alias("g"), Summary("Returns a list of up to 8 tracks of a given genre")]
    public async Task Genre([Remainder, Summary("Genre to search for")]string genre)
    {
        await sheet.GenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("genreinfo"), Alias("gi"), Summary("Returns information of a genre")]
    public async Task GenreInfo([Remainder, Summary("Genre to search for")]string genre)
    {
        await sheet.GenreInfoCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("subgenre"), Alias("sg"), Summary("Returns a list of up to 8 tracks of a given subgenre")]
    public async Task Subgenre([Remainder, Summary("Genre to search for")]string genre)
    {
        await sheet.SubgenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("subgenreexact"), Alias("sge"), Summary("Returns a list of up to 8 tracks of a given subgenre")]
    public async Task SubgenreExact([Remainder, Summary("Genre to search for")]string genre)
    {
        await sheet.SubgenreExactCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("labels"), Alias("ls")]
    public async Task Labels()
    {
        await sheet.LabelsCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command("label"), Alias("l")]
    public async Task Label([Remainder]string label)
    {
        await sheet.LabelCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("labelartists"), Alias("la")]
    public async Task LabelArtists([Remainder]string label)
    {
        await sheet.LabelArtistsCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("debug")]
    public async Task Debug()
    {
        await sheet.DebugCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command("markwhen")]
    public async Task Markwhen()
    {
        await sheet.MarkwhenCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [Command("query"), Alias("q")]
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

    public bool Equals(IRecording x, IRecording y)
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