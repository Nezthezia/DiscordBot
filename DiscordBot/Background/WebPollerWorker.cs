using Discord.WebSocket;

namespace DiscordBot.Background
{
    public class WebPollerWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebPollerWorker> _logger;
        private readonly long _pollingInterval = 5;

        public WebPollerWorker(
            DiscordSocketClient client,
            IConfiguration configuration,
            ILogger<WebPollerWorker> logger)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ulong channelId = _configuration.GetValue<ulong>("DiscordConfig:AnnouncementChannelId");

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_pollingInterval));

            _logger.LogInformation($"Servicio de anuncios periódicos iniciado (cada {_pollingInterval} minutos).");

            // 3. Bucle que se ejecuta cada vez que el timer cumple 5 minutos
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // Asegurarnos de que el bot esté conectado antes de intentar enviar
                    if (_client.ConnectionState != Discord.ConnectionState.Connected)
                    {
                        _logger.LogWarning("El bot aún no está conectado a Discord. Omitiendo ciclo...");
                        continue;
                    }

                    // Buscar el canal por su ID
                    if (_client.GetChannel(channelId) is SocketTextChannel channel)
                    {
                        string mensaje = "Hola pinches putas";

                        await channel.SendMessageAsync(mensaje);
                        _logger.LogInformation("Mensaje periódico enviado con éxito al canal {ChannelId}", channelId);
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró el canal de texto con ID: {ChannelId}", channelId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al intentar enviar el mensaje periódico al canal {ChannelId}", channelId);
                }
            }
        }
    }
}
