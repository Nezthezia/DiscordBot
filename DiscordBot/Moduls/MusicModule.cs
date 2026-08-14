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

        public MusicModule(IAudioService audioService, PlayerUiService playerUiService)
        {
            _audioService = audioService;
            _playerUiService = playerUiService;
        }

        [SlashCommand("play", "Busca y reproduce una canción, o la añade a la cola")]
        public async Task PlayCommandAsync(
            [Autocomplete(typeof(MusicAutocompleteHandler))]
            [Summary("busqueda", "Nombre de la canción o URL de Deezer/Spotify")] string busqueda)
        {
            await DeferAsync();

            var voiceChannel = (Context.Guild.GetUser(Context.User.Id))?.VoiceChannel;
            if (voiceChannel == null)
            {
                await FollowupAsync("❌ ¡Debes estar en un canal de voz para que pueda cantar para ti!");
                return;
            }

            try
            {
                var trackInfoDto = await _audioService.PlayAsync(Context.Guild.Id, voiceChannel.Id, busqueda);

                string avatarUrl = Context.Client.CurrentUser.GetAvatarUrl();
                var embed = MusicEmbedBuilder.BuildPlayerEmbed(trackInfoDto, avatarUrl);
                IUserMessage response;

                if (!trackInfoDto.IsPlayingNow)
                {
                    response = await FollowupAsync(embed: embed);
                    _playerUiService.EnqueuePendingMessage(
                        Context.Guild.Id, Context.Channel.Id, response.Id, trackInfoDto);
                }
                else
                {
                    await _playerUiService.CleanActivePlayerUiAsync(Context.Guild.Id);

                    var components = MusicComponentBuilder.BuildPlayerComponents(
                        MusicComponentBuilder.GetIsBucle(),
                        MusicComponentBuilder.GetIsPaused());
                    response = await FollowupAsync(embed: embed, components: components);
                    _playerUiService.RegisterPlayerMessage(
                        Context.Guild.Id, Context.Channel.Id, response.Id, trackInfoDto);
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al intentar reproducir: {ex.Message}");
            }
        }

        [SlashCommand("skip", "Salta la canción que se está reproduciendo actualemente")]
        public async Task SkipCommandAsync()
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _playerUiService.CleanActivePlayerUiAsync(
                    Context.Guild.Id,
                    customTitle: null 
                );

                await _audioService.SkipAsync(Context.Guild.Id);

                await _playerUiService.PromoteAndActivateNextAsync(Context.Guild.Id);

                await FollowupAsync("⏭️ ¡Canción saltada!");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al saltar la canción: {ex.Message}");
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
                MessageComponent components = MusicComponentBuilder.BuildPlayerComponents(
                    MusicComponentBuilder.GetIsBucle(),
                    isPaused: true);

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
                MessageComponent components = MusicComponentBuilder.BuildPlayerComponents(
                    MusicComponentBuilder.GetIsBucle(),
                    isPaused: false);

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

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _playerUiService.CleanActivePlayerUiAsync(
                    Context.Guild.Id,
                    customTitle: null 
                );
                _playerUiService.ClearGuild(Context.Guild.Id);
                await _audioService.StopAsync(Context.Guild.Id);

                await FollowupAsync("😭 Se acabó la fiesta. ¡Hasta la próxima!");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al detener la reproducción: {ex.Message}");
            }
        }


        [SlashCommand("loop", "Hacer que una cancion se repita en bucle")]
        public async Task LoopCommandAsync()
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.LoopAsync(Context.Guild.Id);
                await _playerUiService.UpdateLoopUiAsync(Context.Guild.Id, isBucle: true);

                await FollowupAsync("🔄 Música en bucle");
            }
            catch(Exception ex)
            {
                await FollowupAsync($"❌ Error al detener la reproducción: {ex.Message}");
            }
        }


        [SlashCommand("unloop", "Desactivar la repetición en bucle")]
        public async Task UnloopCommandAsync()
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.NotLoopAsync(Context.Guild.Id);
                await _playerUiService.UpdateLoopUiAsync(Context.Guild.Id, isBucle: false);

                await FollowupAsync("▶️ Bucle desactivado.");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al detener la reproducción: {ex.Message}");
            }
        }
    }
}
