using Application.DTOs;
using Discord;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections;
using System.Diagnostics;
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

            string loopMode = MusicComponentBuilder.GetIsBucle() ? "On" : "Off";

            track = track with
            {
                LoopMode = loopMode
            };

            var description = new StringBuilder()
                .AppendLine($"• Added by {track.RequestedByMention}")
                .AppendLine($"• 🔊 **{track.ChannelName}**")
                .AppendLine()
                .AppendLine($"Queue Size: `{track.QueueSize}` · Volume: `{track.Volume}%` · Loop: `{loopMode}`")
                .AppendLine()
                .AppendLine(progressBar)
                .AppendLine(formatted);

            var builder = new EmbedBuilder()
                .WithColor(new Color(0x7F00FF)) // Morado
                .WithAuthor(msgPlaying, avatarUrl)
                .WithTitle($"{track.Autor} - {track.Title}")
                .WithDescription(description.ToString());

            if (!string.IsNullOrEmpty(track.Uri))
                builder.WithUrl(track.Uri);

            if (!string.IsNullOrEmpty(track.ArtworkUri))
                builder.WithThumbnailUrl(track.ArtworkUri);

            return builder.Build();
        }

        public static Embed[] BuildListPlayerEmbed(List<TrackInfoDto>? musics, int page = 1, int pageSize = 10, string? avatarUrl = null)
        {
            if(musics!.Count == 0)
            {
                return
                [
                    new EmbedBuilder()
                        .WithAuthor("Lista", avatarUrl)
                        .WithTitle("Sin reproducciones")
                        .WithColor(Color.Purple)
                        .Build()
                ];
            }

            var currentTrack = musics[0];

            if (musics!.Count == 1)
            {
                string currentDurationStr = currentTrack.Duration.TotalHours >= 1
                ? currentTrack.Duration.ToString(@"hh\:mm\:ss")
                : currentTrack.Duration.ToString(@"mm\:ss");

                var singleEmbed = new EmbedBuilder()
                    .WithTitle("Now playing")
                    .WithDescription($"**{currentTrack.Autor} - {currentTrack.Title}** - `{currentDurationStr}`\n\n Requested by {currentTrack.RequestedByMention}")
                    .WithColor(new Color(114, 137, 218));

                if (!string.IsNullOrWhiteSpace(currentTrack.ArtworkUri))
                    singleEmbed.WithThumbnailUrl(currentTrack.ArtworkUri);

                return 
                    [
                    singleEmbed.Build() 
                ];
            }

            var queueTracks = musics.Skip(1).ToList();
            int totalTracks = queueTracks.Count;
            TimeSpan totalDuration = TimeSpan.FromMilliseconds(queueTracks.Sum(t => t.Duration.TotalMilliseconds));

            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalTracks / pageSize));
            page = Math.Clamp(page, 1, totalPages);

            string nowPlayingDuration = currentTrack.Duration.TotalHours >= 1
                ? currentTrack.Duration.ToString(@"hh\:mm\:ss")
                : currentTrack.Duration.ToString(@"mm\:ss");

            var nowPlayingEmbed = new EmbedBuilder()
                .WithTitle("Now playing")
                .WithDescription($"**{currentTrack.Autor} - {currentTrack.Title}** - `{nowPlayingDuration}`\n\n Requested by {currentTrack.RequestedByMention}")
                .WithColor(new Color(114, 137, 218));

            if (!string.IsNullOrWhiteSpace(currentTrack.ArtworkUri))
                nowPlayingEmbed.WithThumbnailUrl(currentTrack.ArtworkUri);

            int startIndex = (page - 1) * pageSize;
            var pageTracks = queueTracks.Skip(startIndex).Take(pageSize).ToList();

            var sb = new StringBuilder();
            for (int i = 0; i < pageTracks.Count; i++)
            {
                var track = pageTracks[i];
                int trackNumber = startIndex + i + 1; // Inicia la cola numerada en 1
                string durationStr = track.Duration.TotalHours >= 1
                    ? track.Duration.ToString(@"hh\:mm\:ss")
                    : track.Duration.ToString(@"mm\:ss");

                sb.AppendLine($"**{trackNumber}.** {track.Title} - `{durationStr}`");
                if (!string.IsNullOrWhiteSpace(track.Autor))
                {
                    sb.AppendLine($"{track.Autor}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"Page {page}/{totalPages}");

            string totalDurationFormatted = totalDuration.TotalHours >= 1
                ? totalDuration.ToString(@"hh\:mm\:ss")
                : totalDuration.ToString(@"mm\:ss");

            var queueEmbed = new EmbedBuilder()
                .WithTitle($"({totalTracks}) songs in queue for {totalDurationFormatted}")
                .WithDescription(sb.ToString())
                .WithColor(new Color(114, 137, 218))
                .Build();

            return [nowPlayingEmbed.Build(), queueEmbed];
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
