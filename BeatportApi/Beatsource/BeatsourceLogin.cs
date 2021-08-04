using System.Collections.Generic;
using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourceLogin
    {
        [JsonProperty("application_id"), JsonRequired]
        public int ApplicationId { get; set; }

        [JsonProperty("user_id"), JsonRequired]
        public int UserId { get; set; }

        [JsonProperty("username"), JsonRequired]
        public string Username { get; set; }

        [JsonProperty("first_name"), JsonRequired]
        public string FirstName { get; set; }

        [JsonProperty("last_name"), JsonRequired]
        public string LastName { get; set; }

        [JsonProperty("scope"), JsonRequired]
        public string Scope { get; set; } // array?

        [JsonProperty("feature"), JsonRequired]
        public List<string> Features { get; set; }

        [JsonProperty("subscription")]
        public string Subscription { get; set; }

        [JsonProperty("person_id"), JsonRequired]
        public int PersonId { get; set; }

        [JsonProperty("access_token"), JsonRequired]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in"), JsonRequired]
        public int ExpiresIn { get; set; }

        [JsonProperty("token_type"), JsonRequired]
        public string TokenType { get; set; }

        [JsonProperty("refresh_token"), JsonRequired]
        public string RefreshToken { get; set; }

        [JsonProperty("expires_at"), JsonRequired]
        public long ExpiresAt { get; set; }

        [JsonProperty("expires_in_ms"), JsonRequired]
        public int ExpiresInMs { get; set; }

        [JsonProperty("timestamp"), JsonRequired]
        public long Timestamp { get; set; }
    }
}