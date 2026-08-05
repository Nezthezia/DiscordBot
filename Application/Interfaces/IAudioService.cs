using Lavalink4NET.Tracks;
using Application.DTOs;

namespace Application.Interfaces
{
    public interface IAudioService
    {
        Task<TrackInfoDto> PlayAsync(ulong guildId, ulong voiceChannelId, string query);
        Task SkipAsync(ulong guildId);
        Task<IEnumerable<string>> GetQueueAsync(ulong guildId);

        Task<IEnumerable<LavalinkTrack>> SearchTracksAsync(string query);

        Task PauseAsync(ulong guildId);

        Task ResumeAsync(ulong guildId);

        Task StopAsync(ulong guildId);

        event Func<TrackInfoDto, ulong, Task>? TrackEnded;
    }
}
