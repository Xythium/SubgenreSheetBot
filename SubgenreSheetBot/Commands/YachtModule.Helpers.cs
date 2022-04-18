using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SubgenreSheetBot.Yacht;

namespace SubgenreSheetBot.Commands
{
    public partial class YachtModule : ModuleBase
    {
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

            await Context.Message.ReplyAsync(embed: embed.Build());
        }
    }
}