using Application.DTOs;
using Application.Interfaces;
using Discord;
using Discord.WebSocket;
using DiscordBot.Builders;
using System.Collections.Concurrent;

namespace DiscordBot.Services
{
    public class PlayerUiService
    {
        private readonly DiscordSocketClient _client;
        private readonly ConcurrentDictionary<ulong, (ulong ChannelId, ulong MessageId)> _activeMessages = new();
        private readonly ConcurrentDictionary<ulong, Queue<(ulong ChannelId, ulong MessageId)>> _pendingMessages = new();


        public PlayerUiService(DiscordSocketClient client, IAudioService audioService)
        {
            _client = client;

            // Nos suscribimos al evento de la infraestructura cuando una pista termina
            audioService.TrackEnded += OnTrackEndedAsync;
        }

        // Cuando una nueva canción empieza, promueve el siguiente pendiente a activo
        private void PromoteNextPending(ulong guildId)
        {
            if (_pendingMessages.TryGetValue(guildId, out var queue))
            {
                lock (queue)
                {
                    if (queue.TryDequeue(out var next))
                    { 
                        RegisterPlayerMessage(guildId, next.ChannelId, next.MessageId);
                    }
                }
            }
        }

        // Registra el mensaje enviado por /play
        public void RegisterPlayerMessage(ulong guildId, ulong channelId, ulong messageId)
        {
            _activeMessages[guildId] = (channelId, messageId);
        }

        // Encola el mensaje de una canción que aún no está sonando
        public void EnqueuePendingMessage(ulong guildId, ulong channelId, ulong messageId)
        {
            var queue = _pendingMessages.GetOrAdd(guildId, _ => new Queue<(ulong, ulong)>());
            lock (queue) queue.Enqueue((channelId, messageId));
        }

        // Método de limpieza principal
        public async Task CleanPreviousPlayerUiAsync(TrackInfoDto trackInfoDto, ulong guildId)
        {
            if (_activeMessages.TryRemove(guildId, out var info))
            {
                if (await _client.GetChannelAsync(info.ChannelId) is IMessageChannel channel)
                {
                    if (await channel.GetMessageAsync(info.MessageId) is IUserMessage message)
                    {
                        // Remueve la botonera para desactivar el mensaje previo
                        await message.ModifyAsync(msg =>
                        {
                            msg.Components = null;
                            msg.Embed = new EmbedBuilder()
                            .WithColor(new Color(0x7F00FF)) // Morado
                            .WithTitle($"It's over {trackInfoDto.Title} of {trackInfoDto.Autor}")
                            .Build();
                        });
                    }
                }
            }
        }

        public void ClearGuild(ulong guildId)
        {
            _activeMessages.TryRemove(guildId, out _);
            _pendingMessages.TryRemove(guildId, out _);
        }

        private async Task OnTrackEndedAsync(TrackInfoDto trackInfoDto, ulong guildId)
        {
            await CleanPreviousPlayerUiAsync(trackInfoDto, guildId);
            PromoteNextPending(guildId);
            await ModifyUIFirstPending(guildId);
        }

        //Método Auxiliar
        public async Task ModifyUIFirstPending(ulong guildId)
        {
            if (_activeMessages.TryGetValue(guildId, out var info)) // TryGetValue, no Remove
            {
                if (await _client.GetChannelAsync(info.ChannelId) is IMessageChannel channel)
                {
                    if (await channel.GetMessageAsync(info.MessageId) is IUserMessage message)
                    {
                        await message.ModifyAsync(msg =>
                        {
                            msg.Components = MusicComponentBuilder.BuildPlayerComponents();
                        });
                    }
                }
            }
        }


    }
}
