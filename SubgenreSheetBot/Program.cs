using System.IO;
using System.Threading.Tasks;
using XDiscordBotLib.Utils;

namespace SubgenreSheetBot
{
    class Program
    {
        private static void Main(string[] args)
        {
            Logging.Setup();
            new Program().MainAsync()
                .GetAwaiter()
                .GetResult();
        }

        async Task MainAsync()
        {
            var token = File.ReadAllText("bottoken");
            var bot = new SubgenreSheetBot(token, "$");
            await bot.RunAsync();
        }
    }
}