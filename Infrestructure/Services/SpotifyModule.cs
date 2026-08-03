using Application.Interfaces;
using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using IAudioService = Lavalink4NET.IAudioService;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implementación del módulo Spotify usando LavaSrc (prefijo "spsearch").
    /// Para URLs directas, LavaSrc resuelve el track sin necesitar prefijo.
    /// </summary>
    public class SpotifyModule : ISpotifyModule
    {
        private readonly IAudioService _lavalink;

        // LavaSrc usa "spsearch:" como prefijo para búsquedas de texto en Spotify
        private static readonly TrackSearchMode SpotifySearch = new("spsearch");

        public SpotifyModule(IAudioService lavalink)
        {
            _lavalink = lavalink;
        }

        /// <inheritdoc/>
        public bool IsSpotifyUrl(string query)
            => query.Contains("spotify.com", StringComparison.OrdinalIgnoreCase)
            || query.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase); // URIs tipo spotify:track:xxx

        /// <inheritdoc/>
        public async Task<LavalinkTrack?> ResolveAsync(string spotifyUrl)
        {
            // LavaSrc intercepta la URL de Spotify y la resuelve directamente.
            // TrackSearchMode.None le indica a Lavalink que la query ya es una URL.
            return await _lavalink.Tracks.LoadTrackAsync(spotifyUrl, TrackSearchMode.None);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<LavalinkTrack>> SearchAsync(string query, int limit = 7)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<LavalinkTrack>();

            var result = await _lavalink.Tracks.LoadTracksAsync(query, SpotifySearch);

            return result.Tracks.Take(limit);
        }
    }
}