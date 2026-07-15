using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Planning.Application.DTOs
{
    public sealed class MicrosoftGraphUserDto
    {
        public string Id { get; init; } = string.Empty;

        public string? EmployeeId { get; init; }

        public string? UserPrincipalName { get; init; }

        public string? Mail { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string? OfficeLocation { get; init; }

        public string? Department { get; init; }

        public string? EmployeeType { get; init; }

        public string? JobTitle { get; init; }

        public string? MobilePhone { get; init; }

        public bool? AccountEnabled { get; init; }

        public string? ManagerId { get; set; }

        public string? ManagerUserPrincipalName { get; set; }

        public string? ManagerDisplayName { get; set; }
    }
}
