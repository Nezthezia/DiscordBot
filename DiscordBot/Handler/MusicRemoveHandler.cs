using Application.Interfaces;
using Discord;
using Discord.Interactions;

namespace DiscordBot.Handler
{
    public class MusicRemoveHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
            IInteractionContext context,
            IAutocompleteInteraction autocompleteInteraction,
            IParameterInfo parameter,
            IServiceProvider services)
        {
            if (context.Guild is null)
                return AutocompletionResult.FromSuccess();

            var audioService = services.GetRequiredService<IAudioService>();
            var queue = await audioService.GetQueueTracksAsync(context.Guild.Id);

            if (queue is null || !queue.Any())
                return AutocompletionResult.FromSuccess();

            string userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

            var suggestions = queue
                .Select((track, index) => new
                {
                    Position = index + 1,
                    DisplayText = $"{index + 1}. {track.Title} - {track.Autor}"
                })
                .Where(x => string.IsNullOrWhiteSpace(userInput) ||
                            x.DisplayText.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                .Take(25) // Discord permite un máximo de 25 opciones
                .Select(x => new AutocompleteResult(
                    name: x.DisplayText.Length > 100 ? x.DisplayText[..97] + "..." : x.DisplayText,
                    value: x.Position
                ));

            return AutocompletionResult.FromSuccess(suggestions);
        }
    }
}
