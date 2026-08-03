using Lavalink4NET.Tracks;

namespace Application.Interfaces
{
    /// <summary>
    /// Módulo responsable de resolver y buscar canciones a través de Deezer.
    /// Requiere el plugin LavaSrc activo en el servidor Lavalink.
    /// </summary>
    public interface IDeezerModule
    {
        /// <summary>
        /// Detecta si la query es una URL directa de Deezer.
        /// </summary>
        bool IsDeezerUrl(string query);

        /// <summary>
        /// Resuelve una URL de Deezer a un LavalinkTrack reproducible.
        /// </summary>
        Task<LavalinkTrack?> ResolveAsync(string deezerUrl);

        /// <summary>
        /// Busca canciones en Deezer por texto. Devuelve hasta <paramref name="limit"/> resultados.
        /// </summary>
        Task<IEnumerable<LavalinkTrack>> SearchAsync(string query, int limit = 7);
    }
}