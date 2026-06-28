using Application.Interfaces;
using Discord;
using Discord.Interactions;
using DiscordBot.Handler;
using Infrestructure.Services;

namespace DiscordBot.Moduls
{
    public class MusicModule : InteractionModuleBase<SocketInteractionContext>
    {

        private readonly IAudioService _audioService;

        public MusicModule(IAudioService audioService)
        {
            _audioService = audioService;
        }

        [SlashCommand("play", "Busca y reproduce una canción, o la añade a la cola")]
        public async Task PlayCommandAsync(
            [Autocomplete(typeof(MusicAutocompleteHandler))]
            [Summary("busqueda", "Nombre de la canción o URL de YouTube/Spotify")] string busqueda)
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
                await _audioService.PlayAsync(Context.Guild.Id, voiceChannel.Id, busqueda);

                await FollowupAsync($"Procesado con éxito: `{busqueda}` añadido a la cola.");
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

                if (lista.Count <= 1)
                {
                    await FollowupAsync("La cola está vacía.");
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
                }

                await FollowupAsync(mensaje);
            }
            catch (Exception ex)
            {
                // 🟢 Corregido el mensaje de error para que corresponda a la lista
                await FollowupAsync($"Error al intentar obtener la lista de reproducción: {ex.Message}");
            }
        }
    }
}
