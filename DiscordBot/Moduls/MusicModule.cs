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
                _playerUiService.SetGuildChannel(Context.Guild.Id, Context.Channel.Id);

                string avatarUrl = Context.Client.CurrentUser.GetAvatarUrl();
                var embed = MusicEmbedBuilder.BuildPlayerEmbed(trackInfoDto, avatarUrl);
                IUserMessage response;

                if (!trackInfoDto.IsPlayingNow)
                {
                    response = await FollowupAsync(embed: embed);
                    _playerUiService.RegisterPlayerMessage(
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

                //await _playerUiService.PromoteAndActivateNextAsync(Context.Guild.Id);

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

        [SlashCommand("clear", "Limpiar lista de canciones")]
        public async Task ClearCommandAsync()
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.ClearListAsync(Context.Guild.Id);
                await _playerUiService.ClearQueueUiAsync(Context.Guild.Id);

                await FollowupAsync("✅ lista limpia.");
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al limpiar la lista de musica: {ex.Message}");
            }
        }

        [SlashCommand("remove", "Elimina una cancion de la lista")]
        public async Task RemoveCommandAsync(
            [Autocomplete(typeof(MusicRemoveHandler))]
            [Summary("Indice", "Nombre de la canción a eliminar")] int posicion)
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                var music = await _audioService.RemoveMusicAsync(Context.Guild.Id, posicion);
                var embed = await _playerUiService.RemoveMusicUIAsync(music, posicion);

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al eliminar un elemento de la lista de musica: {ex.Message}");
            }
        }

        [SlashCommand("shuffle", "Reordena la musica de manera aleatoria")]
        public async Task ShuffleCommandAsync()
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.ShuffleTracksAsync(Context.Guild.Id);
                var embed = await _playerUiService.ShuffleUiAsync();

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al modificar la musica de manera aleatoria: {ex.Message}");
            }
        }

        [SlashCommand("volume", "Sube el volumen del reproductor")]
        public async Task VolumeCommandAsync(
        [Summary("porcentaje", "Nivel de volumen de 0 a 100")]
        [MinValue(0), MaxValue(100)] int volumen)
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                await _audioService.VolumeAsync(Context.Guild.Id, volumen);
                var embed = await _playerUiService.VolumeUiAsync(volumen);

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al modificar el volumen: {ex.Message}");
            }
        }

        [SlashCommand("seek", "Adelanta o atrasa la canción actual a un tiempo específico")]
        public async Task SeekCommandAsync(
        [Summary("minutos", "Minutos a los que deseas saltar")][MinValue(0)] int minutos = 0,
        [Summary("segundos", "Segundos a los que deseas saltar")][MinValue(0), MaxValue(59)] int segundos = 0)
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {
                var targetTime = new TimeSpan(0, 0, minutos, segundos);

                await _audioService.SeekTrackAsync(Context.Guild.Id, targetTime);

                var embed = await _playerUiService.SeekUiAsync(Context.Guild.Id, targetTime);

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al colocar la posicion en la cancion: {ex.Message}");
            }
        }


        [SlashCommand("previous", "Vuelve a la canción anterior o reinicia la actual")]
        public async Task PreviousCommandAsync()
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {

                bool success = await _audioService.PreviousTrackAsync(Context.Guild.Id);

                var embed = await _playerUiService.PreviousUiAsync(success);

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al regresar a la anterior cancion: {ex.Message}");
            }
        }

        [SlashCommand("move", "Mueve una canción a otra posición dentro de la lista de espera")]
        public async Task MoveCommandAsync(
        [Autocomplete(typeof(MusicRemoveHandler))]
        [Summary("origen", "Posición actual de la canción en la cola")] [MinValue(1)] int origen,
        [Summary("destino", "Nueva posición a la que deseas moverla")][MinValue(1)] int destino)
        {
            await DeferAsync();

            if ((Context.User as IGuildUser)?.VoiceChannel == null)
            {
                await FollowupAsync("❌ Debes estar en un canal de voz para usar este comando.");
                return;
            }

            try
            {

                var track = await _audioService.MoveTrackAsync(Context.Guild.Id, origen, destino);

                var embed = await _playerUiService.MoveTrackUiAsync(track, origen, destino);

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"❌ Error al mover la cancion de posicion: {ex.Message}");
            }
        }

    }
}
