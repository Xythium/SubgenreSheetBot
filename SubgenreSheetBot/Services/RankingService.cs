using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.Spotify;
using Discord;
using Microsoft.Data.Sqlite;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Services;

public class RankingService
{
    private MusicBrainzService musicBrainzService;
    private SpotifyService spotifyService;

    public RankingService(MusicBrainzService musicBrainzService, SpotifyService spotifyService)
    {
        this.musicBrainzService = musicBrainzService;
        this.spotifyService = spotifyService;
    }

    private static SqliteConnection GetConnection()
    {
        return new SqliteConnection("Data Source=subgenresheetbot.sqlite;Mode=ReadWrite");
    }


    public async Task TestCommand(DynamicContext context, bool ephemeral, RequestOptions requestOptions)
    {
        await context.DeferAsync(ephemeral, requestOptions);

        using var connection = new SqliteConnection("Data Source=subgenresheetbot.sqlite;Mode=ReadWrite");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from Tracks";

        var num = (long?)command.ExecuteScalar();

        await context.FollowupAsync($"num tracks: {num}");
    }

    public async Task AddFromCommand(string url, DynamicContext context, bool ephemeral, RequestOptions requestOptions)
    {
        await context.DeferAsync(ephemeral, requestOptions);

        if (url.Contains("open.spotify.com/album"))
        {
            var (_, albumId) = SpotifyUtils.GetIdFromUrl(url);
            await AddFromSpotifyAlbum(albumId.Split('/').Last(), context, ephemeral, requestOptions);
            return;
        }
    }

    public async Task RandomCommand(DynamicContext context, bool ephemeral, RequestOptions requestOptions)
    {
        await context.DeferAsync(ephemeral, requestOptions);

        using var connection = GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
                              with recursive ranking as (select distinct 0 as FromNode, tr.FromNode as ToNode, 1 as Depth
                                                         from TrackRanking tr
                                                         where not exists (select 1 from TrackRanking tr2 where tr2.ToNode = tr.FromNode)
                                                         union all
                                                         select tr.FromNode, tr.ToNode, Depth + 1
                                                         from ranking r
                                                                  join TrackRanking tr on tr.FromNode = r.ToNode)
                              select TrackId, Title from Tracks
                              left join ranking r on r.ToNode = TrackId
                              order by Depth, random() limit 2
                              """;
        command.CommandTimeout = 5;

        using var reader = command.ExecuteReader();

        var leastRanked = new List<(int, string)>();

        while (reader.Read())
            leastRanked.Add((reader.GetInt32(0), reader.GetString(1)));

        var sb = new StringBuilder($"Here are random tracks that you have ranked the least:");
        sb.AppendLine();

        for (var i = 0; i < leastRanked.Count; i++)
        {
            var track = leastRanked[i];
            sb.AppendLine($"* [{track.Item1}] {track.Item2}");
        }


        await context.FollowupAsync(sb.ToString());
    }

    private async Task AddFromSpotifyAlbum(string externalId, DynamicContext context, bool ephemeral, RequestOptions requestOptions)
    {
        var album = await spotifyService.GetAlbum(externalId);

        var albumId = AddIfNotExists(album, externalId) ?? GetAlbum(externalId) ?? throw new Exception("failed to get album");
        AddExternalAlbumId(albumId, externalId);
        AddIfNotExists(album.Artists);
        AddArtistsTo(albumId, album.Artists);

        var tracks = await spotifyService.GetTracks(album);
        AddIfNotExists(tracks);
        AddIfNotExists(tracks.SelectMany(t => t.Artists).ToList());
        AddArtistsTo(tracks);

        AddTo(albumId, tracks.Select(t => t.Id.Split('/').Last()).ToList());

        await Tracklist(albumId, context, ephemeral);
    }


    private async Task Tracklist(int albumId, DynamicContext context, bool ephemeral)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select Title, ReleaseDate from Albums where AlbumId = @albumId";
        command.Parameters.AddWithValue("albumId", albumId);

        using var reader = command.ExecuteReader();
        reader.Read();

        var album = new GenericAlbum
        {
            Name = reader.GetString(0),
            ReleaseDate = reader.GetString(1),
            Tracks = new List<GenericTrack>(),
            Artists = new List<string>()
        };

        using var command2 = connection.CreateCommand();
        command2.CommandText = "select Name from Artists where ArtistId in (select ArtistId from AlbumsArtists where AlbumId = @albumId)";
        command2.Parameters.AddWithValue("albumId", albumId);

        using var reader2 = command2.ExecuteReader();
        while (reader2.Read())
            album.Artists.Add(reader2.GetString(0));

        using var command3 = connection.CreateCommand();
        command3.CommandText = "select Title from Tracks where TrackId in (select TrackId from AlbumsTracks where AlbumId = @albumId)";
        command3.Parameters.AddWithValue("albumId", albumId);

        using var reader3 = command3.ExecuteReader();
        while (reader3.Read())
            album.Tracks.Add(new GenericTrack
            {
                Name = reader3.GetString(0)
            });

        var (embed, _) = AlbumEmbed.EmbedBuilder(album);

        await context.FollowupAsync(embed: embed.Build(), ephemeral: ephemeral);
    }

    private int? AddIfNotExists(FullAlbum album, string externalId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "insert into Albums(Title, ReleaseDate) select @title, @date where not exists(select 1 from AlbumsExternal where Type = 'spotify' and ExternalId = @id) returning AlbumId";
        command.Parameters.AddWithValue("title", album.Name);
        command.Parameters.AddWithValue("id", externalId);
        command.Parameters.AddWithValue("date", album.ReleaseDate);
        return (int?)(long?)command.ExecuteScalar();
    }

    private int? GetAlbum(string externalId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select AlbumId from AlbumsExternal where ExternalId = @externalId";
        command.Parameters.AddWithValue("externalId", externalId);
        return (int?)(long?)command.ExecuteScalar();
    }

    private void AddExternalAlbumId(int albumId, string externalId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "insert or ignore into AlbumsExternal(AlbumId, Type, ExternalId) values (@id, 'spotify', @externalId)";
        command.Parameters.AddWithValue("id", albumId);
        command.Parameters.AddWithValue("externalId", externalId);

        var status = command.ExecuteNonQuery();
        /*if (status < 0)
            throw new Exception("external id not added");*/
    }

    private void AddTo(int albumId, List<string> trackIds)
    {
        using var connection = GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.CommandText = "insert or ignore into AlbumsTracks(AlbumId, TrackId) values (@albumId, (select TrackId from TracksExternal where Type = 'spotify' and ExternalId = @trackId))";

        var albumIdParameter = command.CreateParameter();
        albumIdParameter.ParameterName = "albumId";
        albumIdParameter.Value = albumId;
        command.Parameters.Add(albumIdParameter);

        var trackIdParameter = command.CreateParameter();
        trackIdParameter.ParameterName = "trackId";
        command.Parameters.Add(trackIdParameter);

        foreach (var trackId in trackIds)
        {
            trackIdParameter.Value = trackId;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void AddArtistsTo(int albumId, List<SimpleArtist> albumArtists)
    {
        using var connection = GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.CommandText = "insert or ignore into AlbumsArtists(AlbumId, ArtistId) values (@albumId, (select ArtistId from ArtistsExternal where Type = 'spotify' and ExternalId = @artistId))";

        var albumIdParameter = command.CreateParameter();
        albumIdParameter.ParameterName = "albumId";
        albumIdParameter.Value = albumId;
        command.Parameters.Add(albumIdParameter);

        var artistIdParameter = command.CreateParameter();
        artistIdParameter.ParameterName = "artistId";
        command.Parameters.Add(artistIdParameter);

        foreach (var artist in albumArtists)
        {

            artistIdParameter.Value = artist.Id.Split('/').Last();
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void AddArtistsTo(List<FullTrack> tracks)
    {
        using var connection = GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.CommandText = "insert or ignore into TracksArtists(TrackId, ArtistId) values ((select TrackId from TracksExternal where Type = 'spotify' and ExternalId = @trackId), (select ArtistId from ArtistsExternal where Type = 'spotify' and ExternalId = @artistId))";

        var trackIdParameter = command.CreateParameter();
        trackIdParameter.ParameterName = "trackId";
        command.Parameters.Add(trackIdParameter);

        var artistIdParameter = command.CreateParameter();
        artistIdParameter.ParameterName = "artistId";
        command.Parameters.Add(artistIdParameter);

        foreach (var track in tracks)
        {
            trackIdParameter.Value = track.Id.Split('/').Last();

            foreach (var artist in track.Artists)
            {
                artistIdParameter.Value = artist.Id.Split('/').Last();
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    private void AddIfNotExists(List<SimpleArtist> artists)
    {
        using var connection = GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var artistCommand = connection.CreateCommand();
        artistCommand.CommandText = "insert into Artists(Name) select @name where not exists(select 1 from ArtistsExternal where Type = 'spotify' and ExternalId = @externalId) returning ArtistId";

        var insertArtistName = artistCommand.CreateParameter();
        insertArtistName.ParameterName = "name";
        artistCommand.Parameters.Add(insertArtistName);

        var insertArtistId = artistCommand.CreateParameter();
        insertArtistId.ParameterName = "externalId";
        artistCommand.Parameters.Add(insertArtistId);

        using var externalCommand = connection.CreateCommand();
        externalCommand.CommandText = "insert or ignore into ArtistsExternal(ArtistId, Type, ExternalId) values (@id, 'spotify', @externalId)";

        var insertExternalArtistId = externalCommand.CreateParameter();
        insertExternalArtistId.ParameterName = "id";
        externalCommand.Parameters.Add(insertExternalArtistId);

        var insertExternalId = externalCommand.CreateParameter();
        insertExternalId.ParameterName = "externalId";
        externalCommand.Parameters.Add(insertExternalId);

        foreach (var artist in artists)
        {
            insertArtistName.Value = artist.Name;
            insertArtistId.Value = artist.Id;

            var inserted = (int?)(long?)artistCommand.ExecuteScalar();
            if (inserted is null)
                continue;

            insertExternalArtistId.Value = inserted;
            insertExternalId.Value = artist.Id;

            externalCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private List<int> AddIfNotExists(List<FullTrack> tracks)
    {
        using var connection = GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var trackCommand = connection.CreateCommand();
        trackCommand.CommandText = "insert into Tracks(Title) select @title where not exists(select 1 from TracksExternal where Type = 'spotify' and ExternalId = @externalId) returning TrackId";

        var insertTitle = trackCommand.CreateParameter();
        insertTitle.ParameterName = "title";
        trackCommand.Parameters.Add(insertTitle);

        var insertTrackId = trackCommand.CreateParameter();
        insertTrackId.ParameterName = "externalId";
        trackCommand.Parameters.Add(insertTrackId);

        using var externalCommand = connection.CreateCommand();
        externalCommand.CommandText = "insert or ignore into TracksExternal(TrackId, Type, ExternalId) values (@id, 'spotify', @externalId)";

        var insertExternalTrackId = externalCommand.CreateParameter();
        insertExternalTrackId.ParameterName = "id";
        externalCommand.Parameters.Add(insertExternalTrackId);

        var insertExternalId = externalCommand.CreateParameter();
        insertExternalId.ParameterName = "externalId";
        externalCommand.Parameters.Add(insertExternalId);

        using var isrcCommand = connection.CreateCommand();
        isrcCommand.CommandText = "insert or ignore into TracksExternal(TrackId, Type, ExternalId) values (@id, 'isrc', @isrc)";

        var isrcTrackId = isrcCommand.CreateParameter();
        isrcTrackId.ParameterName = "id";
        isrcCommand.Parameters.Add(isrcTrackId);

        var isrcValue = isrcCommand.CreateParameter();
        isrcValue.ParameterName = "isrc";
        isrcCommand.Parameters.Add(isrcValue);

        var trackIds = new List<int>();

        foreach (var track in tracks)
        {
            var externalId = track.Id.Split('/').Last();
            insertTitle.Value = track.Name;
            insertTrackId.Value = externalId;

            var inserted = (int?)(long?)trackCommand.ExecuteScalar();
            if (inserted is null)
                continue;

            trackIds.Add(inserted.Value);

            insertExternalTrackId.Value = inserted;
            insertExternalId.Value = externalId;
            externalCommand.ExecuteNonQuery();

            var isrc = track.ExternalIds["isrc"];
            if (string.IsNullOrWhiteSpace(isrc))
                continue;
            isrc = isrc.Replace("-", "");

            isrcTrackId.Value = inserted;
            isrcValue.Value = isrc;
            isrcCommand.ExecuteNonQuery();
        }

        transaction.Commit();

        return trackIds;
    }
}