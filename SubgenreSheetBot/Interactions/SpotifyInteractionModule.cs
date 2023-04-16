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

    public SpotifyInteractionModule(SpotifyService spotify)
    {
        this.spotify = spotify;
    }

    [SlashCommand(SpotifyService.CMD_TRACKS_NAME, SpotifyService.CMD_TRACKS_DESCRIPTION)]
    public async Task Tracks([Summary(nameof(url), SpotifyService.CMD_TRACKS_SEARCH_DESCRIPTION)]string url)
    {
        await spotify.TracksCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SpotifyService.CMD_ALBUM_NAME, SpotifyService.CMD_ALBUM_DESCRIPTION)]
    public async Task Info([Summary(nameof(url), SpotifyService.CMD_ALBUM_SEARCH_DESCRIPTION)]string url)
    {
        await spotify.AlbumCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SpotifyService.CMD_LABEL_NAME, SpotifyService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Summary(nameof(labelName), SpotifyService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await spotify.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SpotifyService.CMD_LABEL_WITH_YEAR_NAME, SpotifyService.CMD_LABEL_WITH_YEAR_DESCRIPTION)]
    public async Task Label([Summary(nameof(year), SpotifyService.CMD_LABEL_YEAR_DESCRIPTION)]int year, [Summary(nameof(labelName), SpotifyService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await spotify.LabelCommand(labelName, year, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SpotifyService.CMD_ARTIST_NAME, SpotifyService.CMD_ARTIST_DESCRIPTION)]
    public async Task Artist([Summary(nameof(artistName), SpotifyService.CMD_ARTIST_SEARCH_DESCRIPTION)]string artistName)
    {
        await spotify.ArtistCommand(artistName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SpotifyService.CMD_PEEP_NAME, SpotifyService.CMD_PEEP_DESCRIPTION)]
    public async Task Peep([Summary(nameof(labelName), SpotifyService.CMD_PEEP_SEARCH_DESCRIPTION)]string labelName)
    {
        await spotify.PeepCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [SlashCommand(SpotifyService.CMD_PEEPN_NAME, SpotifyService.CMD_PEEPN_DESCRIPTION)]
    public async Task PeepNoDoubleCheck([Summary(nameof(labelName), SpotifyService.CMD_PEEP_SEARCH_DESCRIPTION)]string labelName)
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