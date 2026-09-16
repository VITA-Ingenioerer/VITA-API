using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

public sealed class UpdateUserNoteRequest
{
    /// <summary>Free text; blank clears the note.</summary>
    [MaxLength(1000)]
    public string? Note { get; set; }
}
