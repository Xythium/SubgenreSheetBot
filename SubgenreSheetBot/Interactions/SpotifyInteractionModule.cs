using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Interactions;

[Group("spotify", "Spotify")]
public class SpotifyInteractionModule : InteractionModuleBase
{
    private readonly SpotifyService spotify;

    private readonly RequestOptions defaultOptions = new()
    {
        Timeout = 15
    };

    public SpotifyInteractionModule(SpotifyService spotify) { this.spotify = spotify; }

    [SlashCommand("tracks", "Get all tracks from an album")]
    public async Task Tracks([Summary(nameof(url), "Album ID to search for")] string url)
    {
        await spotify.TracksCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("album", "Get all tracks from an album")]
    public async Task Info([Summary(nameof(url), "Album ID to search for")] string url)
    {
        await spotify.AlbumCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("label", "Get all releases from a label")]
    public async Task Label([Summary(nameof(labelName), "Label name to search for")] string labelName)
    {
        await spotify.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("label-year", "Get all releases from a label from a certain year")]
    public async Task Label([Summary(nameof(year), "Year to find releases for")] int year, [Summary(nameof(labelName), "Label name to search for")] string labelName)
    {
        await spotify.LabelCommand(labelName, year, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("artist", "Get all releases from an artist")]
    public async Task Artist([Summary(nameof(artistName), "Artist to search for")] string artistName)
    {
        await spotify.ArtistCommand(artistName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("peep", "Peep")]
    public async Task Peep([Summary(nameof(labelName))] string labelName)
    {
        await spotify.PeepCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand("peepn", "Peep No double check")]
    public async Task PeepNoDoubleCheck([Summary(nameof(labelName))] string labelName)
    {
        await spotify.PeepNoDoubleCheckCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    /*[SlashCommand("isrc")]
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