using Discord;
using Discord.Interactions;

namespace DiscordBot.Moduls
{
    public class GeneralModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly InteractionService _interactionService;

        public GeneralModule(InteractionService interactionService)
        {
            _interactionService = interactionService;
        }

        [SlashCommand("hola", "prueba de bot abierto")]
        public async Task HolaCommandAsync()
        {
            await RespondAsync($"¡Hola {Context.User.Mention}!");
        }

        [SlashCommand("help", "Te da los comandos que tiene el bot")]
        public async Task HelpCommandAsync()
        {
            var mensaje = "**Lista de comandos disponibles en este bot:**\n\n";

            // 🚀 Aquí ocurre tu foreach interactivo
            foreach (var comando in _interactionService.SlashCommands)
            {
                mensaje += $"/**{comando.Name}** - *{comando.Description}*\n";
            }

            await RespondAsync(mensaje);
        }

    }
}
