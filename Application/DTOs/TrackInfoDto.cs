using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs
{
    public readonly record struct TrackInfoDto
(
    string Autor,
    string Title,
    TimeSpan Duration,
    TimeSpan Position, // <--- Tiempo transcurrido actual
    bool IsPlayingNow,

    // Propiedades adicionales necesarias para tu Embed:
    string? RequestedByMention = null,
    string? ChannelName = null,
    int QueueSize = 0,
    int Volume = 100,
    string LoopMode = "Off",
    string? Uri = null,
    string? ArtworkUri = null
);
}
