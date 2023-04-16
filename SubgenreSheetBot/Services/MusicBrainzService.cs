using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using MetaBrainz.MusicBrainz;

namespace SubgenreSheetBot.Services;

public class MusicBrainzService
{
    private const string MUSICBRAINZ_LIFETIME_FILE = "musicbrainz_lifetime";
    private const string MUSICBRAINZ_REFRESH_FILE = "musicbrainz_refresh";
    private const string MUSICBRAINZ_ACCESS_FILE = "musicbrainz_access";
    private const string MUSICBRAINZ_SECRET_FILE = "musicbrainz_clientsecret";
    private const string MUSICBRAINZ_AUTH_FILE = "musicbrainz_auth";

    public static string CLIENT_ID = File.ReadAllText("musicbrainz_clientid");

    public Query GetQuery()
    {
        return GetQuery(Assembly.GetExecutingAssembly().GetName());
    }

    private static Query GetQuery(AssemblyName assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly.Name))
            throw new Exception("Assembly name is null");

        var query = new Query(assembly.Name, assembly.Version, new Uri("mailto:xythium@gmail.com"));
        var oauth = new OAuth2
        {
            ClientId = CLIENT_ID
        };

        var lifetimeFile = new FileInfo(MUSICBRAINZ_LIFETIME_FILE);

        if (lifetimeFile.Exists)
        {
            var lifetimeStr = File.ReadAllText(lifetimeFile.FullName);
            var lifetime = DateTimeOffset.Parse(lifetimeStr);

            if (DateTime.UtcNow >= lifetime)
            {
                Console.WriteLine("Token lifetime has expired");

                var at = oauth.RefreshBearerToken(File.ReadAllText(MUSICBRAINZ_REFRESH_FILE), File.ReadAllText(MUSICBRAINZ_SECRET_FILE));
                File.WriteAllText(MUSICBRAINZ_REFRESH_FILE, at.RefreshToken, Encoding.UTF8);
                File.WriteAllText(MUSICBRAINZ_ACCESS_FILE, at.AccessToken, Encoding.UTF8);
                File.WriteAllText(MUSICBRAINZ_LIFETIME_FILE, DateTimeOffset.UtcNow.AddSeconds(at.Lifetime).ToString("R"), Encoding.UTF8);
                File.WriteAllText(MUSICBRAINZ_LIFETIME_FILE + "2", at.Lifetime.ToString(), Encoding.UTF8);
                query.BearerToken = at.AccessToken;

                Console.WriteLine("Refreshed token");
                return query;
            }

            var accessTokenFile = new FileInfo(MUSICBRAINZ_ACCESS_FILE);

            if (accessTokenFile.Exists)
            {
                var accessToken = File.ReadAllText(accessTokenFile.FullName);

                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    query.BearerToken = accessToken;
                    return query;
                }
            }
        }

        return NewToken(query, oauth);
    }

    private static Query NewToken(Query query, OAuth2 oauth)
    {
        var url = oauth.CreateAuthorizationRequest(OAuth2.OutOfBandUri, AuthorizationScope.Everything);
        Process.Start($"C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe \"{url}\"");

        var authToken = Console.ReadLine();
        File.WriteAllText(MUSICBRAINZ_AUTH_FILE, authToken, Encoding.UTF8);

        if (string.IsNullOrWhiteSpace(authToken))
            throw new InvalidOperationException("No auth token");

        var at = oauth.GetBearerToken(authToken, File.ReadAllText(MUSICBRAINZ_SECRET_FILE), OAuth2.OutOfBandUri);
        File.WriteAllText(MUSICBRAINZ_REFRESH_FILE, at.RefreshToken, Encoding.UTF8);
        File.WriteAllText(MUSICBRAINZ_ACCESS_FILE, at.AccessToken, Encoding.UTF8);
        File.WriteAllText(MUSICBRAINZ_LIFETIME_FILE, DateTimeOffset.UtcNow.AddSeconds(at.Lifetime).ToString("R"), Encoding.UTF8);
        query.BearerToken = at.AccessToken;
        return query;
    }
}