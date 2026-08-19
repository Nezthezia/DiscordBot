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

        private record PlayerMessageInfo(ulong ChannelId, ulong MessageId, TrackInfoDto Track);

        // Mensaje activo actual con botones por guild
        private readonly ConcurrentDictionary<ulong, PlayerMessageInfo> _activeMessages = new();

        // Canal guardado por servidor para enviar las tarjetas nuevas cuando avance la cola
        private readonly ConcurrentDictionary<ulong, ulong> _guildChannels = new();

        public PlayerUiService(DiscordSocketClient client, IAudioService audioService)
        {
            _client = client;
            audioService.TrackEnded += OnTrackEndedAsync;
            audioService.TrackStarted += OnTrackStartedAsync; // Escuchamos el inicio automático de cada track
        }

        // ── Registro de canal y mensaje ─────────────────────────────────────

        public void SetGuildChannel(ulong guildId, ulong channelId)
            => _guildChannels[guildId] = channelId;

        public void RegisterPlayerMessage(ulong guildId, ulong channelId, ulong messageId, TrackInfoDto track)
        {
            _activeMessages[guildId] = new PlayerMessageInfo(channelId, messageId, track);
            _guildChannels[guildId] = channelId;
        }

        public TrackInfoDto? GetCurrentTrack(ulong guildId) =>
            _activeMessages.TryGetValue(guildId, out var info) ? info.Track : null;

        public bool RemoveActiveMessage(ulong guildId) =>
            _activeMessages.TryRemove(guildId, out _);

        // ── Desactivar tarjeta anterior (quita botones) ──────────────────────

        public async Task<bool> CleanActivePlayerUiAsync(ulong guildId, string? customTitle = null)
        {
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
                    msg.Components = new ComponentBuilder().Build(); // Quita botones
                    msg.Embed = new EmbedBuilder()
                        .WithColor(new Color(0x7F00FF))
                        .WithTitle(title)
                        .Build();
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Evento: Track Iniciado (Se dispara en CADA canción de Playlist o Cola) ──

        private async Task OnTrackStartedAsync(TrackInfoDto trackInfo, ulong guildId)
        {
            // Evitamos duplicar si /play ya envió y registró esta misma canción al inicio
            if (_activeMessages.TryGetValue(guildId, out var active) && active.Track.Title == trackInfo.Title)
                return;

            if (!_guildChannels.TryGetValue(guildId, out ulong channelId)) return;

            try
            {
                if (await _client.GetChannelAsync(channelId) is not IMessageChannel channel) return;

                string avatarUrl = _client.CurrentUser.GetAvatarUrl();
                var trackNow = trackInfo with { IsPlayingNow = true };

                var embed = MusicEmbedBuilder.BuildPlayerEmbed(trackNow, avatarUrl);
                var components = MusicComponentBuilder.BuildPlayerComponents(
                    MusicComponentBuilder.GetIsBucle(),
                    MusicComponentBuilder.GetIsPaused());

                // Se envía SIEMPRE un mensaje NUEVO cuando arranca una nueva pista
                var message = await channel.SendMessageAsync(embed: embed, components: components);

                _activeMessages[guildId] = new PlayerMessageInfo(channelId, message.Id, trackNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerUiService] Error enviando tarjeta: {ex.Message}");
            }
        }

        // ── Evento: Track Terminado ──────────────────────────────────────────

        public async Task OnTrackEndedAsync(TrackInfoDto trackInfoDto, ulong guildId)
        {
            // Limpia la tarjeta que acaba de terminar (quita botones)
            await CleanActivePlayerUiAsync(
                guildId,
                $"It's over {trackInfoDto.Title} of {trackInfoDto.Autor}"
            );
            // OnTrackStartedAsync se encargará de crear la nueva tarjeta automáticamente cuando Lavalink inicie la siguiente
        }

        // ── Limpieza total ──────────────────────────────────────────────────

        public void ClearGuild(ulong guildId)
        {
            _activeMessages.TryRemove(guildId, out _);
            _guildChannels.TryRemove(guildId, out _);
        }

        // ── Control de Bucle (Loop) ───────────────────────────────────────────

        public async Task UpdateLoopUiAsync(ulong guildId, bool isBucle)
        {
            if (!_activeMessages.TryGetValue(guildId, out var info)) return;

            try
            {
                if (await _client.GetChannelAsync(info.ChannelId) is not IMessageChannel channel) return;
                if (await channel.GetMessageAsync(info.MessageId) is not IUserMessage message) return;

                var nuevosComponentes = MusicComponentBuilder.BuildPlayerComponents(
                    isBucle: isBucle,
                    MusicComponentBuilder.GetIsPaused());

                await message.ModifyAsync(msg => msg.Components = nuevosComponentes);
            }
            catch { }
        }

        public async Task ClearQueueUiAsync(ulong guildId)
        {
            await CleanActivePlayerUiAsync(guildId, "🧹 Se ha limpiado la lista de reproducción");
        }

        public async Task<Embed> RemoveMusicUIAsync(TrackInfoDto? removedTrack, int position)
        {
            if (removedTrack == null)
            {
                return MusicEmbedBuilder.BuildTrackNotFoundEmbed(position);
            }

            return MusicEmbedBuilder.BuildTrackRemovedEmbed(removedTrack, position);
        }

        public async Task<Embed> ShuffleUiAsync()
        {
            return MusicEmbedBuilder.BuildShuffleEmbed();
        }

        public async Task<Embed> VolumeUiAsync(int volume)
        {
            return MusicEmbedBuilder.BuildVolumeEmbed(volume);
        }

        public async Task<Embed> SeekUiAsync(ulong guildId, TimeSpan position)
        {
            var currentTrack = GetCurrentTrack(guildId);

            if (currentTrack is null)
            {
                return new EmbedBuilder()
                    .WithColor(new Color(0xE74C3C))
                    .WithTitle("❌ Sin reproducción")
                    .WithDescription("No hay ninguna canción sonando en este momento.")
                    .Build();
            }

            // Validación: si el tiempo supera la duración de la canción
            if (position > currentTrack?.Duration)
            {
                string? duracionFormateada = currentTrack?.Duration.TotalHours >= 1
                    ? currentTrack?.Duration.ToString(@"hh\:mm\:ss")
                    : currentTrack?.Duration.ToString(@"mm\:ss");

                return new EmbedBuilder()
                    .WithColor(new Color(0xE74C3C))
                    .WithTitle("⚠️ Tiempo fuera de rango")
                    .WithDescription($"Saltando a la siguiente cancion.")
                    .Build();
            }

            string tiempoFormateado = position.TotalHours >= 1
                ? position.ToString(@"hh\:mm\:ss")
                : position.ToString(@"mm\:ss");

            return new EmbedBuilder()
                .WithColor(new Color(0x3498DB))
                .WithTitle("⏩ Posición Actualizada")
                .WithDescription($"Se movió la reproducción a **{tiempoFormateado}**.")
                .AddField("🎵 Canción", currentTrack?.Title, inline: true)
                .WithFooter("Control de Audio • Bot de Música")
                .WithCurrentTimestamp()
                .Build();
        }


        public async Task<Embed> PreviousUiAsync(bool success)
        {

            if (!success)
            {
                return new EmbedBuilder()
                    .WithColor(new Color(0xE74C3C))
                    .WithTitle("❌ Sin historial")
                    .WithDescription("No hay canciones anteriores a las cuales regresar.")
                    .Build();
            }

            return new EmbedBuilder()
                .WithColor(new Color(0x3498DB))
                .WithTitle("⏮️ Regresando pista")
                .WithDescription("Se ha reiniciado la canción o regresado a la pista anterior.")
                .WithFooter("Control de Audio • Bot de Música")
                .WithCurrentTimestamp()
                .Build();
        }

        public async Task<Embed> MoveTrackUiAsync(TrackInfoDto? movedTrack, int position, int newPosition)
        {

            if (movedTrack is null)
            {
                return new EmbedBuilder()
                    .WithColor(new Color(0xE74C3C)) // Rojo error
                    .WithTitle("❌ Posición inválida")
                    .WithDescription($"No se pudo mover la canción. Verifica que las posiciones **#{position}** y **#{newPosition}** existan en la cola.")
                    .Build();
            }

            return new EmbedBuilder()
                .WithColor(new Color(0x3498DB)) // Azul
                .WithTitle("↔️ Canción Movida")
                .WithDescription($"Se movió **{movedTrack?.Title}** de la posición **#{position}** a la **#{newPosition}**.")
                .AddField("👤 Autor", movedTrack?.Autor, inline: true)
                .WithFooter("Gestión de Cola • Bot de Música")
                .WithCurrentTimestamp()
                .Build();
        }



    }
}