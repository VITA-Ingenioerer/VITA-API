using Vita.Planning.Application.DTOs;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

internal static class ExtProjectMapper
{
    public static void ApplyFrom(ExtProject entity, SourceEconomicProjectDto source)
    {
        entity.ProjectName = source.Name.Trim();
        entity.ProjectGroupNumber = source.ProjectGroupNumber ?? source.ProjectGroup?.Number ?? 0;
        entity.MainProjectNumber = source.MainProjectNumber ?? source.MainProject?.Number;
        entity.CustomerNumber = source.CustomerNumber ?? source.Customer?.Number;
        entity.ContactPersonId = source.ContactPersonId;
        entity.ResponsibleEmployeeNumber = source.ResponsibleEmployeeNumber ?? source.Responsible?.Number;
        entity.OtherResponsibleEmployeeNumber = source.OtherResponsibleEmployeeNumber ?? source.OtherResponsible?.Number;
        entity.DepartmentNumber = source.DepartmentNumber ?? source.Department?.Number;
        entity.StatusNumber = source.StatusNumber ?? source.Status?.Number;
        entity.Description = source.Description?.Trim();
        entity.OtherReference = source.OtherReference?.Trim();
        entity.ClosedDate = source.ClosedDate;
        entity.DeliveryDate = source.DeliveryDate;
        entity.LastUpdated = source.LastUpdated;
        entity.IsBarred = source.IsBarred ?? false;
        entity.IsClosed = source.IsClosed ?? false;
        entity.IsMainProject = source.IsMainProject ?? false;
        entity.IsMileageInvoiced = source.IsMileageInvoiced ?? false;
        entity.Mileage = source.Mileage;
        entity.CostPrice = source.CostPrice;
        entity.SalesPrice = source.SalesPrice;
        entity.FixedPrice = source.FixedPrice;
        entity.InvoicedTotal = source.InvoicedTotal;
        entity.ObjectVersion = source.ObjectVersion;
        entity.SourceLastSyncedAt = DateTime.UtcNow;
    }

    public static ExtProject ToNewEntity(SourceEconomicProjectDto source)
    {
        var entity = new ExtProject { ProjectNumber = source.Number };
        ApplyFrom(entity, source);
        return entity;
    }
}
