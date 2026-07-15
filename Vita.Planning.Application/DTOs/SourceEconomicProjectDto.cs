using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

public sealed class SourceEconomicProjectDto
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;

    public int? ProjectGroupNumber { get; set; }
    public NestedNumberDto? ProjectGroup { get; set; }

    public int? MainProjectNumber { get; set; }
    public NestedNumberDto? MainProject { get; set; }

    public int? CustomerNumber { get; set; }
    public NestedNumberDto? Customer { get; set; }

    public int? ContactPersonId { get; set; }

    public int? ResponsibleEmployeeNumber { get; set; }
    public NestedNumberDto? Responsible { get; set; }

    public int? OtherResponsibleEmployeeNumber { get; set; }
    public NestedNumberDto? OtherResponsible { get; set; }

    public int? DepartmentNumber { get; set; }
    public NestedNumberDto? Department { get; set; }

    public int? StatusNumber { get; set; }
    public NestedNumberDto? Status { get; set; }

    public string? Description { get; set; }
    public string? OtherReference { get; set; }

    public DateTime? ClosedDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? LastUpdated { get; set; }

    public bool? IsBarred { get; set; }
    public bool? IsClosed { get; set; }
    public bool? IsMainProject { get; set; }
    public bool? IsMileageInvoiced { get; set; }

    public decimal? Mileage { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SalesPrice { get; set; }
    public decimal? FixedPrice { get; set; }
    public decimal? InvoicedTotal { get; set; }

    public string? ObjectVersion { get; set; }
}

public sealed class NestedNumberDto
{
    public int Number { get; set; }
}