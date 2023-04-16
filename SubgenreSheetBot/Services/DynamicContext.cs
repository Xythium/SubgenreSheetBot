using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using SpotifyAPI.Web;

namespace SubgenreSheetBot.Services;

public class DynamicContext
{
    private IInteractionContext? InteractionContext;
    private ICommandContext? CommandContext;

    public DynamicContext(IInteractionContext context)
    {
        InteractionContext = context;
    }

    public DynamicContext(ICommandContext context)
    {
        CommandContext = context;
    }

    public async Task DeferAsync(bool ephemeral, RequestOptions options)
    {
        if (InteractionContext != null)
        {
            await InteractionContext.Interaction.DeferAsync(ephemeral, options);
        }
    }

    public async Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTts = false, bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null, ISticker[]? stickers = null, RequestOptions? options = null)
    {
        if (InteractionContext != null)
        {
           return await InteractionContext.Interaction.FollowupAsync(text: text, embeds: embeds, isTTS: isTts, ephemeral: ephemeral, allowedMentions: allowedMentions, components: components, embed: embed, options: options);
        }

        if (CommandContext != null)
        {
           return await CommandContext.Message.ReplyAsync(text, isTts, embed, allowedMentions, options, components, stickers, embeds);
        }

        throw new UnreachableException();
    }

    public async Task ErrorAsync(string text, Embed[]? embeds = null, bool isTts = false, bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null, ISticker[]? stickers = null, RequestOptions? options = null)
    {
        var emoji = Emoji.Parse(":warning:");
        await FollowupAsync($"Error {emoji}: {text}", embeds, isTts, ephemeral, allowedMentions, components, embed, stickers, options);
    }

    public async Task FollowupWithFileAsync(Stream fileStream, string fileName, string? text = null, Embed[]? embeds = null, bool isTts = false, bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null, ISticker[]? stickers = null, RequestOptions? options = null)
    {
        if (InteractionContext != null)
        {
            await InteractionContext.Interaction.FollowupWithFileAsync(fileStream, fileName, text, embeds, isTts, ephemeral, allowedMentions, components, embed, options);
        }

        if (CommandContext != null)
        {
            await CommandContext.Channel.SendFileAsync(fileStream, fileName, text, isTts, embed, options, false, allowedMentions, new MessageReference(CommandContext.Message.Id), components, stickers, embeds);
        }
    }

    public async Task SendOrAttachment(string str, bool removeQuotes = false)
    {
        if (str.Length > 2000)
        {
            if (removeQuotes)
                str = str.Replace("`", "");
            var writer = new MemoryStream(Encoding.UTF8.GetBytes(str));
            await FollowupWithFileAsync(writer, "context.txt", "Message too long");
        }
        else
        {
            await FollowupAsync(str);
        }
    }
    
    public async Task<IUserMessage> UpdateOrSend(IUserMessage? message, string str)
    {
        if (message is null)
        {
            return message = await FollowupAsync(str);
        }

        await message.ModifyAsync(m => m.Content = str);
        return message;
    }
}