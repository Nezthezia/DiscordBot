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
    }
}
