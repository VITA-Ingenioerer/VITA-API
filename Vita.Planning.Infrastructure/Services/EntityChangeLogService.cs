using System.Text.Json;
using System.Text.Json.Nodes;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Services;

public sealed class EntityChangeLogService : IEntityChangeLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IBusinessEventService _businessEventService;

    public EntityChangeLogService(IBusinessEventService businessEventService)
    {
        _businessEventService = businessEventService;
    }

    public Task<BusinessEventDto> RecordChangeAsync(
        RecordEntityChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var oldSnapshot = ToJsonNode(request.OldSnapshot);
        var newSnapshot = ToJsonNode(request.NewSnapshot);
        var diff = BuildDiff(oldSnapshot, newSnapshot);

        var payload = new JsonObject
        {
            ["changeReason"] = request.ChangeReason,
            ["oldSnapshot"] = oldSnapshot?.DeepClone(),
            ["newSnapshot"] = newSnapshot?.DeepClone(),
            ["diff"] = diff
        };

        return _businessEventService.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = request.EventType,
            EventTitle = request.EventTitle,
            EventDescription = request.EventDescription,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            PlanningTargetId = request.PlanningTargetId,
            OldValue = request.OldValue,
            NewValue = request.NewValue,
            PayloadJson = payload.ToJsonString(JsonOptions),
            CreatedByUserId = request.Caller.UserId,
            CreatedByName = request.Caller.Name,
            SourceModule = request.SourceModule,
            CorrelationId = request.CorrelationId
        }, cancellationToken);
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value as JsonNode ?? JsonSerializer.SerializeToNode(value, JsonOptions);
    }

    private static JsonArray BuildDiff(JsonNode? oldNode, JsonNode? newNode)
    {
        var diff = new JsonArray();
        AddDiff(diff, "$", oldNode, newNode);
        return diff;
    }

    private static void AddDiff(JsonArray diff, string path, JsonNode? oldNode, JsonNode? newNode)
    {
        if (JsonNode.DeepEquals(oldNode, newNode))
        {
            return;
        }

        if (oldNode is JsonObject oldObject && newNode is JsonObject newObject)
        {
            foreach (var propertyName in oldObject.Select(x => x.Key)
                         .Union(newObject.Select(x => x.Key))
                         .OrderBy(x => x, StringComparer.Ordinal))
            {
                oldObject.TryGetPropertyValue(propertyName, out var oldProperty);
                newObject.TryGetPropertyValue(propertyName, out var newProperty);
                AddDiff(diff, $"{path}.{propertyName}", oldProperty, newProperty);
            }

            return;
        }

        diff.Add(new JsonObject
        {
            ["path"] = path,
            ["oldValue"] = oldNode?.ToJsonString(JsonOptions),
            ["newValue"] = newNode?.ToJsonString(JsonOptions)
        });
    }
}
