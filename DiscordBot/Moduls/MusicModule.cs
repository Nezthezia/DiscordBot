using Application.DTOs;
using Application.Interfaces;
using Discord;
using Discord.Interactions;
using DiscordBot.Builders;
using DiscordBot.Handler;
using DiscordBot.Services;
using Infrastructure.Services;

namespace DiscordBot.Moduls
{
    public class MusicModule : InteractionModuleBase<SocketInteractionContext>
    {

        private readonly IAudioService _audioService;
        private readonly PlayerUiService _playerUiService;
        private TrackInfoDto trackInfoDto;

        public MusicModule(IAudioService audioService, PlayerUiService playerUiService)
        {
            _audioService = audioService;
            _playerUiService = playerUiService;
            trackInfoDto = new("","",new TimeSpan(0,0,0),false);
        }

        [SlashCommand("play", "Busca y reproduce una canción, o la añade a la cola")]
        public async Task PlayCommandAsync(
            [Autocomplete(typeof(MusicAutocompleteHandler))]
            [Summary("busqueda", "Nombre de la canción o URL de Deezer/Spotify")] string busqueda)
        {
            await DeferAsync();

            var user = Context.Guild.GetUser(Context.User.Id);
            var voiceChannel = user?.VoiceChannel;

            if (voiceChannel == null)
            {
                await FollowupAsync("❌ ¡Debes estar en un canal de voz para que pueda cantar para ti!");
                return;
            }

            try
            {
                if(trackInfoDto.Title != "")
                  await _playerUiService.CleanPreviousPlayerUiAsync(trackInfoDto, Context.Guild.Id);

                trackInfoDto = await _audioService.PlayAsync(Context.Guild.Id, voiceChannel.Id, busqueda);

                string avatarUrl = Context.Client.CurrentUser.GetAvatarUrl();
                Embed embed = MusicEmbedBuilder.BuildPlayerEmbed(trackInfoDto, avatarUrl);
                MessageComponent components = MusicComponentBuilder.BuildPlayerComponents();
                IUserMessage response;

                if (!trackInfoDto.IsPlayingNow)
                {
                    response = await FollowupAsync(embed: embed);
                    _playerUiService.EnqueuePendingMessage(Context.Guild.Id, Context.Channel.Id, response.Id);
                }
                else
                {
                    response = await FollowupAsync(embed: embed, components: components);
                    _playerUiService.RegisterPlayerMessage(Context.Guild.Id, Context.Channel.Id, response.Id);
                }
            }
            catch (Exception ex)
            {
                // Por si algo truena (Lavalink apagado, problemas de red, etc.)
                await FollowupAsync($"Error al intentar reproducir: {ex.Message}");
            }
        }

        [SlashCommand("skip", "Salta la canción que se está reproduciendo actualemente")]
        public async Task SkipCommandAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            var voiceChannel = user?.VoiceChannel;

            if (voiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                //  Llamamos al método real de la infraestructura
                await _audioService.SkipAsync(Context.Guild.Id);

                await FollowupAsync("¡Canción saltada con éxito!");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error al intentar saltar la canción: {ex.Message}");
            }
        }

        [SlashCommand("list", "Muestra la lista de canciones en la cola de reproducción")]
        public async Task ListCommandAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            var voiceChannel = user?.VoiceChannel;

            if (voiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                IEnumerable<string> colaStrings = await _audioService.GetQueueAsync(Context.Guild.Id);
                var lista = colaStrings.ToList();
                string avatarUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var embed = MusicEmbedBuilder.BuildListPlayerEmbed(lista, avatarUrl);

                await FollowupAsync(embed: embed);

                /*if (lista.Count == 0)
                {
                    await FollowupAsync("La cola está vacía.");
                    return;
                }

                if (lista.Count == 1 && lista[0].StartsWith("SONANDO AHORA:"))
                {
                    await FollowupAsync($"🎵 {lista[0]}\n\nNo hay canciones en cola.");
                    return;
                }

                string mensaje = "**Estado de la reproducción:**\n";

                foreach (var item in lista)
                {
                    if (item.StartsWith("SONANDO AHORA:"))
                    {
                        // La pintamos con esteroides en el mensaje
                        mensaje += $"\n{item}\n\n**Siguientes en la cola:**";
                    }
                    else
                    {
                        // Las demás van numeradas normal
                        mensaje += $"\n• {item}";
                    }
                }*/

                //await FollowupAsync(mensaje);
            }
            catch (Exception ex)
            {
                // 🟢 Corregido el mensaje de error para que corresponda a la lista
                await FollowupAsync($"Error al intentar obtener la lista de reproducción: {ex.Message}");
            }
        }

        [SlashCommand("pause", "Pausa la reproducción actual de música")]
        public async Task PauseCommandAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            if (user?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.PauseAsync(Context.Guild.Id);

                // Actualiza el componente a estado pausado (isPaused: true)
                MessageComponent components = MusicComponentBuilder.BuildPlayerComponents(isPaused: true);

                await FollowupAsync("⏸️ Reproducción pausada.", components: components);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error al intentar pausar: {ex.Message}");
            }
        }

        [SlashCommand("resume", "Reanuda la reproducción de música pausada")]
        public async Task ResumeCommandAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            if (user?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.ResumeAsync(Context.Guild.Id);

                // Actualiza el componente a estado reproduciendo (isPaused: false)
                MessageComponent components = MusicComponentBuilder.BuildPlayerComponents(isPaused: false);

                await FollowupAsync("▶️ Reproducción reanudada.", components: components);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error al intentar reanudar: {ex.Message}");
            }
        }

        [SlashCommand("stop", "Detiene el seguir reproduciendo musica con el bot")]
        public async Task StopCommandAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            var voiceChannel = user?.VoiceChannel;

            if (voiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {

                await _audioService.StopAsync(Context.Guild.Id);
                _playerUiService.ClearGuild(Context.Guild.Id);

                await FollowupAsync("Deteniendo el bot para reproducir musica");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error al intentar detener la reproduccion: {ex.Message}");
            }
        }

    }
}
