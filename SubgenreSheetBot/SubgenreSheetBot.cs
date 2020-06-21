using Discord.WebSocket;
using XDiscordBotLib.Utils;

namespace SubgenreSheetBot
{
    public class SubgenreSheetBot : Bot
    {
        public SubgenreSheetBot(string token) : base(token)
        {
        }

        public SubgenreSheetBot(string token, string commandPrefix) : base(token, commandPrefix)
        {
        }

        public SubgenreSheetBot(string token, string commandPrefix, DiscordSocketConfig socketConfig) : base(token, commandPrefix, socketConfig)
        {
        }
    }
}