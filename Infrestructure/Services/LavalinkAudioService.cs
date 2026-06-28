using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Options;
using System.Numerics;

namespace Infrestructure.Services
{
    public class LavalinkAudioService : Application.Interfaces.IAudioService
    {
        private readonly IAudioService _lavalink; // El servicio nativo que inyectó .NET

        public LavalinkAudioService(IAudioService lavalink)
        {
            _lavalink = lavalink;
        }

        public async Task PlayAsync(ulong guildId, ulong voiceChannelId, string query)
        {
            // Configuración para que el bot soporte una cola de canciones
            var options = new QueuedLavalinkPlayerOptions();

            // Unir al bot al canal de voz del servidor
            var player = await _lavalink.Players.JoinAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
            guildId,
            voiceChannelId,
            playerFactory: (context, _) => ValueTask.FromResult(new QueuedLavalinkPlayer(context)),
            options: Options.Create(options));

            var searchMode = Uri.TryCreate(query, UriKind.Absolute, out _)
            ? TrackSearchMode.None
            : TrackSearchMode.YouTube;

            var track = await _lavalink.Tracks.LoadTrackAsync(query, searchMode);

            if (track is null) return;

            // Reproducir o mandar a la cola automáticamente
            await player.PlayAsync(track);
        }

        public async Task SkipAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                await player.SkipAsync();
            }
        }

        public async Task<IEnumerable<LavalinkTrack>> SearchTracksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<LavalinkTrack>();

            // Buscamos una lista de tracks (LoadTracksAsync en lugar de LoadTrackAsync)
            var searchResult = await _lavalink.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

            // Retornamos las primeras 5 o 7 canciones encontradas para no saturar
            return searchResult.Tracks.Take(7) ?? Enumerable.Empty<LavalinkTrack>();
        }

        public async Task<IEnumerable<string>> GetQueueAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                // NUEVA LÓGICA REEMPLAZADA:
                var resultado = new List<string>();

                // 1. Si hay algo sonando, lo metemos en la posición [0] con su prefijo
                if (player.CurrentTrack is not null)
                {
                    resultado.Add($"SONANDO AHORA: {player.CurrentTrack.Title} - {player.CurrentTrack.Author}");
                }

                // 2. Limpiamos nulos de la cola y transformamos a string al mismo tiempo
                var enCola = player.Queue.Select(x => x.Track).OfType<LavalinkTrack>().Select(t => $"{t.Title} - {t.Author}");

                // 3. Acoplamos las canciones en espera al final de la lista
                resultado.AddRange(enCola);

                return resultado;
            }

            // Si el reproductor no existe, la lista vuelve vacía limpiamente
            return Enumerable.Empty<string>();
        }
    }
}
