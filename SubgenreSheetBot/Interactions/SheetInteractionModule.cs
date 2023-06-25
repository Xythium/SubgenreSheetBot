using System.Threading.Tasks;
using Common;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Autocomplete;
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

    public SheetInteractionModule(SheetService sheet)
    {
        this.sheet = sheet;
    }

    [SlashCommand(SheetService.CMD_TRACK_NAME, SheetService.CMD_TRACK_DESCRIPTION)]
    public async Task Track([Summary(nameof(title), SheetService.CMD_TRACK_TITLE_DESCRIPTION)]string title,
                            [Summary(nameof(artist), SheetService.CMD_TRACK_ARTIST_DESCRIPTION), Autocomplete(typeof(ArtistAutocomplete))]string artist = "",
                            [Summary(nameof(matchMode), SheetService.CMD_TRACK_MATCH_DESCRIPTION)]MatchMode matchMode = MatchMode.Exact,
                            [Summary(nameof(threshold), SheetService.CMD_TRACK_THRESHOLD_DESCRIPTION)]int threshold = 80)
    {
        var matchOptions = new SheetService.MatchOptions
        {
            MatchMode = matchMode,
            Threshold = threshold
        };
        await sheet.TrackCommand(artist, title, matchOptions, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_TRACK_INFO_EXACT_NAME, SheetService.CMD_TRACK_INFO_EXACT_DESCRIPTION)]
    public async Task TrackInfoExact([Summary(nameof(search), SheetService.CMD_TRACK_INFO_EXACT_SEARCH_DESCRIPTION)]string search)
    {
        await sheet.TrackInfoExactCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_TRACK_INFO_FORCE_NAME, SheetService.CMD_TRACK_INFO_FORCE_DESCRIPTION)]
    public async Task TrackInfoForce([Summary(nameof(search), SheetService.CMD_TRACK_INFO_FORCE_SEARCH_DESCRIPTION)]string search)
    {
        await sheet.TrackInfoForceCommand(search, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_ARTIST_NAME, SheetService.CMD_ARTIST_DESCRIPTION)]
    public async Task Artist([Summary(nameof(search), SheetService.CMD_ARTIST_SEARCH_DESCRIPTION), Autocomplete(typeof(ArtistAutocomplete))]string search,
                             [Summary(nameof(matchMode), SheetService.CMD_ARTIST_MATCH_DESCRIPTION)]MatchMode matchMode = MatchMode.Exact,
                             [Summary(nameof(threshold), SheetService.CMD_ARTIST_THRESHOLD_DESCRIPTION)]int threshold = 80)
    {
        var matchOptions = new SheetService.MatchOptions
        {
            MatchMode = matchMode,
            Threshold = threshold
        };
        await sheet.ArtistCommand(search, matchOptions, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_ARTIST_DEBUG_NAME, SheetService.CMD_ARTIST_DEBUG_DESCRIPTION)]
    public async Task ArtistDebug([Summary(nameof(artist), SheetService.CMD_ARTIST_DEBUG_SEARCH_DESCRIPTION), Autocomplete(typeof(ArtistAutocomplete))]string artist)
    {
        await sheet.ArtistDebugCommand(artist, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_GENRE_NAME, SheetService.CMD_GENRE_DESCRIPTION)]
    public async Task Genre([Summary(nameof(genre), SheetService.CMD_GENRE_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.GenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_GENRE_INFO_NAME, SheetService.CMD_GENRE_INFO_DESCRIPTION)]
    public async Task GenreInfo([Summary(nameof(genre), SheetService.CMD_GENRE_INFO_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.GenreInfoCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_SUBGENRE_NAME, SheetService.CMD_SUBGENRE_DESCRIPTION)]
    public async Task Subgenre([Summary(nameof(genre), SheetService.CMD_SUBGENRE_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.SubgenreCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_SUBGENRE_EXACT_NAME, SheetService.CMD_SUBGENRE_EXACT_DESCRIPTION)]
    public async Task SubgenreExact([Summary(nameof(genre), SheetService.CMD_SUBGENRE_EXACT_SEARCH_DESCRIPTION)]string genre)
    {
        await sheet.SubgenreExactCommand(genre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_LABELS_NAME, SheetService.CMD_LABELS_DESCRIPTION)]
    public async Task Labels()
    {
        await sheet.LabelsCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_LABEL_NAME, SheetService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Summary(nameof(label), SheetService.CMD_LABEL_SEARCH_DESCRIPTION)]string label)
    {
        await sheet.LabelCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_LABEL_ARTISTS_NAME, SheetService.CMD_LABEL_ARTISTS_DESCRIPTION)]
    public async Task LabelArtists([Summary(nameof(label), SheetService.CMD_LABEL_ARTISTS_SEARCH_DESCRIPTION)]string label)
    {
        await sheet.LabelArtistsCommand(label, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_DEBUG_NAME, SheetService.CMD_DEBUG_DESCRIPTION)]
    public async Task Debug()
    {
        await sheet.DebugCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_MARKWHEN_NAME, SheetService.CMD_MARKWHEN_DESCRIPTION)]
    public async Task Markwhen()
    {
        await sheet.MarkwhenCommand(new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_SUBGENRE_GRAPH_NAME, SheetService.CMD_SUBGENRE_GRAPH_DESCRIPTION)]
    public async Task SubgenreGraph([Summary(nameof(subgenre), SheetService.CMD_SUBGENRE_GRAPH_SEARCH_DESCRIPTION), Autocomplete(typeof(SubgenreAutocomplete))]string subgenre,
                                    [Summary(nameof(engine), SheetService.CMD_SUBGENRE_GRAPH_ENGINE_DESCRIPTION), Choice("dot", "dot"), Choice("neato", "neato"), Choice("force-directed placement", "fdp"), Choice("scalable force-directed placement", "sfdp"), Choice("circo", "circo"), Choice("twopi", "twopi"), Choice("nop", "nop"), Choice("osage", "osage"), Choice("patchwork", "patchwork")]string engine = "dot",
                                    [Summary(nameof(maxSubgenreDepth), SheetService.CMD_SUBGENRE_GRAPH_MAXDEPTH_DESCRIPTION)]int maxSubgenreDepth = 1)
    {
        var graphOptions = new SheetService.SheetGraphCommandOptions
        {
            Subgenre = subgenre,
            Engine = engine,
            MaxSubgenreDepth = maxSubgenreDepth
        };
        await sheet.SubgenreGraphCommand(graphOptions, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_SUBGENRE_DEBUG_NAME, SheetService.CMD_SUBGENRE_DEBUG_DESCRIPTION)]
    public async Task SubgenreDebug([Summary(nameof(subgenre), SheetService.CMD_SUBGENRE_DEBUG_SEARCH_DESCRIPTION), Autocomplete(typeof(SubgenreAutocomplete))]string subgenre)
    {
        await sheet.SubgenreDebugCommand(subgenre, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SheetService.CMD_COLLAB_GRAPH_NAME, SheetService.CMD_COLLAB_GRAPH_DESCRIPTION)]
    public async Task CollabGraph([Summary(nameof(artist), SheetService.CMD_COLLAB_GRAPH_SEARCH_DESCRIPTION)]string artist,
                                  [Summary(nameof(maxDepth), "todo")]int maxDepth = 1,
                                  [Summary(nameof(engine), SheetService.CMD_COLLAB_GRAPH_ENGINE_DESCRIPTION), Choice("dot", "dot"), Choice("neato", "neato"), Choice("force-directed placement", "fdp"), Choice("scalable force-directed placement", "sfdp"), Choice("circo", "circo"), Choice("twopi", "twopi"), Choice("nop", "nop"), Choice("osage", "osage"), Choice("patchwork", "patchwork")]string engine = "dot",
                                  [Summary(nameof(matchMode), SheetService.CMD_ARTIST_MATCH_DESCRIPTION)]MatchMode matchMode = MatchMode.Exact,
                                  [Summary(nameof(threshold), SheetService.CMD_ARTIST_THRESHOLD_DESCRIPTION)]int threshold = 80)
    {
        var graphOptions = new SheetService.CollabGraphCommandOptions
        {
            StartArtist = artist,
            Engine = engine,
            MaxSubgenreDepth = maxDepth
        };
        var matchOptions = new SheetService.MatchOptions
        {
            MatchMode = matchMode,
            Threshold = threshold
        };
        await sheet.CollabGraphCommand(graphOptions, matchOptions, new DynamicContext(Context), false, defaultOptions);
    }


    [SlashCommand(SheetService.CMD_QUERY_NAME, SheetService.CMD_QUERY_DESCRIPTION)]
    public async Task Query([Summary(nameof(artist), SheetService.CMD_QUERY_ARTIST_DESCRIPTION)]string? artist = null,
                            [Summary(nameof(artistCount), SheetService.CMD_QUERY_ARTIST_COUNT_DESCRIPTION)]string? artistCount = null,
                            [Summary(nameof(subgenre), SheetService.CMD_QUERY_SUBGENRE_DESCRIPTION)]string? subgenre = null,
                            [Summary(nameof(subgenreCount), SheetService.CMD_QUERY_SUBGENRE_COUNT_DESCRIPTION)]string? subgenreCount = null,
                            [Summary(nameof(label), SheetService.CMD_QUERY_LABEL_DESCRIPTION)]string? label = null,
                            [Summary(nameof(labelCount), SheetService.CMD_QUERY_LABEL_COUNT_DESCRIPTION)]string? labelCount = null,
                            [Summary(nameof(before), SheetService.CMD_QUERY_BEFORE_DESCRIPTION)]string? before = null,
                            [Summary(nameof(after), SheetService.CMD_QUERY_AFTER_DESCRIPTION)]string? after = null,
                            [Summary(nameof(date), SheetService.CMD_QUERY_DATE_DESCRIPTION)]string? date = null,
                            [Summary(nameof(select), SheetService.CMD_QUERY_SELECT_DESCRIPTION)]string? select = null,
                            [Summary(nameof(order), SheetService.CMD_QUERY_ORDER_DESCRIPTION)]string? order = null)
    {
        var arguments = new SheetService.QueryArguments
        {
            Artist = artist,
            ArtistCount = artistCount,
            Subgenre = subgenre,
            SubgenreCount = subgenreCount,
            Label = label,
            LabelCount = labelCount,
            Before = before,
            After = after,
            Date = date,
            Select = select,
            Order = order
        };
        await sheet.QueryCommand(arguments, new DynamicContext(Context), false, defaultOptions);
    }


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