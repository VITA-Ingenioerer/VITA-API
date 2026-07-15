namespace Vita.Planning.Application.DTOs;

public sealed class CustomerDto
{
    public int CustomerId { get; set; }
    public int? ExtCustomerNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerStatus { get; set; } = string.Empty;
    public string CustomerSource { get; set; } = string.Empty;
    public string? CvrNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? CvrStatus { get; set; }
    public string? IndustryCode { get; set; }
    public string? IndustryName { get; set; }
    public string? SourceReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
