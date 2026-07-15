namespace Vita.Planning.Application.DTOs;

public sealed class CapacityDefaultsSettings
{
    public decimal BaselineWeeklyHours { get; set; } = 37.0m;
    public decimal Monday { get; set; } = 7.5m;
    public decimal Tuesday { get; set; } = 7.5m;
    public decimal Wednesday { get; set; } = 7.5m;
    public decimal Thursday { get; set; } = 7.5m;
    public decimal Friday { get; set; } = 7.0m;
    public decimal Saturday { get; set; } = 0m;
    public decimal Sunday { get; set; } = 0m;
}
