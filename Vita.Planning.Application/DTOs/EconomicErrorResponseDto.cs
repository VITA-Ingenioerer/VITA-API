namespace Vita.Planning.Application.DTOs;

public sealed class EconomicErrorResponseDto
{
    public string? ErrorCode { get; set; }
    public List<EconomicErrorDetailDto>? Errors { get; set; }
}

public sealed class EconomicErrorDetailDto
{
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
}
