using Application.DTOs;
using Discord;
using System.Collections;

namespace DiscordBot.Builders
{
    public static class MusicEmbedBuilder
    {
        public static Embed BuildPlayerEmbed(TrackInfoDto track, string? avatarUrl = null)
        {
            //string progressBar = GenerarBarraProgreso(track.Position, track.Duration);
            string msgPlaying = !track.IsPlayingNow ? "added" : "Now playing";
            return new EmbedBuilder()
                .WithColor(new Color(0x7F00FF)) // Morado
                .WithAuthor(msgPlaying, avatarUrl)
                .WithTitle($"{track.Autor} - {track.Title}")
                .WithDescription($"Duration: {track.Duration}")
                //.WithUrl(track.Uri)
                //.WithThumbnailUrl(track.ArtworkUri)
                //.WithDescription($"• Added by {track.RequestedByMention}\n• 🔊 `{track.ChannelName}`")
                //.AddField("Queue Size", track.QueueSize.ToString(), inline: true)
                //.AddField("Volume", $"{track.Volume}%", inline: true)
                //.AddField("Loop", track.LoopMode, inline: true)
                //.AddField("\u200B", $"{progressBar}\n`{track.Position:mm\\:ss} / {track.Duration:mm\\:ss}`")
                .Build();
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
