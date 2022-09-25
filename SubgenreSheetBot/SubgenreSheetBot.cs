using Discord.WebSocket;
using XDiscordBotLib.Database;
using XDiscordBotLib.Utils;

namespace SubgenreSheetBot
{
    public class SubgenreSheetBot : Bot
    {
        public static readonly DataStore BeatportStore = new("beatport");
        public static readonly DataStore BeatsourceStore = new("beatsource");
        public static readonly DataStore SpotifyStore = new("spotify");
        public static readonly DataStore AppleMusicStore = new("apple-music");
        public static readonly DataStore MonstercatStore = new("monstercat");

        public SubgenreSheetBot(string token) : base(token)
        {
            BeatportStore.GetStore()
                .Conventions.MaxNumberOfRequestsPerSession = 200;
        }

        public SubgenreSheetBot(string token, string commandPrefix) : base(token, commandPrefix)
        {
            BeatportStore.GetStore()
                .Conventions.MaxNumberOfRequestsPerSession = 200;
        }

        public SubgenreSheetBot(string token, string commandPrefix, DiscordSocketConfig socketConfig) : base(token, commandPrefix, socketConfig)
        {
            BeatportStore.GetStore()
                .Conventions.MaxNumberOfRequestsPerSession = 200;
        }
    }
}