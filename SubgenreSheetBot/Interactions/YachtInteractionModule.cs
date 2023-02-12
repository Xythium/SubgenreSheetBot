using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Yacht;

namespace SubgenreSheetBot.Interactions;

[Group("yacht", "Yacht")]
public class YachtInteractionModule : InteractionModuleBase
{
    private static readonly List<YachtGame> games = new();
    
    [SlashCommand("start", "Start")]
    public async Task Start(/*params IUser[] players*/)
    {
        var actualPlayers = new List<IUser>();//players.ToList();

        if (!actualPlayers.Contains(Context.User))
            actualPlayers.Add(Context.User);
        if (!actualPlayers.Contains(Context.Client.CurrentUser))
            actualPlayers.Add(Context.Client.CurrentUser);

        var game = games.FirstOrDefault(g => g.Players.SequenceEqual(actualPlayers));

        if (game != null)
        {
            await FollowupAsync($"There is already a game between players {string.Join(" vs ", actualPlayers.Select(u => $"`{u}`"))}");
            return;
        }

        game = new YachtGame
        {
            Players = actualPlayers
        };
        games.Add(game);

        await FollowupAsync($"Starting match: {string.Join(" vs ", actualPlayers.Select(u => $"`{u}`"))}");
        await PrintScores(game);
    }
    
    private async Task PrintScores(YachtGame game)
    {
        var embed = new EmbedBuilder().WithTitle(string.Join(" vs ", game.Players));

        foreach (var player in game.Players)
        {
            var sb = new StringBuilder("```");
            sb.AppendLine($"1s: aaa | 3x:  aaa");
            sb.AppendLine($"2s: aaa | 4x:  aaa");
            sb.AppendLine($"3s: aaa | 3+2: aaa");
            sb.AppendLine($"4s: aaa | Sml: aaa");
            sb.AppendLine($"5s: aaa | Lrg: aaa");
            sb.AppendLine($"6s: aaa | Yht: aaa");
            sb.AppendLine($"Bns: aa | Chc: aaa");
            sb.Append("```");

            embed = embed.AddField(player.Username, sb.ToString(), true);
        }

        await FollowupAsync(embed: embed.Build());
    }
}