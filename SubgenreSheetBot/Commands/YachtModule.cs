using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Yacht;

namespace SubgenreSheetBot.Commands
{
    [Group("yacht"), Alias("y")]
    public partial class YachtModule : ModuleBase
    {
        private static readonly List<YachtGame> games = new List<YachtGame>();

        [Command("start")]
        public async Task Start(params IUser[] players)
        {
            var actualPlayers = players.ToList();

            if (!actualPlayers.Contains(Context.User))
                actualPlayers.Add(Context.User);
            if (!actualPlayers.Contains(Context.Client.CurrentUser))
                actualPlayers.Add(Context.Client.CurrentUser);

            var game = games.FirstOrDefault(g => g.Players.SequenceEqual(actualPlayers));

            if (game != null)
            {
                await Context.Message.ReplyAsync($"There is already a game between players {string.Join(" vs ", actualPlayers.Select(u => $"`{u}`"))}");
                return;
            }

            game = new YachtGame
            {
                Players = actualPlayers
            };
            games.Add(game);

            await Context.Message.ReplyAsync($"Starting match: {string.Join(" vs ", actualPlayers.Select(u => $"`{u}`"))}");
            await PrintScores(game);
        }
    }
}