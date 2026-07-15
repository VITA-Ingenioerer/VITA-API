using System;
using System.Collections.Generic;
using System.Text;
using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces
{
    public interface IMicrosoftGraphUserSourceClient
    {
        Task<IReadOnlyList<MicrosoftGraphUserDto>> GetUsersAsync(
            CancellationToken cancellationToken = default);
    }
}
