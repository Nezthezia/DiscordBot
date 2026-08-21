using Application.Common.Constants;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Builders;
using DiscordBot.Handler;
using DiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

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
        private readonly Application.Interfaces.IAudioService _audioService;
        private readonly PlayerUiService _playerUiService;

        // El constructor ahora recibe TODO limpio desde .NET
        public DiscordBotWorker(
            IConfiguration configuration,
            ILogger<DiscordBotWorker> logger,
            IServiceProvider services,
            DiscordSocketClient client,
            InteractionService interactionService,
            DiscordMessageListener messageListener,
            Application.Interfaces.IAudioService audioService,
            PlayerUiService playerUiService)
        {
            _configuration = configuration;
            _logger = logger;
            _services = services;
            _client = client;
            _interactionService = interactionService;
            _messageListener = messageListener;
            _audioService = audioService;
            _playerUiService = playerUiService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Ready += OnReadyAsync;

            // 1. Enlazar los logs de Discord a la consola de .NET
            _client.Log += LogAsync;

            // 2. Enlazar el evento de mensajes recibidos (Nuestro Hola Mundo)
            _client.MessageReceived += _messageListener.HandleMessageAsync;

            _client.InteractionCreated += OnInteractionCreatedAsync;

            //_client.MessageDeleted += OnMessageDeleted;

            _client.ButtonExecuted += OnButtonExecutedAsync;

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

        /*private async Task OnMessageDeleted(Cacheable<IMessage, ulong> cachedMessage,
    Cacheable<IMessageChannel, ulong> cachedChannel)
        {
            var channel = await cachedChannel.GetOrDownloadAsync();
            ulong channelId = _configuration.GetValue<ulong>("DiscordConfig:LogsChannelId");
            string message = "-------------------------------------------\n"
                + "!!!MENSAJE ELIMINADO!!!\n"
                + $"Canal: #{channel?.Name ?? "Desconocido"}\n";

            if (cachedMessage.HasValue)
            {
                var msg = cachedMessage.Value;
                message += $"Autor: {msg.Author.Username} ({msg.Author.Id})\n"
                    + $"Contenido: {msg.Content}\n";

                if (msg.Attachments.Count > 0)
                {
                    message += $"Archivos adjuntos que tenía: {msg.Attachments.Count}\n";
                }
            }
            else
            {
                message += "Contenido no disponible en la caché local.\n";
            }
            message += $"ID del Mensaje: {cachedMessage.Id}\n";

            if (channel is IGuildChannel guildChannel)
            {
                try
                {
                    var guild = guildChannel.Guild;
                    var auditLogs = await guild.GetAuditLogsAsync(limit: 5, actionType: ActionType.MessageDeleted);
                    var auditEntry = auditLogs.FirstOrDefault(x =>
                        x.Data is MessageDeleteAuditLogData data &&
                        data.ChannelId == channel.Id &&
                        (cachedMessage.HasValue ? data.Target.Id == cachedMessage.Value.Author.Id : true));

                    message += auditEntry != null ? 
                        ($"Eliminador: {auditEntry.User.Username} ({auditEntry.User.Id}) [Moderador]") 
                        : 
                        ($"{cachedMessage.Value.Author.Username} (Propio autor)");

                }
                catch(Exception ex)
                {
                    _logger.LogWarning($"No se pudieron consultar los Audit Logs: {ex.Message}");
                }
            }

            _logger.LogInformation(message);

            if (_client.GetChannel(channelId) is SocketTextChannel _channel)
            {
                await _channel.SendMessageAsync(message);
            }
        }
        */
        private async Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            ulong guildId = component.GuildId ?? 0;
            if (guildId == 0) return;

            // Evaluamos contra las constantes estáticas
            switch (component.Data.CustomId)
            {
                case AudioButtonIds.Pause:
                    await _audioService.PauseAsync(guildId);
                    var componentesPausado = MusicComponentBuilder.BuildPlayerComponents(
                        MusicComponentBuilder.GetIsBucle(),
                        isPaused: true);
                    await component.UpdateAsync(msg =>
                    {
                        msg.Components = componentesPausado;
                    });
                    break;

                case AudioButtonIds.Resume:
                    await _audioService.ResumeAsync(guildId);
                    var componentesResumen = MusicComponentBuilder.BuildPlayerComponents(
                        MusicComponentBuilder.GetIsBucle(), 
                        isPaused: false);
                    await component.UpdateAsync(msg =>
                    {
                        msg.Components = componentesResumen;
                    });
                    break;

                case AudioButtonIds.Loop:
                    await _audioService.NotLoopAsync(guildId);
                    var componentesBucle = MusicComponentBuilder.BuildPlayerComponents(
                        isBucle: false,
                        MusicComponentBuilder.GetIsPaused());
                    await component.UpdateAsync(msg =>
                    {
                        msg.Components = componentesBucle;
                    });
                    break;

                case AudioButtonIds.NotLoop:
                    await _audioService.LoopAsync(guildId);
                    var componentesNoBucle = MusicComponentBuilder.BuildPlayerComponents(
                        isBucle: true,
                        MusicComponentBuilder.GetIsPaused());
                    await component.UpdateAsync(msg =>
                    {
                        msg.Components = componentesNoBucle;
                    });
                    break;

                case AudioButtonIds.Skip:
                    {
                        var track = _playerUiService.GetCurrentTrack(guildId);
                        _playerUiService.RemoveActiveMessage(guildId);
                        MusicComponentBuilder.SetIsBucle(false);
                        MusicComponentBuilder.SetIsPaused(false);
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = string.Empty;
                            msg.Components = new ComponentBuilder().Build(); 
                            msg.Embed = track is not null
                                ? new EmbedBuilder()
                                    .WithColor(new Color(0x7F00FF))
                                    .WithTitle($"⏭️ Saltada: {track?.Title} — {track?.Autor}")
                                    .Build()
                                : new EmbedBuilder()
                                    .WithColor(new Color(0x7F00FF))
                                    .WithTitle("⏭️ Canción saltada")
                                    .Build();
                        });
                        await _audioService.SkipAsync(guildId);
                        //await _playerUiService.PromoteAndActivateNextAsync(guildId);
                        break;
                    }

                case AudioButtonIds.Stop:
                    {
                        var track = _playerUiService.GetCurrentTrack(guildId);
                        _playerUiService.ClearGuild(guildId);
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = string.Empty;
                            msg.Components = new ComponentBuilder().Build();
                            msg.Embed = track is not null
                                ? new EmbedBuilder()
                                    .WithColor(new Color(0x7F00FF))
                                    .WithTitle($"😭 Se acabó: {track?.Title} — {track?.Autor}")
                                    .Build()
                                : new EmbedBuilder()
                                    .WithColor(new Color(0x7F00FF))
                                    .WithTitle("😭 Se acabó la fiesta")
                                    .Build();
                        });
                        await _audioService.StopAsync(guildId);
                        break;
                    }
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
