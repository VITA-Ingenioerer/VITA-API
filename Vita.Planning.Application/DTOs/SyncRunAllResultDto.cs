using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Planning.Application.DTOs
{
    public sealed class SyncRunAllResultDto
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();

        public string InitiatedBy { get; init; } = string.Empty;

        public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        public bool Success { get; set; } = true;

        public string? ErrorMessage { get; set; }

        public List<SyncRunAllStepResultDto> Steps { get; init; } = [];
    }

    public sealed class SyncRunAllStepResultDto
    {
        public string Name { get; init; } = string.Empty;

        public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        public bool Success { get; set; }

        public object? Result { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
