using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Interactions;
using MetaBrainz.MusicBrainz;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using MusicTools.Parsing.Track;
using SubgenreSheetBot.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Interactions;

[Group("sheet", "Subgenre Sheet")]
public class SheetInteractionModule : InteractionModuleBase
{
    private readonly SheetService sheet;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public SheetInteractionModule(SheetService sheet) { this.sheet = sheet; }


    [SlashCommand("track", "Search for a track on the sheet")]
    public async Task Track([Summary(nameof(search), "Track to search for")] string search)
    {
        await sheet.TrackCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("trackexact", "Search for a track on the sheet")]
    public async Task TrackExact([Summary(nameof(search), "Track to search for")] string search)
    {
        await sheet.TrackExactCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("trackinfoexact", "Search for a track on the sheet")]
    public async Task TrackInfoExact([Summary(nameof(search), "Track to search for")] string search)
    {
        await sheet.TrackInfoExactCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("trackinfoforce", "Get information about a track")]
    public async Task TrackInfoForce([Summary(nameof(search), "Track to search for")] string search)
    {
        await sheet.TrackInfoForceCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("artist", "Returns info about an artist")]
    public async Task Artist([Summary(nameof(artist), "Artist to search for")] string artist)
    {
        await sheet.ArtistCommand(artist, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("artistdebug", "Returns a list of up to 15 artists most similar to the given input")]
    public async Task ArtistDebug([Summary(nameof(artist), "Artist to search for")] string artist)
    {
        await sheet.ArtistDebugCommand(artist, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("genre", "Returns a list of up to 8 tracks of a given genre")]
    public async Task Genre([Summary(nameof(genre), "Genre to search for")] string genre)
    {
        await sheet.GenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("genreinfo", "Returns information of a genre")]
    public async Task GenreInfo([Summary(nameof(genre), "Genre to search for")] string genre)
    {
        await sheet.GenreInfoCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("subgenre", "Returns a list of up to 8 tracks of a given subgenre")]
    public async Task Subgenre([Summary(nameof(genre), "Genre to search for")] string genre)
    {
        await sheet.SubgenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("subgenreexact", "Returns a list of up to 8 tracks of a given subgenre")]
    public async Task SubgenreExact([Summary(nameof(genre), "Genre to search for")] string genre)
    {
        await sheet.SubgenreExactCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("labels", "todo")]
    public async Task Labels()
    {
        await sheet.LabelsCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("label", "todo")]
    public async Task Label([Summary(nameof(label))] string label)
    {
        await sheet.LabelCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("labelartists", "todo")]
    public async Task LabelArtists([Summary(nameof(label))] string label)
    {
        await sheet.LabelArtistsCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("debug", "todo")]
    public async Task Debug()
    {
        await sheet.DebugCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("markwhen", "todo")]
    public async Task Markwhen()
    {
        await sheet.MarkwhenCommand(new DynamicContext(Context), false, defaultOptions);
    }

    /*[SlashCommand("query", "todo")]
    public async Task Query(SheetService.QueryArguments arguments)
    {
        await sheet.QueryCommand(arguments, new DynamicContext(Context), false, defaultOptions);
    }*/

   

    /*[SlashCommand("mbsubmit", "todo")]
    public async Task MusicBrainzSubmit()
    {
        if (Context.User.Discriminator != "0001")
            return;

        await RevalidateCache();

        var sb = new StringBuilder("This would be submitted if Mark enabled it:\r\n");
        sb.AppendLine($"User Agent: {GetQuery().UserAgent}");

        var message = await Context.Message.ReplyAsync("recordings found: 0");
        var found = 0;
        var lastSend = new DateTime(1970, 1, 1);

        var labels = GetAllLabelNames();

        foreach (var l in labels)
        {
            if (_addedLabels.Contains(l))
                continue;

            var label = (await GetQuery()
                    .FindLabelsAsync($"label:\"{l}\"", 1)).Results.FirstOrDefault()
                ?.Item;

            if (label is null)
            {
                await Context.Message.ReplyAsync($"{l} not found");
                continue;
            }

            var releases = await GetQuery()
                .BrowseLabelReleasesAsync(label.Id, 100, inc: Include.Recordings | Include.ArtistCredits);

            foreach (var release in releases.Results)
            {
                if (release.Media is null)
                    continue;

                foreach (var medium in release.Media)
                {
                    if (medium.Tracks is null)
                        continue;

                    foreach (var track in medium.Tracks)
                    {
                        if (track.Recording != null)
                            _recordings.Add(track.Recording);
                    }
                }
            }

            _addedLabels.Add(l);
        }

        await Context.Message.ReplyAsync($"{_recordings.Count} tracks");

        var notFound = new List<Entry>();

        foreach (var entry in _entries)
        {
            if (entry.SubgenresList.SequenceEqual(new[]
            {
                "?"
            }))
                continue;

            var tags = GetQuery()
                .SubmitTags(CLIENT_ID);

            var recordings = _recordings.Where(t => string.Equals(t.Title, entry.Title, StringComparison.OrdinalIgnoreCase) && string.Equals(t.ArtistCredit?.First()
                    ?.Name, entry.ArtistsList.First(), StringComparison.OrdinalIgnoreCase))
                .Distinct(new MusicBrainzTrackComparer())
                .ToArray();

            if (recordings.Length > 0)
            {
                sb.AppendLine($"{entry.OriginalArtists} - {entry.Title}:");

                foreach (var recording in recordings)
                {
                    sb.AppendLine($"\t{string.Join(" x ", recording.ArtistCredit.Select(ac => ac.Artist.Name))} - {recording.Title} ({recording.Id})");

                    found++;

                    if (DateTime.UtcNow.Subtract(lastSend)
                            .TotalSeconds > 10)
                    {
                        message = await UpdateOrSend(message, $"recordings found: {++found}");
                        lastSend = DateTime.UtcNow;
                    }

                    tags.Add(recording, TagVote.Up, entry.SubgenresList);
                    await tags.SubmitAsync();
                }

                sb.AppendLine($"\t\tTags: {string.Join(", ", entry.SubgenresList)}");
            }
            else
            {
                notFound.Add(entry);
            }
        }

        await SendOrAttachment(sb.ToString());

        if (notFound.Count > 0)
        {
            await SendOrAttachment(string.Join("\r\n", notFound.Select(t => $"{t.OriginalArtists} - {t.Title}")));
        }
    }*/

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