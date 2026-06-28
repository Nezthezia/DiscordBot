namespace Application.Interfaces
{
    public interface IAudioService
    {
        Task PlayAsync(ulong guildId, ulong voiceChannelId, string query);
        Task SkipAsync(ulong guildId);
        Task<IEnumerable<string>> GetQueueAsync(ulong guildId);
    }
}
