using System.Threading.Tasks;
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

    public SpotifyModule(SpotifyService spotify)
    {
        this.spotify = spotify;
    }

    [Command(SpotifyService.CMD_TRACKS_NAME), Alias("t"), Summary(SpotifyService.CMD_TRACKS_DESCRIPTION)]
    public async Task Tracks([Remainder, Summary(SpotifyService.CMD_TRACKS_SEARCH_DESCRIPTION)]string url)
    {
        await spotify.TracksCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SpotifyService.CMD_ALBUM_NAME), Alias("info", "i"), Summary(SpotifyService.CMD_ALBUM_DESCRIPTION)]
    public async Task Info([Remainder, Summary(SpotifyService.CMD_ALBUM_SEARCH_DESCRIPTION)]string url)
    {
        await spotify.AlbumCommand(url, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SpotifyService.CMD_LABEL_NAME), Alias("l"), Summary(SpotifyService.CMD_LABEL_DESCRIPTION)]
    public async Task Label([Remainder, Summary(SpotifyService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await spotify.LabelCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SpotifyService.CMD_LABEL_NAME), Alias("l"), Summary(SpotifyService.CMD_LABEL_WITH_YEAR_DESCRIPTION)]
    public async Task Label([Summary(SpotifyService.CMD_LABEL_YEAR_DESCRIPTION)]int year, [Remainder, Summary(SpotifyService.CMD_LABEL_SEARCH_DESCRIPTION)]string labelName)
    {
        await spotify.LabelCommand(labelName, year, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SpotifyService.CMD_ARTIST_NAME), Alias("a"), Summary(SpotifyService.CMD_ARTIST_DESCRIPTION)]
    public async Task Artist([Remainder, Summary(SpotifyService.CMD_ARTIST_SEARCH_DESCRIPTION)]string artistName)
    {
        await spotify.ArtistCommand(artistName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SpotifyService.CMD_PEEP_NAME), Summary(SpotifyService.CMD_PEEP_DESCRIPTION)]
    public async Task Peep([Remainder, Summary(SpotifyService.CMD_PEEP_SEARCH_DESCRIPTION)]string labelName)
    {
        await spotify.PeepCommand(labelName, new DynamicContext(Context), false, defaultOptions);
    }

    [Command(SpotifyService.CMD_PEEPN_NAME), Alias("pn")]
    public async Task PeepNoDoubleCheck([Remainder, Summary(SpotifyService.CMD_PEEP_SEARCH_DESCRIPTION)]string labelName)
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