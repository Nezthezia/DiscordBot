using Application.Common.Constants;
using Discord;

namespace DiscordBot.Builders
{
    public static class MusicComponentBuilder
    {
        public static MessageComponent BuildPlayerComponents(bool isPaused = false)
        {
            string pauseLabel = isPaused ? "Reanudar" : "Pausar";
            IEmote pauseEmoji = isPaused ? new Emoji("▶️") : new Emoji("⏸");
            ButtonStyle pauseStyle = isPaused ? ButtonStyle.Success : ButtonStyle.Secondary;
            string buttonId = isPaused ? AudioButtonIds.Resume : AudioButtonIds.Pause;

            var builder = new ComponentBuilder()
                // Fila 1: Controles de reproducción
                .WithButton(pauseLabel, buttonId, pauseStyle, pauseEmoji, row: 0)
                .WithButton("Skip", AudioButtonIds.Skip, ButtonStyle.Secondary, new Emoji("⏭"), row: 0)
                .WithButton("Stop", AudioButtonIds.Stop, ButtonStyle.Danger, new Emoji("⏹"), row: 0);

            return builder.Build();
        }
    }
}
