using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IBotCommandService
    {
        Task<string?> ProcessMessageAsync(string content, string username, string userMention);
    }
}
