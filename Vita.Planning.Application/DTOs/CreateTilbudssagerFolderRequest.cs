using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateTilbudssagerFolderRequest
{
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    [MaxLength(50)]
    public string OfferNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// When provided, the created folder's drive ID and item ID are stored on the offer's
    /// ProjectMetadata so they can be looked up automatically at conversion time.
    /// </summary>
    public int? OfferId { get; set; }
}