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
        bool IsPlayingNow
    );
}
