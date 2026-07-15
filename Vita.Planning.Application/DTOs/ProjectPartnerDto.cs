namespace Vita.Planning.Application.DTOs;

public sealed class ProjectPartnerDto
{
    public int CustomerPartnerRoleId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CvrNumber { get; set; }
    public int RoleTypeId { get; set; }
    public string RoleTypeName { get; set; } = string.Empty;
    public int? CustomerContactId { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? ContactPersonPhone { get; set; }
}
