using Application.DTOs;
using Application.Interfaces;
using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Numerics;
using static System.Net.WebRequestMethods;

using IAudioService = Lavalink4NET.IAudioService;

namespace Infrastructure.Services
{
    public class LavalinkAudioService : Application.Interfaces.IAudioService
    {
        private readonly IAudioService _lavalink;
        private readonly ISpotifyModule _spotify;
        private readonly IDeezerModule _deezer;
        private readonly ILogger<LavalinkAudioService> _logger;

        public event Func<TrackInfoDto, ulong, Task>? TrackEnded;
        public event Func<TrackInfoDto, ulong, Task>? TrackStarted;

        private readonly ConcurrentDictionary<ulong, Stack<LavalinkTrack>> _history = new();
        private readonly ConcurrentDictionary<ulong, bool> _isRevertingHistory = new();
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _updateCancellations = new();
        private readonly ConcurrentDictionary<ulong, TrackInfoDto> _currentTracks = new();


        public LavalinkAudioService(
            IAudioService lavalink,
            ISpotifyModule spotify,
            IDeezerModule deezer,
            ILogger<LavalinkAudioService> logger)
        {
            _lavalink = lavalink;
            _spotify = spotify;
            _deezer = deezer;
            _logger = logger;
            _lavalink.TrackEnded += OnLavalinkTrackEndedAsync;
            _lavalink.TrackStarted += OnLavalinkTrackStartedAsync;
        }

        public async Task<TrackInfoDto> PlayAsync(ulong guildId, ulong voiceChannelId, string query,
            string channelName, string userName)
        {
            var options = new QueuedLavalinkPlayerOptions { DisconnectOnStop = true };

            var player = await _lavalink.Players.JoinAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                guildId,
                voiceChannelId,
                playerFactory: PlayerFactory.Queued,
                //playerFactory: (context, _) => ValueTask.FromResult(new QueuedLavalinkPlayer(context)),
                options: Options.Create(options));

            var tracks = await ResolveTrackAsync(query);

            var track = tracks?.FirstOrDefault();

            if (tracks is null || track is null) return new TrackInfoDto(
                Autor: "",
                Title: "",
                Duration: new TimeSpan(0, 0, 0),
                Position: new TimeSpan(0,0,0),
                IsPlayingNow: false
                );

            bool isPlaying = (player.CurrentTrack != null);

            if (isPlaying)
            {
                // Hay algo sonando: encolar todos los tracks al final
                foreach (var item in tracks)
                    await player.Queue.AddAsync(new TrackQueueItem(item));
            }
            else
            {
                // No hay nada sonando: reproducir el primero y encolar el resto
                await player.PlayAsync(track);

                for (int i = 1; i < tracks.Count; i++)
                    await player.Queue.AddAsync(new TrackQueueItem(tracks[i]));
            }

            var trackInfo =  new TrackInfoDto(
                Autor: track.Author,
                Title: track.Title,
                Duration: track.Duration,
                Position: TimeSpan.Zero,
                IsPlayingNow: !isPlaying,

                //Valores nulos
                RequestedByMention: userName,
                ChannelName: channelName,
                QueueSize: player.Queue.Count,
                Volume: (int)(player.Volume * 100),
                Uri: track.Uri?.AbsoluteUri,
                ArtworkUri: track.ArtworkUri?.AbsoluteUri
                );

            if (trackInfo.IsPlayingNow)
            {
                _currentTracks[guildId] = trackInfo;
            }

            return trackInfo;
        }

        public async Task SkipAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
                await player.SkipAsync();
        }

        public async Task<IEnumerable<LavalinkTrack>> SearchTracksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<LavalinkTrack>();

            // Buscamos primero en Deezer; si no hay resultados, intentamos en Spotify
            var results = (await _deezer.SearchAsync(query)).ToList();

            if (results.Count == 0)
                results = (await _spotify.SearchAsync(query)).ToList();

            return results;
        }

        public async Task<IEnumerable<TrackInfoDto>> GetQueueAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                var resultado = new List<TrackInfoDto>();

                int queueCount = player.Queue.Count;
                int volume = (int)(player.Volume * 100);

                // 1. Agregar la canción que está sonando ahora (si existe)
                if (player.CurrentTrack is not null)
                {
                    // Reutilizamos el estado guardado si existe para no perder el RequestedByMention / ChannelName
                    var activeInfo = GetCurrentTrack(player.GuildId);

                    var currentTrackDto = activeInfo ?? new TrackInfoDto(
                        Autor: player.CurrentTrack.Author,
                        Title: player.CurrentTrack.Title,
                        Duration: player.CurrentTrack.Duration,
                        Position: player.Position != null ? player.Position.Value.Position : TimeSpan.Zero,
                        IsPlayingNow: true,
                        RequestedByMention: "Desconocido",
                        ChannelName: "Canal de voz",
                        QueueSize: queueCount,
                        Volume: volume,
                        Uri: player.CurrentTrack.Uri?.AbsoluteUri,
                        ArtworkUri: player.CurrentTrack.ArtworkUri?.AbsoluteUri
                    );

                    resultado.Add(currentTrackDto);
                }

                // 2. Mapear y agregar la cola de reproducción
                var enCola = player.Queue
                    .Select(x => x.Track)
                    .Where(t => t is not null)
                    .Select(t => new TrackInfoDto(
                        Autor: t.Author,
                        Title: t.Title,
                        Duration: t.Duration,
                        Position: TimeSpan.Zero,
                        IsPlayingNow: false,
                        RequestedByMention: "Desconocido",
                        ChannelName: "Canal de voz",
                        QueueSize: queueCount,
                        Volume: volume,
                        Uri: t.Uri?.AbsoluteUri,
                        ArtworkUri: t.ArtworkUri?.AbsoluteUri
                    ));

                resultado.AddRange(enCola);

                return resultado;
            }

            return [];
        }

        public async Task PauseAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                await player.PauseAsync();
            }
        }

        public async Task ResumeAsync(ulong guildId) 
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                await player.ResumeAsync();
            }
        }

        public async Task StopAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                await player.Queue.ClearAsync();
                await player.StopAsync();
            }
        }

        // ── Helpers privados ─────────────────────────────────────────────────────

        /// <summary>
        /// Lógica centralizada de resolución de tracks:
        /// 1. URL de Spotify  → SpotifyModule.ResolveAsync
        /// 2. URL de Deezer   → DeezerModule.ResolveAsync
        /// 3. Texto libre     → busca en Spotify primero, luego en Deezer como fallback
        /// </summary>
        private async Task<IReadOnlyList<LavalinkTrack>?> ResolveTrackAsync(string query)
        {
            if (_spotify.IsSpotifyUrl(query))
                throw new InvalidOperationException("Spotify aun no esta disponible se le pide amablemente usar deezer o buscarlo manualmente.");
            //return await _spotify.ResolveAsync(query);
            else if (_deezer.IsDeezerUrl(query))
                return await _deezer.ResolveAsync(query);

            else if (IsYouTubeUrl(query))
                throw new InvalidOperationException("YouTube no se puede usar.");

            var deezerResults = await _deezer.SearchAsync(query, limit: 1);
            var track = deezerResults.FirstOrDefault();

            // Búsqueda por texto: Spotify es preferido, Deezer es el fallback
            /*var spotifyResults = await _spotify.SearchAsync(query, limit: 1);
            var track = spotifyResults.FirstOrDefault();*/

            if (track is null)
            {
                return null;
            }

            return [track];
        }

        private static bool IsYouTubeUrl(string query)
        {
            return query.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                || query.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
        }

        private async Task OnLavalinkTrackEndedAsync(object sender, TrackEndedEventArgs args)
        {
            if (args.Player is not QueuedLavalinkPlayer player || args.Track is null)
                return;

            if (args.Track is not null &&
                (!_isRevertingHistory.TryGetValue(args.Player.GuildId, out var isReverting) || !isReverting))
            {
                if (args.Track is not null)
                {
                    var stack = _history.GetOrAdd(args.Player.GuildId, _ => new Stack<LavalinkTrack>());
                    stack.Push(args.Track);
                }
            }

            if (args.Player is QueuedLavalinkPlayer { RepeatMode: TrackRepeatMode.Track })
                return;

            if (TrackEnded is null || args.Track is null)
                return;

            var currentInfo = GetCurrentTrack(player.GuildId);
            _currentTracks.TryRemove(player.GuildId, out _);


            var trackInfo = new TrackInfoDto(
                Autor: args.Track.Author,
                Title: args.Track.Title,
                Duration: args.Track.Duration,
                Position: args.Track.Duration,
                IsPlayingNow: false,
                //RequestedByMention: requestedByMention,
                RequestedByMention: currentInfo != null? currentInfo?.RequestedByMention : "Desconocido",
                ChannelName: currentInfo != null? currentInfo?.ChannelName : "Canal de voz",
                QueueSize: player.Queue.Count,
                Volume: (int)(player.Volume * 100),
                Uri: player.CurrentTrack?.Uri?.AbsoluteUri,
                ArtworkUri: player.CurrentTrack?.ArtworkUri?.AbsoluteUri
            );

            await TrackEnded.Invoke(trackInfo, args.Player.GuildId);
        }


        private async Task OnLavalinkTrackStartedAsync(object sender, TrackStartedEventArgs args)
        {
            if (TrackStarted is null || args.Player is not QueuedLavalinkPlayer player || args.Track is null)
                return;

            var currentInfo = GetCurrentTrack(player.GuildId);

            var trackInfo = new TrackInfoDto(
                      Autor: args.Track.Author,
                      Title: args.Track.Title,
                      Duration: args.Track.Duration,
                      Position: TimeSpan.Zero,
                      IsPlayingNow: true,
                      RequestedByMention: currentInfo is not null ? currentInfo?.RequestedByMention : "Desconocido",
                      ChannelName: currentInfo is not null ? currentInfo?.ChannelName : "Canal de voz",
                      QueueSize: player.Queue.Count,
                      Volume: (int)(player.Volume * 100),
                      Uri: args.Track.Uri?.AbsoluteUri,
                      ArtworkUri: args.Track.ArtworkUri?.AbsoluteUri
                  );

            _currentTracks[player.GuildId] = trackInfo;


            await TrackStarted.Invoke(trackInfo, args.Player.GuildId);
        }

        public async Task LoopAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                player.RepeatMode = TrackRepeatMode.Track;
            }
            await Task.CompletedTask;
        }

        public async Task NotLoopAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                player.RepeatMode = TrackRepeatMode.None;
            }

            await Task.CompletedTask;
        }

        public async Task ClearListAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                await player.Queue.ClearAsync();
            }
        }

        public async Task<TrackInfoDto?> RemoveMusicAsync(ulong guildId, int position)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                // Convertimos a base 0 (ej. posición 1 en el comando = índice 0 en la cola)
                int index = position - 1;

                if (index >= 0 && index < player.Queue.Count)
                {
                    var item = player.Queue[index];
                    await player.Queue.RemoveAtAsync(index);

                    var track = item.Track;
                    var currentInfo = GetCurrentTrack(guildId);
                    if (track is not null)
                    {
                        return new TrackInfoDto(
                            Autor: track.Author,
                            Title: track.Title,
                            Duration: track.Duration,
                            Position: TimeSpan.Zero,
                            IsPlayingNow: false,
                            RequestedByMention: currentInfo is not null ? currentInfo?.RequestedByMention : "Desconocido",
                            ChannelName: currentInfo is not null ? currentInfo?.ChannelName : "Canal de voz",
                            QueueSize: player.Queue.Count, // Refleja el tamaño actual tras borrar
                            Volume: (int)(player.Volume * 100),
                            Uri: track.Uri?.AbsoluteUri,
                            ArtworkUri: track.ArtworkUri?.AbsoluteUri
                        );
                    }
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<TrackInfoDto>> GetQueueTracksAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                int queueCount = player.Queue.Count;
                int volume = (int)(player.Volume * 100);

                var currentInfo = GetCurrentTrack(guildId);

                return player.Queue
                    .Select(x => x.Track)
                    .Where(t => t is not null)
                    .Select(t => new TrackInfoDto(
                        Autor: t.Author,
                        Title: t.Title,
                        Duration: t.Duration,
                        Position: TimeSpan.Zero,
                        IsPlayingNow: false,
                        RequestedByMention: currentInfo is not null ? currentInfo?.RequestedByMention : "Desconocido",
                        ChannelName: currentInfo is not null ? currentInfo?.ChannelName : "Canal de voz",
                        QueueSize: queueCount,
                        Volume: volume,
                        Uri: t.Uri?.AbsoluteUri,
                        ArtworkUri: t.ArtworkUri?.AbsoluteUri
                    ))
                    .ToList();
            }

            return Array.Empty<TrackInfoDto>();
        }

        public async Task ShuffleTracksAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                await player.Queue.ShuffleAsync();
            }
        }

        public async Task VolumeAsync(ulong guildId, int volumen)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                float volumePercent = Math.Clamp(volumen, 0, 100) / 100f;
                await player.SetVolumeAsync(volumePercent);
            }
        }

        public async Task SeekTrackAsync(ulong guildId, TimeSpan time)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                if (player.CurrentTrack is not null)
                {
                    await player.SeekAsync(time);
                }
            }
        }

        public async Task<bool> PreviousTrackAsync(ulong guildId)
        {
            if (!_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) || player is null)
                return false;

            // Regla 1: Si pasaron más de 10 o 30 segundos, reiniciamos la canción actual
            if (player.Position.HasValue && player.Position.Value.Position > TimeSpan.FromSeconds(10))
            {
                _logger.LogInformation("Tiempo máximo alcanzado, reiniciando canción actual");
                await player.SeekAsync(TimeSpan.Zero);
                return true;
            }

            // Regla 2: Si lleva poco tiempo, buscamos en el historial
            if (_history.TryGetValue(guildId, out var stack) && stack.TryPop(out var previousTrack))
            {
                _logger.LogInformation($"Regresando a la canción anterior: {previousTrack.Title}");

                _isRevertingHistory[guildId] = true;
                try
                {
                    if (player.CurrentTrack is not null)
                    {
                        // 1. Colocamos la canción actual al frente para que sea la siguiente si el usuario da /skip
                        await player.Queue.InsertAsync(0, new TrackQueueItem(player.CurrentTrack));
                    }

                    // 2. Colocamos la canción anterior en el tope de la cola (índice 0)
                    await player.Queue.InsertAsync(0, new TrackQueueItem(previousTrack));

                    // 3. Hacemos Skip para forzar la reproducción de la canción en posición 0
                    await player.SkipAsync();
                }
                finally
                {
                    _isRevertingHistory[guildId] = false;
                }

                return true;
            }

            // Si no hay historial, reiniciamos desde 00:00
            if (player.CurrentTrack is not null)
            {
                _logger.LogInformation("Sin historial, reiniciando desde el inicio");
                await player.SeekAsync(TimeSpan.Zero);
                return true;
            }

            return false;
        }

        public async Task<TrackInfoDto?> MoveTrackAsync(ulong guildId, int position, int newPosition)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                int oldIndex = position - 1;
                int newIndex = newPosition - 1;

                // Validamos que ambas posiciones existan dentro de la cola
                if (oldIndex >= 0 && oldIndex < player.Queue.Count &&
                    newIndex >= 0 && newIndex < player.Queue.Count)
                {
                    var item = player.Queue[oldIndex];

                    // 1. Eliminamos el track de su posición original
                    await player.Queue.RemoveAtAsync(oldIndex);

                    // 2. Lo insertamos en la nueva posición deseada
                    await player.Queue.InsertAsync(newIndex, item);

                    if (item.Track is LavalinkTrack track)
                    {
                        var currentInfo = GetCurrentTrack(guildId);

                        return new TrackInfoDto(
                            Autor: track.Author,
                            Title: track.Title,
                            Duration: track.Duration,
                            Position: TimeSpan.Zero,
                            IsPlayingNow: false,
                            RequestedByMention: currentInfo is not null ? currentInfo?.RequestedByMention : "Desconocido",
                            ChannelName: currentInfo is not null ? currentInfo?.ChannelName : "Canal de voz",
                            QueueSize: player.Queue.Count,
                            Volume: (int)(player.Volume * 100),
                            Uri: track.Uri?.AbsoluteUri,
                            ArtworkUri: track.ArtworkUri?.AbsoluteUri
                        );
                    }
                }
            }

            return null;
        }


        private TrackInfoDto? GetCurrentTrack(ulong guildId)
        {
            _currentTracks.TryGetValue(guildId, out var trackInfo);
            return trackInfo;
        }


    }
}