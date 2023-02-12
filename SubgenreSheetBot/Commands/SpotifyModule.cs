﻿using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Commands;

[Group("Spotify"), Alias("s")]
public class SpotifyModule : ModuleBase
{
    private readonly SpotifyService spotify;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public SpotifyModule(SpotifyService spotify) { this.spotify = spotify; }

    [Command("tracks"), Alias("t"), Summary("Get all tracks from an album")]
    public async Task Tracks([Remainder, Summary("Album ID to search for")] string url)
    {
        await spotify.TracksCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("info"), Alias("i"), Summary("Get all tracks from an album")]
    public async Task Info([Remainder, Summary("Album ID to search for")] string url)
    {
        await spotify.AlbumCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("label"), Alias("l"), Summary("Get all releases from a label")]
    public async Task Label([Remainder, Summary("Label name to search for")] string labelName)
    {
        await spotify.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("label"), Alias("l"), Summary("Get all releases from a label from a certain year")]
    public async Task Label([Summary("Year to find releases for")] int year, [Remainder, Summary("Label name to search for")] string labelName)
    {
        await spotify.LabelCommand(labelName, year, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("artist"), Alias("a"), Summary("Get all releases from an artist")]
    public async Task Artist([Remainder, Summary("Artist to search for")] string artistName)
    {
        await spotify.ArtistCommand(artistName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("peep")]
    public async Task Peep([Remainder] string labelName)
    {
        await spotify.PeepCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command("peepn"), Alias("pn")]
    public async Task PeepNoDoubleCheck([Remainder] string labelName)
    {
        await spotify.PeepNoDoubleCheckCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    /*[Command("isrc")]
    public async Task Isrc(
        [Remainder, Summary("Label name to search for")]
        string labelName)
    {
        var sb = new StringBuilder();

        IUserMessage message = null;

        try
        {
            for (int i = 423 - 1; i >= 0; i--)
            {
                var isrc = $"GBTDG07{i.ToString().PadLeft(5, '0')}";
                if (i % 50 == 0)
                    message = await UpdateOrSend(message, $"{isrc} looking");

                var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"isrc:\"{isrc}\""));

                await foreach (var track in api.Paginate(response.Tracks, s => s.Tracks))
                {
                    //message = await UpdateOrSend(message, $"GBTDG13{i.ToString().PadLeft(5, '0')} found");
                    sb.AppendLine($"{string.Join(" & ", track.Artists.Select(a => a.Name.ToUpper()))},{track.Name.ToUpper()},{isrc},,,{TimeSpan.FromMilliseconds(track.DurationMs)}");
                }

                await Task.Delay(100);
            }
        }
        catch
        {
        }
        finally
        {
            await SendOrAttachment(sb.ToString());
        }
    }*/
}