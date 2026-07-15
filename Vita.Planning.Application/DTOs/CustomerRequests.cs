using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateCustomerRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string CustomerSource { get; set; } = "Local";

    public string? CvrNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? CvrStatus { get; set; }
    public string? IndustryCode { get; set; }
    public string? IndustryName { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class UpdateCustomerRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string CustomerStatus { get; set; } = "Active";

    public string? CvrNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? CvrStatus { get; set; }
    public string? IndustryCode { get; set; }
    public string? IndustryName { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class LinkEconomicCustomerRequest
{
    public int ExtCustomerNumber { get; set; }
    public string? UpdatedBy { get; set; }
}
