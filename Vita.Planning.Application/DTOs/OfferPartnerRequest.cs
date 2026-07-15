namespace Vita.Planning.Application.DTOs;

public sealed class OfferPartnerRequest
{
    public int CustomerId { get; set; }
    public int RoleTypeId { get; set; }
    public int? CustomerContactId { get; set; }
}
