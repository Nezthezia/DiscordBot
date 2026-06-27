using Discord.Interactions;
using Discord;

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
            // 1. Obtener lo que el usuario está escribiendo en tiempo real
            string userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

            // 2. Tu lista de sugerencias (En el futuro, aquí llamarás a tu capa Application/Lavalink)
            var listaSugerencias = new List<AutocompleteResult>
        {
            new AutocompleteResult("Never Gonna Give You Up - Rick Astley", "ytsearch:never gonna give you up"),
            new AutocompleteResult("Chop Suey! - System of a Down", "ytsearch:chop suey"),
            new AutocompleteResult("Blinding Lights - The Weeknd", "ytsearch:blinding lights"),
            new AutocompleteResult("In The End - Linkin Park", "ytsearch:in the end")
        };

            // 3. Filtrar las sugerencias según lo que el usuario ya escribió
            var filtradas = listaSugerencias
                .Where(x => x.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                .Take(25); // Discord permite un máximo de 25 opciones

            // 4. Retornar los resultados a Discord
            return AutocompletionResult.FromSuccess(filtradas);
        }
    }
}
