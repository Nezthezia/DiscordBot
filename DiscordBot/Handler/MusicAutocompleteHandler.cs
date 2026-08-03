using Discord;
using Discord.Interactions;
using Application.Interfaces;
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

            if (string.IsNullOrWhiteSpace(userInput) || userInput.Length < 3)
                return AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>());

            try
            {
                var audioService = services.GetRequiredService<IAudioService>();
                var tracks = await audioService.SearchTracksAsync(userInput);

                var sugerencias = tracks
                    .Where(t => t.Uri is not null)          // descarta tracks sin URL
                    .Take(25)                                // Discord acepta máximo 25
                    .Select(track =>
                    {
                        var nombre = $"{track.Title} — {track.Author}";
                        // Fix: truncar string correctamente
                        if (nombre.Length > 100) nombre = nombre[..100];

                        return new AutocompleteResult(nombre, track.Uri!.ToString());
                    });

                return AutocompletionResult.FromSuccess(sugerencias);
            }
            catch
            {
                return AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>());
            }
        }
    }
}