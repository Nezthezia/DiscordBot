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

        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _updaterTokens = new();


        private readonly ILogger<PlayerUiService> _logger;

        public PlayerUiService(DiscordSocketClient client, IAudioService audioService, ILogger<PlayerUiService> logger)
        {
            _client = client;
            audioService.TrackEnded += OnTrackEndedAsync;
            audioService.TrackStarted += OnTrackStartedAsync; // Escuchamos el inicio automático de cada track
            _logger = logger;
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
            MusicComponentBuilder.SetIsBucle(false);
            MusicComponentBuilder.SetIsPaused(false);
            if (!_activeMessages.TryRemove(guildId, out var info))
                return false;

            try
            {
                if (await _client.GetChannelAsync(info.ChannelId) is not IMessageChannel channel)
                    return false;
                if (await channel.GetMessageAsync(info.MessageId) is not IUserMessage message)
                    return false;

                StopPlayerUpdater(guildId);

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

            if (_commandHandlingPlay.ContainsKey(guildId))
                return;

            if (!_guildChannels.TryGetValue(guildId, out ulong _)) return;

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

                StartPlayerUpdater(guildId);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"[PlayerUiService] Error enviando tarjeta: {ex.Message}");
            }
        }

        // ── Evento: Track Terminado ──────────────────────────────────────────

        public async Task OnTrackEndedAsync(TrackInfoDto trackInfoDto, ulong guildId)
        {
            // Limpia la tarjeta que acaba de terminar (quita botones)
            MusicComponentBuilder.SetIsBucle(false);
            MusicComponentBuilder.SetIsPaused(false);
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
            MusicComponentBuilder.SetIsBucle(false);
            MusicComponentBuilder.SetIsPaused(false);
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
            MusicComponentBuilder.SetIsBucle(false);
            MusicComponentBuilder.SetIsPaused(false);
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

        public void StopPlayerUpdater(ulong guildId)
        {
            MusicComponentBuilder.SetIsBucle(false);
            MusicComponentBuilder.SetIsPaused(false);
            if (_updaterTokens.TryRemove(guildId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        // Elimina el parámetro IUserMessage — ya no hace falta
        public void StartPlayerUpdater(ulong guildId)
        {
            MusicComponentBuilder.SetIsBucle(false);
            MusicComponentBuilder.SetIsPaused(false);
            StopPlayerUpdater(guildId);

            var cts = new CancellationTokenSource();
            _updaterTokens[guildId] = cts;

            _ = Task.Run(async () =>
            {
                var interval = TimeSpan.FromSeconds(10);
                using var timer = new PeriodicTimer(interval);

                try
                {
                    while (await timer.WaitForNextTickAsync(cts.Token))
                    {
                        if (!_activeMessages.TryGetValue(guildId, out var activePlayer))
                            continue;

                        var track = activePlayer.Track;
                        if (!track.IsPlayingNow)
                            continue;

                        var newPosition = track.Position + interval;
                        if (newPosition > track.Duration)
                            newPosition = track.Duration;

                        var updatedTrack = track with { Position = newPosition };

                        // Actualizamos el estado interno
                        _activeMessages[guildId] = activePlayer with { Track = updatedTrack };

                        // Buscamos el mensaje FRESCO cada tick — sin referencias stale
                        try
                        {
                            if (await _client.GetChannelAsync(activePlayer.ChannelId) is not IMessageChannel channel)
                                continue;
                            if (await channel.GetMessageAsync(activePlayer.MessageId) is not IUserMessage message)
                                continue;

                            var updatedEmbed = MusicEmbedBuilder.BuildPlayerEmbed(
                                updatedTrack, _client.CurrentUser.GetAvatarUrl());

                            await message.ModifyAsync(msg => msg.Embed = updatedEmbed);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[Updater] Error editando mensaje en guild {GuildId}", guildId);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Updater] Error en el loop de guild {GuildId}", guildId);
                }
            }, cts.Token);
        }

        private readonly ConcurrentDictionary<ulong, byte> _commandHandlingPlay = new();
        public void MarkCommandHandlingPlay(ulong guildId)
    => _commandHandlingPlay[guildId] = 1;

        public void ClearCommandHandlingPlay(ulong guildId)
            => _commandHandlingPlay.TryRemove(guildId, out _);

    }
}