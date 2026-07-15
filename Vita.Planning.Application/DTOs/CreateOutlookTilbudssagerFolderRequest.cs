using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateOutlookTilbudssagerFolderRequest
{
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    [MaxLength(50)]
    public string OfferNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string ProjectName { get; set; } = string.Empty;

    public int? OfferId { get; set; }
}