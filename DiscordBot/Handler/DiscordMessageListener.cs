using Application.Interfaces;
using Discord.WebSocket;

namespace DiscordBot.Handler;

public class DiscordMessageListener
{
    private readonly IBotCommandService _commandService;

    public DiscordMessageListener(IBotCommandService commandService)
    {
        _commandService = commandService;
    }

    public async Task HandleMessageAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        string? response = await _commandService.ProcessMessageAsync(
            message.Content,
            message.Author.Username,
            message.Author.Mention
        );

        if (!string.IsNullOrEmpty(response))
        {
            await message.Channel.SendMessageAsync(response);
        }
    }
}