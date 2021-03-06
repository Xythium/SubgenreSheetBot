﻿using Discord.WebSocket;
using XDiscordBotLib.Database;
using XDiscordBotLib.Utils;

namespace SubgenreSheetBot
{
    public class SubgenreSheetBot : Bot
    {
        public static readonly DataStore BeatportStore = new DataStore("beatport");
        public static readonly DataStore BeatsourceStore = new DataStore("beatsource");

        public SubgenreSheetBot(string token) : base(token) { }

        public SubgenreSheetBot(string token, string commandPrefix) : base(token, commandPrefix) { }

        public SubgenreSheetBot(string token, string commandPrefix, DiscordSocketConfig socketConfig) : base(token, commandPrefix, socketConfig) { }
    }
}