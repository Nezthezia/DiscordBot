using Application.DTOs;
using Application.Interfaces;
using Discord;
using Discord.WebSocket;
using DiscordBot.Builders;
using System.Collections.Concurrent;

namespace DiscordBot.Services
{
    public class PlayerUiService
    {
        private readonly DiscordSocketClient _client;

        // ── Estructura interna ──────────────────────────────────────────────
        // Guarda channelId, messageId Y el track que se está reproduciendo.
        // Al tener el track aquí ya no hay que pasarlo desde fuera.
        private record PlayerMessageInfo(ulong ChannelId, ulong MessageId, TrackInfoDto Track);

        // Mensaje activo (reproduciéndose ahora) por guild
        private readonly ConcurrentDictionary<ulong, PlayerMessageInfo> _activeMessages = new();

        // Cola de mensajes pendientes (canciones en espera) por guild
        // ConcurrentQueue es thread-safe sin necesidad de locks manuales
        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<PlayerMessageInfo>> _pendingMessages = new();

        public PlayerUiService(DiscordSocketClient client, IAudioService audioService)
        {
            _client = client;
            audioService.TrackEnded += OnTrackEndedAsync;
        }

        // ── Registro ────────────────────────────────────────────────────────

        /// <summary>Registra el mensaje activo del player (canción reproduciéndose).</summary>
        public void RegisterPlayerMessage(ulong guildId, ulong channelId, ulong messageId, TrackInfoDto track)
        {
            _activeMessages[guildId] = new PlayerMessageInfo(channelId, messageId, track);
        }

        /// <summary>Encola un mensaje pendiente (canción en la cola de espera).</summary>
        public void EnqueuePendingMessage(ulong guildId, ulong channelId, ulong messageId, TrackInfoDto track)
        {
            var queue = _pendingMessages.GetOrAdd(guildId, _ => new ConcurrentQueue<PlayerMessageInfo>());
            queue.Enqueue(new PlayerMessageInfo(channelId, messageId, track));
        }

        /// <summary>
        /// Devuelve el track del mensaje activo sin removerlo del diccionario.
        /// Útil para los botones que necesitan construir el embed antes de hacer UpdateAsync.
        /// </summary>
        public TrackInfoDto? GetCurrentTrack(ulong guildId) =>
            _activeMessages.TryGetValue(guildId, out var info) ? info.Track : null;

        /// <summary>
        /// Solo remueve el entry del diccionario sin modificar el mensaje de Discord.
        /// Úsalo cuando ya actualizaste el mensaje por otro medio (p.ej. component.UpdateAsync).
        /// </summary>
        public bool RemoveActiveMessage(ulong guildId) =>
            _activeMessages.TryRemove(guildId, out _);

        // ── Limpieza del player activo ───────────────────────────────────────

        /// <summary>
        /// Remueve el mensaje activo del tracking Y lo modifica en Discord:
        /// elimina los botones y actualiza el embed al estado "terminado/saltado/detenido".
        ///
        /// Patrón TryRemove: solo el PRIMER llamador gana el lock atómico;
        /// llamadas subsecuentes (ej. OnTrackEndedAsync después de un Skip manual)
        /// son no-op automáticamente → sin condiciones de carrera.
        /// </summary>
        /// <param name="guildId">Id del servidor.</param>
        /// <param name="customTitle">Título opcional para el embed final. Si es null usa el título del track.</param>
        public async Task<bool> CleanActivePlayerUiAsync(ulong guildId, string? customTitle = null)
        {
            // TryRemove es atómico: si falla, alguien más ya limpió → no-op seguro
            if (!_activeMessages.TryRemove(guildId, out var info))
                return false;

            try
            {
                if (await _client.GetChannelAsync(info.ChannelId) is not IMessageChannel channel)
                    return false;
                if (await channel.GetMessageAsync(info.MessageId) is not IUserMessage message)
                    return false;

                string title = customTitle ?? $"⏹️ {info.Track.Title} — {info.Track.Autor}";

                await message.ModifyAsync(msg =>
                {
                    // ComponentBuilder vacío = elimina todos los botones
                    msg.Components = new ComponentBuilder().Build();
                    msg.Embed = new EmbedBuilder()
                        .WithColor(new Color(0x7F00FF))
                        .WithTitle(title)
                        .Build();
                });

                return true;
            }
            catch
            {
                // El mensaje fue eliminado, el canal no existe, permisos, etc.
                // No propagamos: es un estado esperado (usuario borró el mensaje).
                return false;
            }
        }

        // ── Promoción de la cola ─────────────────────────────────────────────

        /// <summary>
        /// Saca el primer mensaje pendiente, lo convierte en activo y actualiza su
        /// embed + botones para mostrar el player completo.
        /// Si no hay pendientes, no hace nada (no hay fuga de recursos).
        /// </summary>
        public async Task PromoteAndActivateNextAsync(ulong guildId)
        {
            if (!_pendingMessages.TryGetValue(guildId, out var queue)) return;
            if (!queue.TryDequeue(out var next)) return;

            // Primero registrar como activo, luego modificar Discord
            _activeMessages[guildId] = next;

            try
            {
                if (await _client.GetChannelAsync(next.ChannelId) is not IMessageChannel channel) return;
                if (await channel.GetMessageAsync(next.MessageId) is not IUserMessage message) return;

                string avatarUrl = _client.CurrentUser.GetAvatarUrl();
                var embed = MusicEmbedBuilder.BuildPlayerEmbed(next.Track, avatarUrl);
                var components = MusicComponentBuilder.BuildPlayerComponents(
                    MusicComponentBuilder.GetIsBucle(),
                    MusicComponentBuilder.GetIsPaused());

                await message.ModifyAsync(msg =>
                {
                    msg.Embed = embed;
                    msg.Components = components;
                    msg.Content = string.Empty; // limpia cualquier texto anterior
                });
            }
            catch
            {
                // Si el mensaje ya no existe, lo sacamos del tracking también
                _activeMessages.TryRemove(guildId, out _);
            }
        }

        // ── Evento de fin de track (llamado por el audio service) ────────────

        /// <summary>
        /// Maneja el fin natural de una canción:
        ///   1. Limpia el player activo (no-op si ya fue limpiado por Skip/Stop).
        ///   2. Promueve el siguiente pendiente (si existe).
        /// </summary>
        public async Task OnTrackEndedAsync(TrackInfoDto trackInfoDto, ulong guildId)
        {
            // Si fue skip/stop manual, TryRemove ya falló y esto es no-op
            await CleanActivePlayerUiAsync(
                guildId,
                $"It's over {trackInfoDto.Title} of {trackInfoDto.Autor}"
            );

            // Siempre intentamos promover el siguiente, sea cual sea la razón del fin
            await PromoteAndActivateNextAsync(guildId);
        }

        // ── Limpieza total del guild ─────────────────────────────────────────

        /// <summary>
        /// Elimina TODOS los datos de tracking del guild (activo + pendientes).
        /// No modifica mensajes de Discord — úsalo después de haber actualizado la UI.
        /// Previene fugas de memoria cuando el bot se desconecta o se ejecuta /stop.
        /// </summary>
        public void ClearGuild(ulong guildId)
        {
            _activeMessages.TryRemove(guildId, out _);
            _pendingMessages.TryRemove(guildId, out _);
        }



        // ── Control de Bucle (Loop) ───────────────────────────────────────────

        /// <summary>
        /// Actualiza los componentes del mensaje activo para reflejar el estado del bucle.
        /// </summary>
        /// <param name="guildId">Id del servidor.</param>
        /// <param name="isBucle">Indica si el bucle debe estar activado o desactivado.</param>
        public async Task UpdateLoopUiAsync(ulong guildId, bool isBucle)
        {
            // Verificamos si existe un mensaje activo para esta guild
            if (!_activeMessages.TryGetValue(guildId, out var info))
                return;

            try
            {
                if (await _client.GetChannelAsync(info.ChannelId) is not IMessageChannel channel)
                    return;

                if (await channel.GetMessageAsync(info.MessageId) is not IUserMessage message)
                    return;

                // Construimos los componentes según el estado del bucle
                var nuevosComponentes = MusicComponentBuilder.BuildPlayerComponents(
                    isBucle: isBucle,
                    MusicComponentBuilder.GetIsPaused());

                await message.ModifyAsync(msg =>
                {
                    msg.Components = nuevosComponentes;
                });
            }
            catch
            {
                // Ignoramos si el mensaje o canal fue eliminado por el usuario
            }
        }



    }
}