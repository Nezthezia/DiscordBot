using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Handler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Background
{
    public class DiscordBotWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiscordBotWorker> _logger;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;
        private readonly DiscordMessageListener _messageListener;

        // El constructor ahora recibe TODO limpio desde .NET
        public DiscordBotWorker(
            IConfiguration configuration,
            ILogger<DiscordBotWorker> logger,
            IServiceProvider services,
            DiscordSocketClient client,
            InteractionService interactionService,
            DiscordMessageListener messageListener)
        {
            _configuration = configuration;
            _logger = logger;
            _services = services;
            _client = client;
            _interactionService = interactionService;
            _messageListener = messageListener;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Ready += OnReadyAsync;

            // 1. Enlazar los logs de Discord a la consola de .NET
            _client.Log += LogAsync;

            // 2. Enlazar el evento de mensajes recibidos (Nuestro Hola Mundo)
            _client.MessageReceived += _messageListener.HandleMessageAsync;

            _client.InteractionCreated += OnInteractionCreatedAsync;

            // 3. Leer el token desde el appsettings.json
            var token = _configuration["DiscordConfig:Token"];

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogCritical("¡Error! No se encontró el Token de Discord en appsettings.json");
                return;
            }

            // 4. Iniciar sesión y encender el bot
            _logger.LogInformation("Conectando con Discord...");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Mantener el servicio vivo hasta que se apague la aplicación
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private Task LogAsync(LogMessage message)
        {
            _logger.LogInformation("[Discord.Net] {Message}", message.ToString());
            return Task.CompletedTask;
        }

        private async Task OnReadyAsync()
        {
            // Buscar los comandos en tu proyecto e inyectarlos al InteractionService
            await _interactionService.AddModulesAsync(System.Reflection.Assembly.GetExecutingAssembly(), _services);

            // Tu ID de servidor de pruebas
            ulong guildId = _configuration.GetValue<ulong>("DiscordConfig:TestGuildId");

            // ¡AQUÍ VA! Le dice a Discord: "Oye, estos son mis comandos Slash para este servidor"
            _logger.LogInformation("Registrando comandos Slash en Discord...");
            await _interactionService.RegisterCommandsToGuildAsync(guildId);
        }

        private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                await _interactionService.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar una interacción.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Apagando el bot de Discord...");
            await _client.LogoutAsync();
            await _client.StopAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
