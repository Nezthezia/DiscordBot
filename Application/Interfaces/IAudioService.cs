using Lavalink4NET.Tracks;
using Application.DTOs;

namespace Application.Interfaces
{
    public interface IAudioService
    {
        Task<TrackInfoDto> PlayAsync(ulong guildId, ulong voiceChannelId, string query, string channelName, string userName);
        Task SkipAsync(ulong guildId);
        Task<IEnumerable<TrackInfoDto>> GetQueueAsync(ulong guildId);

        Task<IEnumerable<LavalinkTrack>> SearchTracksAsync(string query);

        Task PauseAsync(ulong guildId);

        Task ResumeAsync(ulong guildId);

        Task StopAsync(ulong guildId);

        Task LoopAsync(ulong guildId);

        Task NotLoopAsync(ulong guildId);

        Task ClearListAsync(ulong guildId);

        Task<TrackInfoDto?> RemoveMusicAsync(ulong guildId, int position);

        Task<IReadOnlyList<TrackInfoDto>> GetQueueTracksAsync(ulong guildId);

        Task ShuffleTracksAsync(ulong guildId);

        Task VolumeAsync(ulong guildId, int volumen);

        Task SeekTrackAsync(ulong guildId, TimeSpan time);

        Task<bool> PreviousTrackAsync(ulong guildId);

        Task<TrackInfoDto?> MoveTrackAsync(ulong guildId, int position, int newPosition);

        event Func<TrackInfoDto, ulong, Task>? TrackEnded;
        event Func<TrackInfoDto, ulong, Task>? TrackStarted; 
    }
}
