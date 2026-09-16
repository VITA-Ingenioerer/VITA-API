using System;
using System.Collections.Generic;
using System.Text;
using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces
{
    public interface IMicrosoftGraphUserSourceClient
    {
        Task<IReadOnlyList<MicrosoftGraphUserDto>> GetUsersAsync(
            CancellationToken cancellationToken = default);
    }
}
