using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.Spotify;
using Discord;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Services;

public class SpotifyService
{
    private readonly SpotifyClient api;

    public SpotifyService()
    {
        if (api != null)
            throw new Exception("API already initialized");

        var config = SpotifyClientConfig.CreateDefault()
                                        .WithAuthenticator(new ClientCredentialsAuthenticator(File.ReadAllText("spotify_id"), File.ReadAllText("spotify_secret")))
                                        //.WithDefaultPaginator(new CachingPaginator())
                                        .WithRetryHandler(new SimpleRetryHandler());

        api = new SpotifyClient(config);
    }

    private async Task<FullAlbum> GetAlbum(string albumId)
    {
        using var session = SubgenreSheetBot.SpotifyStore.OpenSession();
        return await SpotifyDbUtils.GetAlbumOrCache(api, session, albumId);
    }

    private async Task<List<FullTrack>> GetTracks(FullAlbum album)
    {
        using var session = SubgenreSheetBot.SpotifyStore.OpenSession();
        return await SpotifyDbUtils.GetTracksOrCache(api, session, album.Tracks.Items);
    }

    private async Task<List<TrackAudioFeatures>> GetFeatures(FullAlbum album)
    {
        using var session = SubgenreSheetBot.SpotifyStore.OpenSession();
        return await SpotifyDbUtils.GetFeaturesOrCache(api, session, album.Tracks.Items);
    }

#region Tracks

    public const string CMD_TRACKS_NAME = "tracks";
    public const string CMD_TRACKS_DESCRIPTION = "Get all tracks from an album";
    public const string CMD_TRACKS_SEARCH_DESCRIPTION = "Album ID to search for";

    public async Task TracksCommand(string url, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        var (trackId, albumId) = SpotifyUtils.GetIdFromUrl(url);

        if (!string.IsNullOrWhiteSpace(trackId))
        {
            await context.ErrorAsync("Track urls are not supported");
            return;
        }

        if (string.IsNullOrWhiteSpace(albumId))
        {
            await context.ErrorAsync("No album ID found");
            return;
        }

        var album = await GetAlbum(albumId);
        var tracks = await GetTracks(album);
        var features = await GetFeatures(album);

        var sb = new StringBuilder();

        //Date	SP?	SC?	BP?	BC?	Genre Color	Subgenres	Artists	Song Title	Primary Label	Length	BPM	Key	Catalog	ISRC
        //2022-01-21	TRUE	FALSE	TRUE	FALSE	House	Progressive House	Kaskade & Ella Vos	Eyes v3	Arkade	4:30	124	G maj		
        foreach (var track in tracks)
        {
            var feature = features.FirstOrDefault(f => f.Uri == track.Uri);

            sb.AppendLine($"`{album.ReleaseDate},TRUE,FALSE,FALSE,FALSE,?,?,{string.Join(" & ", track.Artists.Select(a => a.Name))},{track.Name},{album.Label},{TimeSpan.FromMilliseconds(track.DurationMs):m':'ss},{(feature is null ? "" : Math.Round(feature.Tempo).ToString())},{(feature is null ? "" : $"{SpotifyUtils.IntToKey(feature.Key)} {SpotifyUtils.IntToMode(feature.Mode)}")},,{track.ExternalIds["isrc"]}`");
        }

        await context.SendOrAttachment(sb.ToString(), true);
    }

#endregion

#region Album

    public const string CMD_ALBUM_NAME = "album";
    public const string CMD_ALBUM_DESCRIPTION = "Get all tracks from an album";
    public const string CMD_ALBUM_SEARCH_DESCRIPTION = "Album ID to search for";

    public async Task AlbumCommand(string url, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        var (trackId, albumId) = SpotifyUtils.GetIdFromUrl(url);

        if (!string.IsNullOrWhiteSpace(trackId))
        {
            await context.ErrorAsync("Track urls are not supported");
            return;
        }

        if (string.IsNullOrWhiteSpace(albumId))
        {
            await context.ErrorAsync("No album ID found");
            return;
        }

        var album = await GetAlbum(albumId);
        var tracks = await GetTracks(album);
        var features = await GetFeatures(album);

        var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(album, tracks, features));

        if (file != null)
        {
            await context.FollowupWithFileAsync(file, "tracklist.txt", embed: embed.Build());
            //await Context.Channel.SendFileAsync(file, "tracklist.txt", embed: embed.Build(), messageReference: new MessageReference(Context.Message.Id));
            file.Close();
        }
        else
        {
            await context.FollowupAsync(embed: embed.Build());
        }
    }

#endregion


#region Label

    public const string CMD_LABEL_NAME = "label";
    public const string CMD_LABEL_WITH_YEAR_NAME = "label-year";
    public const string CMD_LABEL_DESCRIPTION = "Get all releases from an album";
    public const string CMD_LABEL_WITH_YEAR_DESCRIPTION = "Get all releases from an album from a certain year";
    public const string CMD_LABEL_SEARCH_DESCRIPTION = "Label name to search for";
    public const string CMD_LABEL_YEAR_DESCRIPTION = "Year to find releases for";

    public async Task LabelCommand(string labelName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        labelName = labelName.Replace("\"", "");
        var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Album, $"label:\"{labelName}\""));

        var albums = new List<FullAlbum>();
        var sb = new StringBuilder();

        await foreach (var album in api.Paginate(response.Albums, s => s.Albums))
        {
            var cacheResult = await GetAlbum(album.Id);

            if (labelName.Contains("mau5trap", StringComparison.OrdinalIgnoreCase) && !labelName.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase) && cacheResult.Label.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase))
                continue;

            albums.Add(cacheResult);
            if (albums.Count == 2000)
                break;
        }

        foreach (var album in albums.OrderByDescending(a => a.ReleaseDate))
        {
            var line = $"{string.Join(" & ", album.Artists.Select(a => a.Name))} - {album.Name} ({album.ReleaseDate})";
            sb.AppendLine(line);
        }

        if (sb.Length < 1)
        {
            await context.ErrorAsync("pissed my pant");
            return;
        }

        var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        await context.FollowupWithFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums");
        //await Context.Channel.SendFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums");
    }

    public async Task LabelCommand(string labelName, int year, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        labelName = labelName.Replace("\"", "");
        var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Album, $"label:\"{labelName}\" year:{year}"));

        var albums = new Dictionary<string, SimpleAlbum>();
        var sb = new StringBuilder();
        var missing = 0;

        await foreach (var album in api.Paginate(response.Albums, s => s.Albums))
        {
            if (string.IsNullOrWhiteSpace(album?.Id))
            {
                await context.ErrorAsync("Cannot obtain an album");
                continue;
            }

            var cacheResult = await GetAlbum(album.Id);

            if (labelName.Contains("mau5trap", StringComparison.OrdinalIgnoreCase) && !labelName.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase) && cacheResult.Label.Contains("mmj mau5trap", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!albums.TryAdd(album.Id, album))
                missing++;
            
            if (albums.Count == 2000)
                break;
        }

        foreach (var kvp in albums.OrderByDescending(a => a.Value.ReleaseDate))
        {
            var album = kvp.Value;
            var line = $"{string.Join(" & ", album.Artists.Select(a => a.Name))} - {album.Name} ({album.ReleaseDate})";
            sb.AppendLine(line);
        }

        if (missing > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{missing} releases may be missing");
        }

        if (sb.Length < 1)
        {
            await context.ErrorAsync("pissed my pant");
            return;
        }

        var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        await context.FollowupWithFileAsync(writer, $"{labelName}-{year}.txt", $"I found {albums.Count} albums");
        //await Context.Channel.SendFileAsync(writer, $"{labelName}-{year}.txt", $"I found {albums.Count} albums");
    }

#endregion


#region Artist

    public const string CMD_ARTIST_NAME = "artist";
    public const string CMD_ARTIST_DESCRIPTION = "Get all releases from an artist";
    public const string CMD_ARTIST_SEARCH_DESCRIPTION = "Artist to search for";

    public async Task ArtistCommand(string artistName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        artistName = artistName.Replace("\"", "");

        var paging = await api.Artists.GetAlbums(artistName);

        var albums = await api.PaginateAll(paging);

        var sb = new StringBuilder();

        /*var playlist =await CreateOrUpdatePlaylist(labelName, albums.OrderByDescending(a => a.ReleaseDate)
            .ToArray());*/

        foreach (var album in albums.OrderBy(a => a.Name).ThenByDescending(a => a.ReleaseDate).ThenBy(a => string.Join(" & ", a.Artists.Select(_ => _.Name))))
        {
            var line = $"{string.Join(" & ", album.Artists.Select(a => $"{a.Name}{(a.Type != "artist" ? $" ({a.Type})" : "")}"))} - {album.Name} ({album.ReleaseDate}) https://open.spotify.com/album/{album.Id}";
            sb.AppendLine(line);
        }

        if (sb.Length < 1)
        {
            await context.ErrorAsync("pissed my pant");
            return;
        }

        //  await Context.Message.ReplyAsync($"{playlist.Uri}");
        if (sb.Length > 2000)
        {
            var writer = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
            await context.FollowupWithFileAsync(writer, $"{artistName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            //await Context.Channel.SendFileAsync(writer, $"{artistName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
        }
        else
        {
            await context.FollowupAsync(sb.ToString());
        }
    }

#endregion


#region Peep

    public const string CMD_PEEP_NAME = "peep";
    public const string CMD_PEEPN_NAME = "peepn";
    public const string CMD_PEEP_DESCRIPTION = "todo";
    public const string CMD_PEEPN_DESCRIPTION = "todo";
    public const string CMD_PEEP_SEARCH_DESCRIPTION = "todo";

    public async Task PeepCommand(string labelName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        labelName = labelName.Replace("\"", "");
        var notFound = await SpotifyUtils.Peep(api, labelName);

        var sb = new StringBuilder();

        foreach (var artist in notFound)
        {
            sb.AppendLine($"`{artist.Name}` <https://open.spotify.com/artist/{artist.Id}>");
        }

        if (sb.Length > 0)
        {
            await context.SendOrAttachment(sb.ToString());
        }
        else
        {
            await context.ErrorAsync($"couldnt find anything for {labelName}");
        }
    }

    public async Task PeepNoDoubleCheckCommand(string labelName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        labelName = labelName.Replace("\"", "");
        var response = await api.Search.Item(new SearchRequest(SearchRequest.Types.Artist | SearchRequest.Types.Track, $"label:\"{labelName}\""));

        var searchedArtists = new HashSet<FullArtist>(new FullArtistComparer());
        var trackArtists = new HashSet<SimpleArtist>(new SimpleArtistComparer());

        await foreach (var artist in api.Paginate(response.Artists, s => s.Artists, new CachingPaginator()))
        {
            searchedArtists.Add(artist);
            if (searchedArtists.Count == 2000)
                break;
        }

        IUserMessage? message = null;
        var count = 0;

        await foreach (var track in api.Paginate(response.Tracks, s => s.Tracks))
        {
            var artists = track.Artists.Select(a => a).ToArray();

            foreach (var artist in artists)
            {
                trackArtists.Add(artist);
            }

            if (++count == 2000)
            {
                await context.ErrorAsync("too many tracks");
                break;
            }

            if (count % 250 == 0)
            {
                message = await context.UpdateOrSend(message, $"{count} tracks");
            }
        }

        if (message is null)
        {
            await context.FollowupAsync($"Checking {searchedArtists.Count} artists & {trackArtists.Count} artists from every track");
        }
        else
        {
            await message.ModifyAsync(m => m.Content = $"Checking {searchedArtists.Count} artists & {trackArtists.Count} artists from every track");
        }

        var notFound = searchedArtists.Where(searchedArtist => trackArtists.FirstOrDefault(trackArtist => string.Equals(trackArtist.Name, searchedArtist.Name, StringComparison.OrdinalIgnoreCase)) is null).ToArray();

        var sb = new StringBuilder();

        foreach (var artist in notFound)
        {
            sb.AppendLine($"`{artist.Name}` <https://open.spotify.com/artist/{artist.Id}>");
        }

        if (sb.Length > 0)
        {
            await context.SendOrAttachment(sb.ToString());
        }
        else
        {
            await context.FollowupAsync($"couldnt find anything for {labelName}");
        }
    }

#endregion
}