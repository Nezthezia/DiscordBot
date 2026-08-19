using Lavalink4NET.Tracks;

namespace Application.Interfaces
{
    /// <summary>
    /// Módulo responsable de resolver y buscar canciones a través de Spotify.
    /// Requiere el plugin LavaSrc activo en el servidor Lavalink.
    /// </summary>
    public interface ISpotifyModule
    {
        /// <summary>
        /// Detecta si la query es una URL directa de Spotify.
        /// </summary>
        bool IsSpotifyUrl(string query);

        /// <summary>
        /// Resuelve una URL de Spotify a un LavalinkTrack reproducible.
        /// </summary>
        Task<IReadOnlyList<LavalinkTrack>?> ResolveAsync(string spotifyUrl);

        /// <summary>
        /// Busca canciones en Spotify por texto. Devuelve hasta <paramref name="limit"/> resultados.
        /// </summary>
        Task<IEnumerable<LavalinkTrack>> SearchAsync(string query, int limit = 7);
    }
}