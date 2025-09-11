using System.Collections.Generic;
using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceLogin
{
    [JsonProperty("application_id")]
    public int ApplicationId { get; set; } = default!;

    [JsonProperty("user_id")]
    public int UserId { get; set; } = default!;

    [JsonProperty("username")]
    public string Username { get; set; } = default!;

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = default!;

    [JsonProperty("last_name")]
    public string LastName { get; set; } = default!;

    [JsonProperty("scope"), JsonRequired]
    public string Scope { get; set; } = default!; // array?

    [JsonProperty("feature")]
    public List<string> Features { get; set; } = default!;

    [JsonProperty("subscription")]
    public string? Subscription { get; set; }

    [JsonProperty("person_id")]
    public int PersonId { get; set; } = default!;

    [JsonProperty("access_token"), JsonRequired]
    public string AccessToken { get; set; } = default!;

    [JsonProperty("expires_in"), JsonRequired]
    public int ExpiresIn { get; set; } = default!;

    [JsonProperty("token_type"), JsonRequired]
    public string TokenType { get; set; } = default!;

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = default!;

    [JsonProperty("expires_at"), JsonRequired]
    public long ExpiresAt { get; set; } = default!;

    [JsonProperty("expires_in_ms"), JsonRequired]
    public int ExpiresInMs { get; set; } = default!;

    [JsonProperty("timestamp"), JsonRequired]
    public long Timestamp { get; set; } = default!;
}