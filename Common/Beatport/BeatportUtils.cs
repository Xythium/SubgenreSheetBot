using System;
using System.Linq;
using System.Threading.Tasks;
using BeatportApi;
using Discord;
using Raven.Client;
using Serilog;

namespace Common.Beatport;

public static class BeatportUtils
{
    public static IdFromUrlResult GetIdFromUrl(Uri uri)
    {
        if (!string.Equals(uri.Host, "www.beatport.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "api.beatport.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "www.beatsource.com", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "api.beatsource.com", StringComparison.OrdinalIgnoreCase))
        {
            if (!uri.Host.Contains("beatport.com") && !uri.Host.Contains("beatsource.com"))
            {
                return new IdFromUrlResult
                {
                    Error = $"Host '{uri.Host}' is not beatport.com"
                };
            }
            /*else
                await Context.Message.ReplyAsync($"ERROR: wtf <{url}> '<{uri.Host}>'");*/ //bug???
        }

        var directories = uri.LocalPath.Split(new[]
        {
            "/"
        }, StringSplitOptions.RemoveEmptyEntries);

        var str = directories.Last();

        if (!int.TryParse(str, out var id))
        {
            return new IdFromUrlResult
            {
                Error = $"Id '{id}' is not numeric"
            };
        }

        return new IdFromUrlResult
        {
            Id = id
        };
    }

    public static IdFromUrlResult GetIdFromUrl(string url) { return GetIdFromUrl(new Uri(url)); }
}