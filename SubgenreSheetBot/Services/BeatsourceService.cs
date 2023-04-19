using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using BeatportApi.Beatsource;
using Common;
using Common.Beatport;
using Discord;
using Raven.Client;

namespace SubgenreSheetBot.Services;

public class BeatsourceService
{
    private readonly Beatsource api;

    public BeatsourceService()
    {
        if (api != null)
            throw new Exception("API already initialized");

        api = new Beatsource();
        api.Login(File.ReadAllText("beatsource_user"), File.ReadAllText("beatsource_pass")).GetAwaiter().GetResult();
    }

    private Task<BeatsourceRelease?> GetAlbum(int albumId)
    {
        using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
        return BeatportDbUtils.GetAlbumOrCache(api, session, albumId);
    }

    private Task<BeatsourceTrack[]> GetTracks(BeatsourceRelease album)
    {
        using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
        return album.GetTracksOrCache(api, session);
    }

    private async Task<List<BeatsourceRelease>> GetAlbums(int labelId)
    {
        var releases = new List<BeatsourceRelease>();
        var response = await api.GetReleasesByLabelId(labelId, 200);
        return await GetAlbums(labelId, releases, response);
    }

    private async Task<List<BeatsourceRelease>> GetAlbums(int labelId, List<BeatsourceRelease> releases, BeatsourceResponse<BeatsourceRelease> response)
    {
        if (response.Results == null || response.Results.Count < 1)
            return releases;

        await Task.WhenAll(response.Results.Select(async release =>
        {
            using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
            var realRelease = await BeatportDbUtils.GetAlbumOrCache(api, session, release.Id);
            if (realRelease == null)
                return;

            if (realRelease.TrackUrls == null || realRelease.TrackUrls.Length < 1)
            {
                //Log($"ERROR {release.Id} {release.Name}: no track urls");
                return;
            }

            session.SaveChanges();
            releases.Add(realRelease);
        }));

        if (response.Next != null)
        {
            var url = new Uri($"https://{response.Next}");
            var query = HttpUtility.ParseQueryString(url.Query);
            var page = query.Get("page");

            if (!int.TryParse(page, out var realPage))
            {
                //Log($"ERROR: no page query {labelReleases.Next}");
            }

            response = await api.GetReleasesByLabelId(labelId, 200, realPage);
            return await GetAlbums(labelId, releases, response);
        }

        return releases;
    }

#region Tracks

    public const string CMD_TRACKS_NAME = "tracks";
    public const string CMD_TRACKS_DESCRIPTION = "CSV with tracks from a Beatsource album";
    public const string CMD_TRACKS_SEARCH_DESCRIPTION = "Beatsource album url";

    public async Task TracksCommand(string albumUrl, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        var idResult = BeatportUtils.GetIdFromUrl(albumUrl);

        if (!string.IsNullOrWhiteSpace(idResult.Error))
        {
            await context.ErrorAsync($"{idResult.Error}");
            return;
        }

        var album = await GetAlbum(idResult.Id);
        var albumArtists = album.Artists;

        if (albumArtists.Count == 0)
        {
            await context.ErrorAsync("the artist count is 0");
            return;
        }

        var embed = new EmbedBuilder().WithTitle($"{album.ArtistConcat} - {album.Name}").WithThumbnailUrl(album.Image.DynamicUri.Replace("{w}", "1400").Replace("{h}", "1400"));
        var sb = new StringBuilder();

        var tracks = await GetTracks(album);

        if (!tracks.Any())
        {
            await context.ErrorAsync("no tracks");
            return;
        }

        //2009-09-22	House	Tech House | Progressive House	deadmau5	Lack of a Better Name	mau5trap	8:15	FALSE	128	FALSE	F min
        foreach (var track in tracks)
        {
            sb.AppendLine($"`{album.NewReleaseDate},?,?,{track.ArtistConcat},{track.Name},{album.Label.Name},{TimeSpan.FromMilliseconds(track.LengthMs):m':'ss}`");
        }

        await context.FollowupAsync(sb.ToString(), embed: embed.Build());
    }

#endregion

#region Album

    public const string CMD_ALBUM_NAME = "album";
    public const string CMD_ALBUM_DESCRIPTION = "Information embed for Beatsource albums";
    public const string CMD_ALBUM_SEARCH_DESCRIPTION = "Beatsource album url";

    public async Task AlbumCommand(string albumUrl, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        var idResult = BeatportUtils.GetIdFromUrl(albumUrl);

        if (!string.IsNullOrWhiteSpace(idResult.Error))
        {
            await context.ErrorAsync($"{idResult.Error}");
            return;
        }

        var album = await GetAlbum(idResult.Id);

        if (album is null)
        {
            await context.ErrorAsync($"The album could not be loaded");
            return;
        }

        var tracks = await GetTracks(album);

        if (tracks.Length < 1)
        {
            await context.ErrorAsync("no tracks");
            return;
        }

        using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();

        var (embed, file) = AlbumEmbed.EmbedBuilder(GenericAlbum.FromAlbum(album, tracks.ToList()));

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

#region ISRC

    public const string CMD_ISRC_NAME = "isrc";
    public const string CMD_ISRC_DESCRIPTION = "Information embed with tracks matching cached ISRCs";
    public const string CMD_ISRC_SEARCH_DESCRIPTION = "ISRC to search for";

    public async Task IsrcCommand(string isrc, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        if (string.IsNullOrWhiteSpace(isrc))
        {
            //todo: reply
            return;
        }

        using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
        var results = session.Query<BeatsourceTrack>("TrackIsrc")
                             .Search(t => t.Isrc, isrc)
                             //.Where(t => t.Isrc == isrc)
                             .ToArray();

        if (results.Length == 0)
        {
            await context.ErrorAsync($"found no tracks for isrc {isrc}");
            return;
        }

        var genres = new List<string>();

        var sb1 = new StringBuilder();
        var sb2 = new StringBuilder();
        var sb3 = new StringBuilder();

        //2009-09-22	House	Tech House | Progressive House	deadmau5	Lack of a Better Name	mau5trap	8:15	FALSE	128	FALSE	F min
        foreach (var track in results)
        {
            var line = $"{track.ArtistConcat} - {track.Name} <https://www.beatsource.com/track/{track.Slug}/{track.Id}>";

            if (track.Subgenre != null)
            {
                if (!genres.Contains(track.Subgenre.Name))
                    genres.Add(track.Subgenre.Name);
            }
            else
            {
                if (!genres.Contains(track.Genre.Name))
                    genres.Add(track.Genre.Name);
            }

            if (sb2.Length + line.Length >= 1023)
            {
                sb3.AppendLine(line);
            }
            else if (sb1.Length + line.Length >= 1023)
            {
                sb2.AppendLine(line);
            }
            else
            {
                sb1.AppendLine(line);
            }
        }

        var embed = new EmbedBuilder().WithTitle($"{isrc}").WithThumbnailUrl(results[new Random().Next(0, results.Length)].Release.Image.DynamicUri.Replace("{w}", "1400").Replace("{h}", "1400"))
            /*.AddField("Release Date", album.NewReleaseDate.ToString("yyyy-MM-dd"), true)
            .AddField("Type", album.Type?.Name ?? "None", true)*/;

        embed = embed /*.AddField("Label", album.Label.Name, true)*/
            .AddField("Tracklist", sb1.ToString());

        if (sb2.Length > 0)
            embed = embed.AddField("Tracklist (cont.)", sb2.ToString());
        if (sb3.Length > 0)
            embed = embed.AddField("Tracklist (cont. again)", sb3.ToString());

        await context.FollowupAsync(embed: embed.Build());
    }

#endregion

#region Label

    public const string CMD_LABEL_NAME = "label";
    public const string CMD_LABEL_DESCRIPTION = "Text file with all Beatsource releases from a label";
    public const string CMD_LABEL_SEARCH_DESCRIPTION = "Beatsource label url";

    public async Task LabelCommand(string labelName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        var idResult = BeatportUtils.GetIdFromUrl(labelName);

        if (!string.IsNullOrWhiteSpace(idResult.Error))
        {
            await context.ErrorAsync(idResult.Error);
            return;
        }

        var albums = await GetAlbums(idResult.Id);

        var sb = new StringBuilder();

        foreach (var album in albums.OrderByDescending(a => a.NewReleaseDate))
        {
            var line = $"{album.ArtistConcat} - {album.Name} ({album.CatalogNumber} {album.NewReleaseDate:yyyy-MM-dd})";
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
            await context.FollowupWithFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            //await Context.Channel.SendFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
        }
        else
        {
            await context.FollowupAsync(sb.ToString());
        }
    }

#endregion

#region Label Cached (merge)

    public const string CMD_LABEL_CACHED_NAME = "labelcached";
    public const string CMD_LABEL_CACHED_DESCRIPTION = "Text file with all cached Beatsource releases from a label";
    public const string CMD_LABEL_CACHED_SEARCH_DESCRIPTION = "Label name to search for";

    public async Task LabelCachedCommand(string labelName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        using var session = SubgenreSheetBot.BeatsourceStore.OpenSession();
        var query = session.Advanced.Stream(session.Query<BeatsourceRelease>("ReleaseByLabel"));

        var albums = new List<BeatsourceRelease>();
        while (query.MoveNext())
        {
            if (query.Current is null)
                throw new Exception("Item not loaded");

            var album = query.Current.Document;
            if (!album.Label.Name.StartsWith(labelName, StringComparison.OrdinalIgnoreCase))
                continue;

            albums.Add(album);
        }

        var sb = new StringBuilder();

        foreach (var album in albums.OrderByDescending(a => a.NewReleaseDate))
        {
            var line = $"{album.ArtistConcat} - {album.Name} ({album.CatalogNumber} {album.NewReleaseDate:yyyy-MM-dd})";
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
            await context.FollowupWithFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
            //await Context.Channel.SendFileAsync(writer, $"{labelName}.txt", $"I found {albums.Count} albums which does not fit in a discord message");
        }
        else
        {
            await context.FollowupAsync(sb.ToString());
        }
    }

#endregion
}