using Discord;
using Discord.Interactions;
using Infrestructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Handler
{
    public class MusicAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
        {
            string userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

            // Para no saturar a la API de YouTube, no busques si ha escrito menos de 3 letras
            if (string.IsNullOrWhiteSpace(userInput) || userInput.Length < 3)
            {
                return AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>());
            }

            try
            {
                var audioService = services.GetRequiredService<LavalinkAudioService>();

                // Buscamos los tracks reales en Lavalink
                var tracksEncontrados = await audioService.SearchTracksAsync(userInput);

                // Convertimos los tracks de Lavalink al formato que entiende Discord
                var sugerencias = tracksEncontrados.Select(track =>
                    new AutocompleteResult(
                        name: $"{track.Title} ({track.Author})".Take(100).ToString(), // Discord limita a 100 caracteres el nombre
                        value: track.Uri!.ToString() // 👈 Guardamos la URL real como valor
                    ));

                return AutocompletionResult.FromSuccess(sugerencias);
            }
            catch
            {
                // Si algo falla (ej. Lavalink apagado), devolvemos una lista vacía limpiamente
                return AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>());
            }
        }
    }
}
