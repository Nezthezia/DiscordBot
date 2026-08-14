using Application.Common.Constants;
using Discord;
using Lavalink4NET.Protocol.Payloads;

namespace DiscordBot.Builders
{
    public static class MusicComponentBuilder
    {
        private static bool _isBucle = false;
        private static bool _isPaused = false;

        public static bool GetIsPaused() => _isPaused;
        public static bool GetIsBucle() => _isBucle;
        public static MessageComponent BuildPlayerComponents(bool isBucle, bool isPaused)
        {
            _isBucle = isBucle;
            _isPaused = isPaused;

            string pauseLabel = _isPaused ? "Reanudar" : "Pausar";
            string bucleLabel = _isBucle ? "Quitar bucle" : "Bucle";

            IEmote pauseEmoji = _isPaused ? new Emoji("▶️") : new Emoji("⏸");
            IEmote bucleEmoji = _isBucle ? new Emoji("🔄") : new Emoji("▶️");

            ButtonStyle pauseStyle = _isPaused ? ButtonStyle.Success : ButtonStyle.Secondary;
            ButtonStyle bucleStyle = _isBucle ? ButtonStyle.Success : ButtonStyle.Secondary;

            string buttonId = _isPaused ? AudioButtonIds.Resume : AudioButtonIds.Pause;
            string buttonBucleId = _isBucle ? AudioButtonIds.Loop : AudioButtonIds.NotLoop;

            var builder = new ComponentBuilder()
                // Fila 1: Controles de reproducción
                .WithButton(pauseLabel, buttonId, pauseStyle, pauseEmoji, row: 0)
                .WithButton(bucleLabel, buttonBucleId, bucleStyle, bucleEmoji, row: 0)
                .WithButton("Skip", AudioButtonIds.Skip, ButtonStyle.Secondary, new Emoji("⏭"), row: 1)
                .WithButton("Stop", AudioButtonIds.Stop, ButtonStyle.Danger, new Emoji("⏹"), row: 1);

            return builder.Build();
        }
    }
}
