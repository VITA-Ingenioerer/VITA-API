using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CustomerPartnerRoleDto
{
    public int CustomerPartnerRoleId { get; set; }
    public int PlanningTargetId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CvrNumber { get; set; }
    public int PlanningPartnerRoleTypeId { get; set; }
    public string RoleTypeCode { get; set; } = string.Empty;
    public string RoleTypeName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int? CustomerContactId { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? ContactPersonPhone { get; set; }
}

public sealed class UpsertCustomerPartnerRolesRequest
{
    public IReadOnlyList<CustomerRoleItemRequest> Customers { get; set; } = [];
}

public sealed class CustomerRoleItemRequest
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int PlanningPartnerRoleTypeId { get; set; }

    public bool IsPrimary { get; set; }
    public int? CustomerContactId { get; set; }
}

public sealed class CreateCustomerFromCvrRequest
{
    [Required]
    public string CvrNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
}
