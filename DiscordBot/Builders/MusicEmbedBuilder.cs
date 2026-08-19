using Application.DTOs;
using Discord;
using System.Collections;
using System.Text;

namespace DiscordBot.Builders
{
    public static class MusicEmbedBuilder
    {
        private static string GenerarBarraProgreso(TimeSpan current, TimeSpan total, int length = 20)
        {
            // Si no hay duración (ej. un stream en vivo o error), mostramos el botón al inicio
            if (total <= TimeSpan.Zero)
                return "🔘" + new string('▬', length - 1);

            // Calculamos el porcentaje de progreso (de 0.0 a 1.0)
            double progress = Math.Clamp(current.TotalMilliseconds / total.TotalMilliseconds, 0.0, 1.0);

            // Determinamos en qué posición del string debe ir el círculo
            int position = (int)Math.Round(progress * (length - 1));

            var sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                // 🔘 para la bolita del slider, ▬ para la línea continua
                sb.Append(i == position ? "🔘" : "▬");
            }

            return sb.ToString();
        }

        public static Embed BuildPlayerEmbed(TrackInfoDto track, string? avatarUrl = null)
        {
            string msgPlaying = !track.IsPlayingNow ? "Added to queue" : "Now playing";
            string progressBar = !track.IsPlayingNow ? string.Empty : GenerarBarraProgreso(track.Position, track.Duration);

            // Formato para minutos y horas
            string formattedCurrent = track.Position.TotalHours >= 1
                ? track.Position.ToString(@"hh\:mm\:ss")
                : track.Position.ToString(@"m\:ss");

            string formattedTotal = track.Duration.TotalHours >= 1
                ? track.Duration.ToString(@"hh\:mm\:ss")
                : track.Duration.ToString(@"m\:ss");

            string formatted = !track.IsPlayingNow ? $"Duration: {formattedTotal}" : 
                $"`{formattedCurrent}`" + new string(' ', 28) + $"`{formattedTotal}`";

            var description = new StringBuilder()
                //.AppendLine($"• Added by {track.RequestedByMention}")
                //.AppendLine($"• 🔊 **{track.ChannelName}**")
                .AppendLine()
                //.AppendLine($"Queue Size: `{track.QueueSize}` · Volume: `{track.Volume}%` · Loop: `{track.LoopMode}`")
                .AppendLine()
                .AppendLine(progressBar)
                .AppendLine(formatted);

            var builder = new EmbedBuilder()
                .WithColor(new Color(0x7F00FF)) // Morado
                .WithAuthor(msgPlaying, avatarUrl)
                .WithTitle($"{track.Autor} - {track.Title}")
                .WithDescription(description.ToString());

            /*if (!string.IsNullOrEmpty(track.Uri))
                builder.WithUrl(track.Uri);

            if (!string.IsNullOrEmpty(track.ArtworkUri))
                builder.WithThumbnailUrl(track.ArtworkUri);*/

            return builder.Build();
        }

        public static Embed BuildListPlayerEmbed(List<string>? musics, string? avatarUrl = null)
        {
            if(musics!.Count == 0)
            {
                return new EmbedBuilder().WithColor(new Color(0x7F00FF))
                .WithAuthor("Lista", avatarUrl)
                .WithTitle("Sin reproducciones")
                .Build();
            }

            if(musics!.Count == 1)
            {
                return new EmbedBuilder().WithColor(new Color(0x7F00FF))
                .WithAuthor("Lista", avatarUrl)
                .WithTitle($"{musics[0]}.")
                .Build();
            }

            string msg = ""; 

            for (int i = 1; i < musics.Count; i++)
            {
                msg += $"{musics[i]}.\n"; 
            }
            return new EmbedBuilder().WithColor(new Color(0x7F00FF))
                .WithAuthor("Lista", avatarUrl)
                .WithTitle($"{musics[0]}.\nDespues:\n")
                .WithDescription($"{msg}")
                .Build();
        }

        public static Embed BuildTrackNotFoundEmbed(int position)
        {
            return new EmbedBuilder()
                    .WithColor(new Color(0xE74C3C)) // Rojo error
                    .WithTitle("❌ Canción no encontrada")
                    .WithDescription($"No existe ninguna canción en la posición **#{position}** de la lista.")
                    .WithFooter("Verifica la posición con la lista de reproducción e inténtalo de nuevo.")
                    .WithCurrentTimestamp()
                    .Build();
        }

        public static Embed BuildTrackRemovedEmbed(TrackInfoDto? removedTrack, int position)
        {
            return new EmbedBuilder()
                .WithColor(new Color(0xE74C3C)) // Rojo carmesí
                .WithTitle("🗑️ Canción Eliminada de la Cola")
                .WithDescription($"Se ha quitado **{removedTrack?.Title}** de la lista.")
                .AddField("👤 Artista / Autor", removedTrack?.Autor, inline: true)
                .AddField("📍 Posición", $"#{position}", inline: true)
                .AddField("⏱️ Duración", $"{removedTrack?.Duration:mm\\:ss}", inline: true)
                .WithFooter("Gestión de cola • Bot de Música")
                .WithCurrentTimestamp()
                .Build();
        }

        public static Embed BuildShuffleEmbed()
        {
            return new EmbedBuilder()
                .WithColor(new Color(0x9B59B6)) // Morado / Púrpura
                .WithTitle("🔀 Cola Mezclada")
                .WithDescription("Se ha reordenado aleatoriamente la lista de espera.")
                .WithFooter("Gestión de Cola • Bot de Música")
                .WithCurrentTimestamp()
                .Build();
        }

        public static Embed BuildVolumeEmbed(int volume)
        {
            int clampedVolume = Math.Clamp(volume, 0, 100);

            string icon = clampedVolume switch
            {
                0 => "🔇",
                <= 30 => "🔈",
                <= 70 => "🔉",
                _ => "🔊"
            };

            return new EmbedBuilder()
                .WithColor(new Color(0x3498DB)) // Azul
                .WithTitle($"{icon} Volumen Actualizado")
                .WithDescription($"El volumen del reproductor se ha ajustado a **{clampedVolume}%**.")
                .WithFooter("Control de Audio • Bot de Música")
                .WithCurrentTimestamp()
                .Build();
        }
    }
}
