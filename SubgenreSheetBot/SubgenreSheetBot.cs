using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using SubgenreSheetBot.Services;
using XDiscordBotLib.Database;
using XDiscordBotLib.Utils;

namespace SubgenreSheetBot;

public class SubgenreSheetBot : Bot
{
    public static readonly DataStore BeatportStore = new("beatport");
    public static readonly DataStore BeatsourceStore = new("beatsource");
    public static readonly DataStore SpotifyStore = new("spotify");
    public static readonly DataStore AppleMusicStore = new("apple-music");
    public static readonly DataStore MonstercatStore = new("monstercat");

    public SubgenreSheetBot(string token) : base(token)
    {
    }

    public SubgenreSheetBot(string token, string commandPrefix) : base(token, commandPrefix)
    {
    }

    public SubgenreSheetBot(string token, string commandPrefix, DiscordSocketConfig socketConfig) : base(token, commandPrefix, socketConfig)
    {
    }

    protected override void setupServices()
    {
        BeatportStore.GetStore().Conventions.MaxNumberOfRequestsPerSession = 200;
        serviceCollection = serviceCollection
            .AddSingleton<BeatportService>()
            .AddSingleton<AppleMusicService>()
            .AddSingleton<BeatsourceService>()
            .AddSingleton<SpotifyService>()
            .AddSingleton<SheetService>();
        base.setupServices();
    }
}