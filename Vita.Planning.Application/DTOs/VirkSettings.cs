namespace Vita.Planning.Application.DTOs;

public sealed class VirkSettings
{
    public string BrugerID { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "http://distribution.virk.dk";
}
