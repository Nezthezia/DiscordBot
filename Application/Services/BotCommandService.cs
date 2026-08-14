// Application/Services/BotCommandService.cs
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class BotCommandService : IBotCommandService
{
    private readonly ILogger<BotCommandService> _logger;
    private const string CommandInit = "!"; // O config desde appsettings

    public BotCommandService(ILogger<BotCommandService> logger)
    {
        _logger = logger;
    }

    public Task<string?> ProcessMessageAsync(string content, string username, string userMention)
    {
        if (content.Equals($"{CommandInit}hola", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Comando !hola recibido de {User}", username);
            return Task.FromResult<string?>($"¡Hola Mundo desde .NET 10 y Clean Architecture, {userMention}! 🚀");
        }

        return Task.FromResult<string?>(null);
    }
}