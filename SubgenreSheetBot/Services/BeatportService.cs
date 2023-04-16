using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using BeatportApi.Beatport;
using Common;
using Common.Beatport;
using Discord;
using Raven.Client;

namespace SubgenreSheetBot.Services;

public class BeatportService
{
    private readonly Beatport api;

    public BeatportService()
    {
        if (api != null) throw new Exception("API already initialized");

        api = new Beatport(File.ReadAllText("beatport_token"));
    }

    public Task<BeatportRelease?> GetAlbum(int albumId)
    {
        using var session = SubgenreSheetBot.BeatportStore.OpenSession();
        return BeatportDbUtils.GetAlbumOrCache(api, session, albumId);
    }

    public Task<BeatportTrack[]> GetTracks(BeatportRelease album)
    {
        using var session = SubgenreSheetBot.BeatportStore.OpenSession();
        return album.GetTracksOrCache(api, session);
    }

    public async Task<List<BeatportRelease>> GetAlbums(int labelId)
    {
        var releases = new List<BeatportRelease>();
        var response = await api.GetReleasesByLabelId(labelId, 200);
        return await GetAlbums(labelId, releases, response);
    }

    public async Task<List<BeatportRelease>> GetAlbums(int labelId, List<BeatportRelease> releases, BeatportResponse<BeatportRelease> response)
    {
        if (response.Results == null || response.Results.Count < 1)
            return releases;

        await Task.WhenAll(response.Results.Select(async release =>
        {
            using var session = SubgenreSheetBot.BeatportStore.OpenSession();
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

        var embed = new EmbedBuilder().WithTitle($"{album.ArtistConcat} - {album.Name}")
            .WithThumbnailUrl(album.Image.DynamicUri.Replace("{w}", "1400")
                .Replace("{h}", "1400"));
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

        using var session = SubgenreSheetBot.BeatportStore.OpenSession();

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

    public async Task IsrcCommand(string isrc, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        if (string.IsNullOrWhiteSpace(isrc))
        {
            //todo: reply
            return;
        }

        using var session = SubgenreSheetBot.BeatportStore.OpenSession();
        var results = session.Query<BeatportTrack>("TrackIsrc")
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
            var line = $"{track.ArtistConcat} - {track.Name} <https://www.beatport.com/track/{track.Slug}/{track.Id}>";

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

        var embed = new EmbedBuilder().WithTitle($"{isrc}")
                .WithThumbnailUrl(results[new Random().Next(0, results.Length)]
                    .Release.Image.DynamicUri.Replace("{w}", "1400")
                    .Replace("{h}", "1400"))
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

    public async Task LabelCachedCommand(string labelName, DynamicContext context, bool ephemeral, RequestOptions options)
    {
        await context.DeferAsync(ephemeral, options);

        using var session = SubgenreSheetBot.BeatportStore.OpenSession();
        var query = session.Advanced.Stream(session.Query<BeatportRelease>("ReleaseByLabel"));

        var albums = new List<BeatportRelease>();
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
}