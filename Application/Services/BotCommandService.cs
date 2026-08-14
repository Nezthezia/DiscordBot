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
            return Task.FromResult<string?>("foto no disponible actualmente");
        }else if (content.Equals("goku", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("alex", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Enviar foto del programador");
            return Task.FromResult<string?>("https://klipy.com/gifs/mujikcboro-seriymujik-1");
        } else if(content.Equals("joel", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Enviar foto del joel");
            return Task.FromResult<string?>("https://cdn.discordapp.com/attachments/1536721828679127131/1536918975491866664/Perfil_de_alias_Shadow-Anime.webp?ex=6a7d26d6&is=6a7bd556&hm=a59d7bc1178232d598ffad5e277531372e0b5c3f3b9e31606da8e712637c5a45");
        }

        return Task.FromResult<string?>(null);
    }
}