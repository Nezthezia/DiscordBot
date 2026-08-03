using Application.Interfaces;
using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using IAudioService = Lavalink4NET.IAudioService;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implementación del módulo Deezer usando LavaSrc (prefijo "dzsearch").
    /// Para URLs directas, LavaSrc resuelve el track sin necesitar prefijo.
    /// </summary>
    public class DeezerModule : IDeezerModule
    {
        private readonly IAudioService _lavalink;

        // LavaSrc usa "dzsearch:" como prefijo para búsquedas de texto en Deezer
        private static readonly TrackSearchMode DeezerSearch = new("dzsearch");

        public DeezerModule(IAudioService lavalink)
        {
            _lavalink = lavalink;
        }

        /// <inheritdoc/>
        public bool IsDeezerUrl(string query)
            => query.Contains("deezer.com", StringComparison.OrdinalIgnoreCase)
            || query.Contains("deezer.page.link", StringComparison.OrdinalIgnoreCase); // links cortos de Deezer

        /// <inheritdoc/>
        public async Task<LavalinkTrack?> ResolveAsync(string deezerUrl)
        {
            // LavaSrc intercepta la URL de Deezer y la resuelve directamente.
            // TrackSearchMode.None le indica a Lavalink que la query ya es una URL.
            return await _lavalink.Tracks.LoadTrackAsync(deezerUrl, TrackSearchMode.None);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<LavalinkTrack>> SearchAsync(string query, int limit = 7)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<LavalinkTrack>();

            var result = await _lavalink.Tracks.LoadTracksAsync(query, DeezerSearch);

            return result.Tracks.Take(limit);
        }
    }
}