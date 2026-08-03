using Application.Interfaces;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Options;
using IAudioService = Lavalink4NET.IAudioService;

namespace Infrastructure.Services
{
    public class LavalinkAudioService : Application.Interfaces.IAudioService
    {
        private readonly IAudioService _lavalink;
        private readonly ISpotifyModule _spotify;
        private readonly IDeezerModule _deezer;

        public LavalinkAudioService(
            IAudioService lavalink,
            ISpotifyModule spotify,
            IDeezerModule deezer)
        {
            _lavalink = lavalink;
            _spotify = spotify;
            _deezer = deezer;
        }

        public async Task PlayAsync(ulong guildId, ulong voiceChannelId, string query)
        {
            var options = new QueuedLavalinkPlayerOptions { DisconnectOnStop = true };

            var player = await _lavalink.Players.JoinAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                guildId,
                voiceChannelId,
                playerFactory: (context, _) => ValueTask.FromResult(new QueuedLavalinkPlayer(context)),
                options: Options.Create(options));

            var track = await ResolveTrackAsync(query);

            if (track is null) return;

            await player.PlayAsync(track);
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

        public async Task<IEnumerable<string>> GetQueueAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
            {
                var resultado = new List<string>();

                if (player.CurrentTrack is not null)
                    resultado.Add($"SONANDO AHORA: {player.CurrentTrack.Title} - {player.CurrentTrack.Author}");

                var enCola = player.Queue
                    .Select(x => x.Track)
                    .OfType<LavalinkTrack>()
                    .Select(t => $"{t.Title} - {t.Author}");

                resultado.AddRange(enCola);
                return resultado;
            }

            return Enumerable.Empty<string>();
        }

        public async Task StopAsync(ulong guildId)
        {
            if (_lavalink.Players.TryGetPlayer<QueuedLavalinkPlayer>(guildId, out var player) && player is not null)
                await player.DisconnectAsync();
        }

        // ── Helpers privados ─────────────────────────────────────────────────────

        /// <summary>
        /// Lógica centralizada de resolución de tracks:
        /// 1. URL de Spotify  → SpotifyModule.ResolveAsync
        /// 2. URL de Deezer   → DeezerModule.ResolveAsync
        /// 3. Texto libre     → busca en Spotify primero, luego en Deezer como fallback
        /// </summary>
        private async Task<LavalinkTrack?> ResolveTrackAsync(string query)
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
            var track = spotifyResults.FirstOrDefault();

            if (track is null)
            {
                var deezerResults = await _deezer.SearchAsync(query, limit: 1);
                track = deezerResults.FirstOrDefault();
            }*/

            return track;
        }

        private static bool IsYouTubeUrl(string query)
        {
            return query.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                || query.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
        }
    }
}