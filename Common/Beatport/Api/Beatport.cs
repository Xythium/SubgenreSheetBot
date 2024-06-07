using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BeatportApi.Beatsource;
using Newtonsoft.Json;
using RestSharp;
using Serilog;

namespace Common.Beatport.Api;

public class Beatport
{
    private readonly IBeatportClient client;

    public Beatport(IBeatportClient client)
    {
        this.client = client;
    }

    public Task<BeatportResponse<BeatportRelease>?> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1)
    {
        return client.GetReleasesByLabelId(labelId, itemsPerPage, page);
    }

    public Task<BeatportResponse<BeatportTrack>?> GetTracksByLabelId(int labelId, int itemsPerPage = 200, int page = 1)
    {
        return client.GetTracksByLabelId(labelId, itemsPerPage, page);
    }


    public Task<BeatportResponse<BeatportTrack>?> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
    {
        return client.GetTracksByReleaseId(releaseId, itemsPerPage, page);
    }

    public Task<BeatportRelease?> GetReleaseById(int releaseId)
    {
        return client.GetReleaseById(releaseId);
    }

    public Task<BeatportTrack?> GetTrackByTrackId(int trackId)
    {
        return client.GetTrackByTrackId(trackId);
    }
}

public interface IBeatportClient
{
    public Task<BeatportResponse<BeatportRelease>?> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1);


    public Task<BeatportResponse<BeatportTrack>?> GetTracksByLabelId(int labelId, int itemsPerPage = 200, int page = 1);


    public Task<BeatportResponse<BeatportTrack>?> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1);


    public Task<BeatportRelease?> GetReleaseById(int releaseId);


    public Task<BeatportTrack?> GetTrackByTrackId(int trackId);
}

public class BeatportClient : IBeatportClient
{
    private const string API_BASE = "https://api.beatport.com/v4";

    private static readonly JsonSerializerSettings serializerSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Error
    };

    private readonly RestClient client;
    private string username;
    private string password;
    private string bearerToken;
    private string refreshToken;
    private DateTime expires;

    public BeatportClient()
    {
        client = new RestClient(API_BASE);
    }

    public Task<BeatportResponse<BeatportRelease>?> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1)
    {
        return Request<BeatportResponse<BeatportRelease>?>($"catalog/releases/?label_id={labelId}&per_page={itemsPerPage}&page={page}", $"label{labelId}");
    }

    public Task<BeatportResponse<BeatportTrack>?> GetTracksByLabelId(int labelId, int itemsPerPage = 200, int page = 1)
    {
        return Request<BeatportResponse<BeatportTrack>?>($"catalog/tracks/?label_id={labelId}&per_page={itemsPerPage}&page={page}", $"label{labelId}");
    }

    public Task<BeatportResponse<BeatportTrack>?> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
    {
        return Request<BeatportResponse<BeatportTrack>?>($"catalog/releases/{releaseId}/tracks/?per_page={itemsPerPage}&page={page}", $"release{releaseId}");
    }

    public Task<BeatportRelease?> GetReleaseById(int releaseId)
    {
        return Request<BeatportRelease?>($"catalog/releases/{releaseId}/", $"release{releaseId}");
    }

    public Task<BeatportTrack?> GetTrackByTrackId(int trackId)
    {
        return Request<BeatportTrack?>($"catalog/tracks/{trackId}/", $"track{trackId}");
    }

    public async Task<BeatsourceLogin> Login(string user, string pass)
    {
        if (string.IsNullOrWhiteSpace(user))
            throw new Exception("No username provided");
        if (string.IsNullOrWhiteSpace(pass))
            throw new Exception("No password provided");
        username = user;
        password = pass;

        var request = new RestRequest("https://www.beatsource.com/api/auth/login", Method.Post);
        request.AddJsonBody(new
        {
            data = new
            {
                username,
                password
            }
        });
        var response = await client.ExecuteAsync(request);

        var result = Deserialize<BeatsourceLogin>(response.Content, $"login{user}");
        if (result.Value is null && result.Error != BeatportErrorType<BeatsourceLogin>.NOT_FOUND)
            throw new Exception(result.Error);

        var value = result.Value;

        if (value is null)
            throw new Exception("Beatsource login failed");

        if (value.TokenType != "Bearer")
            throw new InvalidDataException("Unknown token type");

        bearerToken = value.AccessToken;
        refreshToken = value.RefreshToken;
        expires = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(value.ExpiresAt);

        client.DefaultParameters.RemoveParameter("authorization");
        client.AddDefaultHeader("authorization", $"Bearer {bearerToken}");

        return value;
    }

    public async Task Refresh()
    {
        var request = new RestRequest("https://www.beatsource.com/api/auth/login", Method.Post);
        request.AddJsonBody(new
        {
            data = new
            {
                refreshToken,
                username,
                password
            }
        });
        var response = await client.ExecuteAsync(request);

        var result = Deserialize<BeatsourceLogin>(response.Content, $"refresh{username}");
        if (result.Value is null && result.Error != BeatportErrorType<BeatsourceLogin>.NOT_FOUND)
            throw new Exception(result.Error);

        var value = result.Value;

        if (value is null)
            throw new Exception("Beatsource login failed");

        if (value.TokenType != "Bearer")
            throw new InvalidDataException("Unknown token type");

        bearerToken = value.AccessToken;
        refreshToken = value.RefreshToken;
        expires = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(value.ExpiresAt);

        client.DefaultParameters.RemoveParameter("authorization");
        client.AddDefaultHeader("authorization", $"Bearer {bearerToken}");
    }

    private async Task<T> Request<T>(string resource, string errorIdentifier)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            throw new Exception("Not logged in");

        if (DateTime.UtcNow >= expires)
        {
            await Refresh();
        }

        var request = new RestRequest(resource);
        request.AddHeader("origin", "www.beatport.com");
        //request.AddHeader("Authorization", $"Bearer {bearerToken}");
        var response = await client.ExecuteAsync(request);

        var result = Deserialize<T>(response.Content, errorIdentifier);
        if (result.Value is null && result.Error != BeatportErrorType<T>.NOT_FOUND)
            throw new Exception(result.Error);

        return result.Value!;
    }

    private static BeatportErrorType<T?> Deserialize<T>(string? json, string identifier, [CallerMemberName]string memberName = "")
    {
        if (string.IsNullOrWhiteSpace(json))
            return new BeatportErrorType<T?>
            {
                Value = default,
                Error = BeatportErrorType<T>.NO_VALUE
            };

        if (!json.StartsWith("{") && !json.StartsWith("["))
            return new BeatportErrorType<T?>
            {
                Value = default,
                Error = BeatportErrorType<T>.NOT_JSON
            };

        try
        {
            //Log.Verbose("json: {json}", json);
            var res = JsonConvert.DeserializeObject<T>(json, serializerSettings);
            if (res is null)
                return new BeatportErrorType<T?>
                {
                    Value = default,
                    Error = $"Deserialization failed in {memberName}"
                };

            return new BeatportErrorType<T?>
            {
                Value = res
            };
        }
        catch (Exception ex)
        {
            BeatportError error;
            try
            {
                error = JsonConvert.DeserializeObject<BeatportError>(json);
                if (error is null)
                    return new BeatportErrorType<T?>
                    {
                        Value = default,
                        Error = $"Deserialization failed in {memberName}"
                    };
            }
            catch (Exception e)
            {
                File.WriteAllText($"fatalbeatport.{identifier}.txt", json);
                return new BeatportErrorType<T?>
                {
                    Value = default,
                    Error = $"Error parsing error {memberName} {identifier}: ```\r\n{json}\r\n```"
                };
            }

            switch (error.Detail)
            {
                case "Internal server error":
                    File.WriteAllText($"errorbeatport-internal.{identifier}.txt", json);
                    return new BeatportErrorType<T?>
                    {
                        Value = default,
                        Error = $"Internal Beatport in {memberName}: {ex}"
                    };

                case "Not found.":
                    File.WriteAllText($"errorbeatport-notfound.{identifier}.txt", json);
                    return new BeatportErrorType<T?>
                    {
                        Value = default,
                        Error = BeatportErrorType<T>.NOT_FOUND
                    };

                case "Territory Restricted.":
                    File.WriteAllText($"errorbeatport-territory.{identifier}.txt", json);
                    return new BeatportErrorType<T?>
                    {
                        Value = default,
                        Error = $"Territory restricted in {memberName}: {ex}"
                    };

                case "Authentication credentials were not provided.":
                    File.WriteAllText($"errorbeatport-creds.{identifier}.txt", json);
                    return new BeatportErrorType<T?>
                    {
                        Value = default,
                        Error = $"Authentication in {memberName}: {ex}"
                    };

                default:
                    File.WriteAllText($"errorbeatport.{identifier}.txt", json);
                    return new BeatportErrorType<T?>
                    {
                        Value = default,
                        Error = $"Unknown error in {memberName}: {ex}"
                    };
            }

        }

        return default;
    }
}

public class BeatportErrorType<T>
{
    public required T Value { get; init; }

    public string Error { get; set; }

    public const string NO_VALUE = "No value";
    public const string NOT_FOUND = "Not found";
    public const string NOT_JSON = "Value is not json";
    public const string DESERIALIZATION_FAILED = "Value is not json";
}

public class TestBeatportClient : IBeatportClient
{
    private readonly Dictionary<int, BeatportTrack> tracks;
    private readonly Dictionary<int, BeatportRelease> releases;

    public TestBeatportClient(Dictionary<int, BeatportTrack> tracks, Dictionary<int, BeatportRelease> releases)
    {
        this.tracks = tracks;
        this.releases = releases;
    }

    public Task<BeatportResponse<BeatportRelease>?> GetReleasesByLabelId(int labelId, int itemsPerPage = 200, int page = 1)
    {
        return Task.FromResult<BeatportResponse<BeatportRelease>?>(null);
    }

    public Task<BeatportResponse<BeatportTrack>?> GetTracksByLabelId(int labelId, int itemsPerPage = 200, int page = 1)
    {
        return Task.FromResult<BeatportResponse<BeatportTrack>?>(null);
    }

    public Task<BeatportResponse<BeatportTrack>?> GetTracksByReleaseId(int releaseId, int itemsPerPage = 200, int page = 1)
    {
        return Task.FromResult<BeatportResponse<BeatportTrack>?>(null);
    }

    public Task<BeatportRelease?> GetReleaseById(int releaseId)
    {
        if (releases.TryGetValue(releaseId, out var release))
            return Task.FromResult(release)!;
        throw new KeyNotFoundException($"Relesae with id {release} not found.");
    }

    public Task<BeatportTrack?> GetTrackByTrackId(int trackId)
    {
        if (tracks.TryGetValue(trackId, out var track))
            return Task.FromResult(track)!;
        throw new KeyNotFoundException($"Track with id {trackId} not found.");
    }
}