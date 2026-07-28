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

        if (content.Equals($"{CommandInit}adios", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Comando !hola recibido de {User}", username);
            return Task.FromResult<string?>($"¡Nos vemos {userMention} espero verte pronto!");
        }

        if (content.Equals("paola", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Enviar foto de la admin");
            return Task.FromResult<string?>("https://cdn.discordapp.com/attachments/1528389131758211084/1531147712077234286/20260726_215345.jpg?ex=6a6827ed&is=6a66d66d&hm=b8a8622d6e77e74b6ca81a5f1d89894e416209fb37c58b2ca288803012f07004&");
        }

        if (content.Equals("goku", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("alex", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Enviar foto del programador");
            return Task.FromResult<string?>("https://klipy.com/gifs/mujikcboro-seriymujik-1");
        }

        return Task.FromResult<string?>(null);
    }
}