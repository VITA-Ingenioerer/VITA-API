namespace Vita.Planning.Application.DTOs;

public sealed class ProjectPartnerRequest
{
    public int CustomerId { get; set; }
    public int RoleTypeId { get; set; }
    public int? CustomerContactId { get; set; }
}
