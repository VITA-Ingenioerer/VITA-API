using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Planning.Application.DTOs
{

    public sealed class MicrosoftGraphSettings
    {
        public string TenantId { get; init; } = string.Empty;

        public string ClientId { get; init; } = string.Empty;

        public string ClientSecret { get; init; } = string.Empty;
    }
}
