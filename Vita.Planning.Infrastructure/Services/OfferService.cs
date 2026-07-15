using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class OfferService : IOfferService
{
    private static readonly Regex QuarterPattern = new(
        @"^Q\s*(?<quarter>[1-4])\s*[-,./_ ]\s*(?<year>\d{4})$|^(?<year2>\d{4})\s*[-,./_ ]\s*Q\s*(?<quarter2>[1-4])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly PlanningDbContext _dbContext;
    private readonly IEntityChangeLogService _changeLog;
    private readonly IResourcePlanEntryHistoryService _historyService;
    private readonly IEconomicProjectNumberAllocator _projectNumberAllocator;
    private readonly IEconomicProjectSourceClient _projectSourceClient;
    private readonly IProjectWorkspaceProvisioningClient _workspaceProvisioningClient;

    public OfferService(
        PlanningDbContext dbContext,
        IEntityChangeLogService changeLog,
        IResourcePlanEntryHistoryService historyService,
        IEconomicProjectNumberAllocator projectNumberAllocator,
        IEconomicProjectSourceClient projectSourceClient,
        IProjectWorkspaceProvisioningClient workspaceProvisioningClient)
    {
        _dbContext = dbContext;
        _changeLog = changeLog;
        _historyService = historyService;
        _projectNumberAllocator = projectNumberAllocator;
        _projectSourceClient = projectSourceClient;
        _workspaceProvisioningClient = workspaceProvisioningClient;
    }

    public async Task<PagedResultDto<OfferDto>> GetAllAsync(int page = 1, int pageSize = 100, string? query = null, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 500);

        var dbQuery = BuildOfferQuery();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            dbQuery = dbQuery.Where(x => x.Title.Contains(q) || x.OfferNumber.Contains(q));
        }

        dbQuery = dbQuery.OrderByDescending(x => x.CreatedAtUtc);
        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<OfferDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<OfferDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var offer = await BuildOfferQuery()
            .Where(x => x.OfferId == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (offer is null)
            return null;

        await ResolveLookupNamesAsync(offer, cancellationToken);
        offer.Partners = await LoadPartnersAsync(id, cancellationToken);
        offer.SegmentIds = await LoadOfferSegmentIdsAsync(id, cancellationToken);
        offer.Segments = await LoadOfferSegmentNamesAsync(id, cancellationToken);
        return offer;
    }

    private IQueryable<OfferDto> BuildOfferQuery() =>
        _dbContext.Offers
            .AsNoTracking()
            .GroupJoin(_dbContext.OfferStatuses.AsNoTracking(),
                o => o.OfferStatusId, s => s.OfferStatusId,
                (o, statuses) => new { o, statuses })
            .SelectMany(x => x.statuses.DefaultIfEmpty(),
                (x, status) => new { x.o, status })
            .GroupJoin(_dbContext.Customers.AsNoTracking(),
                x => x.o.CustomerId, c => c.CustomerId,
                (x, customers) => new { x.o, x.status, customers })
            .SelectMany(x => x.customers.DefaultIfEmpty(),
                (x, customer) => new OfferDto
                {
                    OfferId = x.o.OfferId,
                    OfferNumber = x.o.OfferNumber,
                    Title = x.o.Title,
                    ResponsibleInitials = x.o.ResponsibleInitials,
                    ResponsibleOfficeCode = x.o.ResponsibleOfficeCode,
                    ProjectType = x.o.ProjectType,
                    FeeAmount = x.o.FeeAmount,
                    ExpectedStartYear = x.o.ExpectedStartYear,
                    ExpectedStartQuarter = x.o.ExpectedStartQuarter,
                    ExpectedEndYear = x.o.ExpectedEndYear,
                    ExpectedEndQuarter = x.o.ExpectedEndQuarter,
                    EstimatedTotalHours = x.o.EstimatedTotalHours,
                    WeightedHoursOverride = x.o.WeightedHoursOverride,
                    CalculatedWeightedHours = x.o.WeightedHoursOverride,
                    Notes = x.o.Notes,
                    SizeDescription = x.o.SizeDescription,
                    AddToPqCompetition = x.o.AddToPqCompetition,
                    EstimatedCompetitionStartDate = x.o.EstimatedCompetitionStartDate,
                    PqSubmissionDate = x.o.PqSubmissionDate,
                    DeliveredToPq = x.o.DeliveredToPq,
                    HasRelation = x.o.HasRelation,
                    ConvertedToProjectNumber = x.o.ConvertedToProjectNumber,
                    ConvertedAtUtc = x.o.ConvertedAtUtc,
                    OfferStatusId = x.o.OfferStatusId,
                    OfferStatusCode = x.status == null ? null : x.status.Code,
                    OfferStatusName = x.status == null ? null : x.status.Name,
                    CustomerId = x.o.CustomerId,
                    CustomerName = customer == null ? null : customer.Name,
                    CustomerCvrNumber = customer == null ? null : customer.CvrNumber,
                    CustomerContactId = x.o.CustomerContactId,
                    IsActive = x.o.IsActive,
                    CreatedBy = x.o.CreatedBy,
                    UpdatedBy = x.o.UpdatedBy,
                    CreatedAtUtc = x.o.CreatedAtUtc,
                    UpdatedAtUtc = x.o.UpdatedAtUtc,
                    PlanningCategory = x.o.PlanningCategory,
                    PlanningStatus = x.o.PlanningStatus,
                    DisciplineOwner = x.o.DisciplineOwner,
                    DefaultDescription = x.o.DefaultDescription,
                    ColorTag = x.o.ColorTag,
                    PlanningGroup = x.o.PlanningGroup,
                    Phase = x.o.Phase,
                    ProbabilityPercent = x.o.ProbabilityPercent,
                    LastPlanningReviewBy = x.o.LastPlanningReviewBy,
                    Priority = x.o.Priority,
                    IsBillableForPlanning = x.o.IsBillableForPlanning,
                    IsAbsence = x.o.IsAbsence,
                    IsInternal = x.o.IsInternal,
                    IsProbableCase = x.o.IsProbableCase,
                    IsVisibleInPlanner = x.o.IsVisibleInPlanner,
                    DailyPlanningEnabled = x.o.DailyPlanningEnabled,
                    EntrepriseSum = x.o.EntrepriseSum,
                    EntrepriseForm = x.o.EntrepriseForm,
                    ArealM2 = x.o.ArealM2,
                    Raadgivningsform = x.o.Raadgivningsform,
                    Rolle = x.o.Rolle,
                    ByghherreKontaktperson = x.o.ByghherreKontaktperson,
                    CompetitionFormId = x.o.CompetitionFormId,
                    EnterpriseFormId = x.o.EnterpriseFormId,
                    ConsultantFormId = x.o.ConsultantFormId,
                    ProjectTypeId = x.o.ProjectTypeId,
                    ProjectRoleId = x.o.ProjectRoleId,
                    ComplexityLevelId = x.o.ComplexityLevelId,
                    EngineeringDisciplineId = x.o.EngineeringDisciplineId,
                    OfferCaseUrl = x.o.OfferCaseUrl,
                    OfferCasePath = x.o.OfferCasePath,
                    OfferCaseDriveId = x.o.OfferCaseDriveId,
                    OfferCaseFolderItemId = x.o.OfferCaseFolderItemId,
                    OfferCaseOutlookFolderId = x.o.OfferCaseOutlookFolderId,
                    ProjectArchiveUrl = x.o.ProjectArchiveUrl,
                    ProjectArchiveSiteId = x.o.ProjectArchiveSiteId,
                    ProjectArchiveDriveId = x.o.ProjectArchiveDriveId,
                    ProjectArchiveOutlookFolderId = x.o.ProjectArchiveOutlookFolderId,
                    ProjectDawaId = x.o.ProjectDawaId,
                    ProjectStreetAddress = x.o.ProjectStreetAddress,
                    ProjectPostalCode = x.o.ProjectPostalCode,
                    ProjectCity = x.o.ProjectCity,
                    ProjectMunicipality = x.o.ProjectMunicipality,
                    ProjectRegion = x.o.ProjectRegion
                });

    public async Task<OfferDto> CreateAsync(
        CreateOfferRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        ValidateQuarter(request.ExpectedStartQuarter, nameof(request.ExpectedStartQuarter));
        ValidateQuarter(request.ExpectedEndQuarter, nameof(request.ExpectedEndQuarter));

        var normalizedOfferNumber = request.OfferNumber.Trim().ToUpperInvariant();

        var exists = await _dbContext.Offers
            .AnyAsync(x => x.OfferNumber == normalizedOfferNumber, cancellationToken);

        if (exists)
            throw new InvalidOperationException($"Offer '{normalizedOfferNumber}' already exists.");

        if (request.OfferStatusId.HasValue)
        {
            var statusExists = await _dbContext.OfferStatuses
                .AnyAsync(x => x.OfferStatusId == request.OfferStatusId.Value && x.IsActive, cancellationToken);
            if (!statusExists)
                throw new InvalidOperationException($"OfferStatus with id {request.OfferStatusId.Value} does not exist.");
        }

        await ValidateLookupIdsAsync(request.CompetitionFormId, request.EnterpriseFormId,
            request.ConsultantFormId, request.ProjectTypeId, request.ProjectRoleId,
            request.ComplexityLevelId, request.EngineeringDisciplineId, request.SegmentIds, cancellationToken);

        var actor = ResolveActor(caller);
        var entity = new Offer
        {
            OfferNumber = normalizedOfferNumber,
            Title = request.Title.Trim(),
            ResponsibleInitials = NormalizeNullableUpper(request.ResponsibleInitials),
            ResponsibleOfficeCode = NormalizeNullableUpper(request.ResponsibleOfficeCode),
            ProjectType = NormalizeNullable(request.ProjectType),
            FeeAmount = request.FeeAmount,
            ExpectedStartYear = request.ExpectedStartYear,
            ExpectedStartQuarter = request.ExpectedStartQuarter,
            ExpectedEndYear = request.ExpectedEndYear,
            ExpectedEndQuarter = request.ExpectedEndQuarter,
            EstimatedTotalHours = request.EstimatedTotalHours,
            WeightedHoursOverride = request.WeightedHoursOverride,
            Notes = NormalizeNullable(request.Notes),
            SizeDescription = NormalizeNullable(request.SizeDescription),
            AddToPqCompetition = request.AddToPqCompetition,
            EstimatedCompetitionStartDate = request.EstimatedCompetitionStartDate,
            PqSubmissionDate = request.PqSubmissionDate,
            DeliveredToPq = request.DeliveredToPq,
            HasRelation = request.HasRelation,
            ConvertedToProjectNumber = request.ConvertedToProjectNumber,
            ConvertedAtUtc = request.ConvertedAtUtc,
            OfferStatusId = request.OfferStatusId,
            CustomerId = request.CustomerId,
            CustomerContactId = request.CustomerContactId,
            IsActive = request.IsActive,
            CreatedBy = actor,
            CreatedAtUtc = DateTime.UtcNow,
            PlanningCategory = NormalizeNullable(request.PlanningCategory),
            PlanningStatus = NormalizeNullable(request.PlanningStatus),
            DisciplineOwner = NormalizeNullable(request.DisciplineOwner),
            DefaultDescription = NormalizeNullable(request.DefaultDescription),
            ColorTag = NormalizeNullable(request.ColorTag),
            PlanningGroup = NormalizeNullable(request.PlanningGroup),
            Phase = NormalizeNullable(request.Phase),
            ProbabilityPercent = request.ProbabilityPercent,
            LastPlanningReviewBy = NormalizeNullable(request.LastPlanningReviewBy),
            Priority = request.Priority,
            IsBillableForPlanning = request.IsBillableForPlanning,
            IsAbsence = request.IsAbsence,
            IsInternal = request.IsInternal,
            IsProbableCase = request.IsProbableCase,
            IsVisibleInPlanner = request.IsVisibleInPlanner,
            DailyPlanningEnabled = request.DailyPlanningEnabled,
            EntrepriseSum = request.EntrepriseSum,
            EntrepriseForm = NormalizeNullable(request.EntrepriseForm),
            ArealM2 = request.ArealM2,
            Raadgivningsform = NormalizeNullable(request.Raadgivningsform),
            Rolle = NormalizeNullable(request.Rolle),
            ByghherreKontaktperson = NormalizeNullable(request.ByghherreKontaktperson),
            CompetitionFormId = request.CompetitionFormId,
            EnterpriseFormId = request.EnterpriseFormId,
            ConsultantFormId = request.ConsultantFormId,
            ProjectTypeId = request.ProjectTypeId,
            ProjectRoleId = request.ProjectRoleId,
            ComplexityLevelId = request.ComplexityLevelId,
            EngineeringDisciplineId = request.EngineeringDisciplineId
        };

        _dbContext.Offers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SavePartnersAsync(entity.OfferId, request.Partners, cancellationToken);
        await SyncOfferSegmentsAsync(entity.OfferId, request.SegmentIds, cancellationToken);

        var dto = MapToDto(entity);
        await ResolveOfferStatusNameAsync(dto, cancellationToken);
        dto.Partners = await LoadPartnersAsync(entity.OfferId, cancellationToken);
        dto.SegmentIds = await LoadOfferSegmentIdsAsync(entity.OfferId, cancellationToken);
        dto.Segments = await LoadOfferSegmentNamesAsync(entity.OfferId, cancellationToken);
        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = "OfferCreated",
            EventTitle = $"Tilbud oprettet: {entity.OfferNumber}",
            EntityType = "Offer",
            EntityId = entity.OfferId.ToString(),
            NewValue = $"{entity.Title} | status: {entity.PlanningStatus ?? "-"}",
            NewSnapshot = dto,
            ChangeReason = "CreateOffer",
            Caller = caller,
            SourceModule = "OfferService"
        }, cancellationToken);

        return dto;
    }

    public async Task<OfferDto?> UpdateAsync(
        int id,
        UpdateOfferRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        ValidateQuarter(request.ExpectedStartQuarter, nameof(request.ExpectedStartQuarter));
        ValidateQuarter(request.ExpectedEndQuarter, nameof(request.ExpectedEndQuarter));

        var entity = await _dbContext.Offers
            .FirstOrDefaultAsync(x => x.OfferId == id, cancellationToken);

        if (entity is null)
            return null;

        if (request.OfferStatusId.HasValue)
        {
            var statusExists = await _dbContext.OfferStatuses
                .AnyAsync(x => x.OfferStatusId == request.OfferStatusId.Value && x.IsActive, cancellationToken);
            if (!statusExists)
                throw new InvalidOperationException($"OfferStatus with id {request.OfferStatusId.Value} does not exist.");
        }

        await ValidateLookupIdsAsync(request.CompetitionFormId, request.EnterpriseFormId,
            request.ConsultantFormId, request.ProjectTypeId, request.ProjectRoleId,
            request.ComplexityLevelId, request.EngineeringDisciplineId, request.SegmentIds, cancellationToken);

        var oldSnapshot = MapToDto(entity);
        await ResolveOfferStatusNameAsync(oldSnapshot, cancellationToken);
        var oldValue = $"{entity.Title} | {entity.EstimatedTotalHours:0.##} timer | status: {entity.PlanningStatus ?? "-"}";
        var actor = ResolveActor(caller);

        entity.Title = request.Title.Trim();
        entity.ResponsibleInitials = NormalizeNullableUpper(request.ResponsibleInitials);
        entity.ResponsibleOfficeCode = NormalizeNullableUpper(request.ResponsibleOfficeCode);
        entity.ProjectType = NormalizeNullable(request.ProjectType);
        entity.FeeAmount = request.FeeAmount;
        entity.ExpectedStartYear = request.ExpectedStartYear;
        entity.ExpectedStartQuarter = request.ExpectedStartQuarter;
        entity.ExpectedEndYear = request.ExpectedEndYear;
        entity.ExpectedEndQuarter = request.ExpectedEndQuarter;
        entity.EstimatedTotalHours = request.EstimatedTotalHours;
        entity.WeightedHoursOverride = request.WeightedHoursOverride;
        entity.Notes = NormalizeNullable(request.Notes);
        entity.SizeDescription = NormalizeNullable(request.SizeDescription);
        entity.AddToPqCompetition = request.AddToPqCompetition;
        entity.EstimatedCompetitionStartDate = request.EstimatedCompetitionStartDate;
        entity.PqSubmissionDate = request.PqSubmissionDate;
        entity.DeliveredToPq = request.DeliveredToPq;
        entity.HasRelation = request.HasRelation;
        entity.ConvertedToProjectNumber = request.ConvertedToProjectNumber;
        entity.ConvertedAtUtc = request.ConvertedAtUtc;
        entity.OfferStatusId = request.OfferStatusId;
        entity.CustomerId = request.CustomerId;
        entity.CustomerContactId = request.CustomerContactId;
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = actor;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.PlanningCategory = NormalizeNullable(request.PlanningCategory);
        entity.PlanningStatus = NormalizeNullable(request.PlanningStatus);
        entity.DisciplineOwner = NormalizeNullable(request.DisciplineOwner);
        entity.DefaultDescription = NormalizeNullable(request.DefaultDescription);
        entity.ColorTag = NormalizeNullable(request.ColorTag);
        entity.PlanningGroup = NormalizeNullable(request.PlanningGroup);
        entity.Phase = NormalizeNullable(request.Phase);
        entity.ProbabilityPercent = request.ProbabilityPercent;
        entity.LastPlanningReviewBy = NormalizeNullable(request.LastPlanningReviewBy);
        entity.Priority = request.Priority;
        entity.IsBillableForPlanning = request.IsBillableForPlanning;
        entity.IsAbsence = request.IsAbsence;
        entity.IsInternal = request.IsInternal;
        entity.IsProbableCase = request.IsProbableCase;
        entity.IsVisibleInPlanner = request.IsVisibleInPlanner;
        entity.DailyPlanningEnabled = request.DailyPlanningEnabled;
        entity.EntrepriseSum = request.EntrepriseSum;
        entity.EntrepriseForm = NormalizeNullable(request.EntrepriseForm);
        entity.ArealM2 = request.ArealM2;
        entity.Raadgivningsform = NormalizeNullable(request.Raadgivningsform);
        entity.Rolle = NormalizeNullable(request.Rolle);
        entity.ByghherreKontaktperson = NormalizeNullable(request.ByghherreKontaktperson);
        entity.CompetitionFormId = request.CompetitionFormId;
        entity.EnterpriseFormId = request.EnterpriseFormId;
        entity.ConsultantFormId = request.ConsultantFormId;
        entity.ProjectTypeId = request.ProjectTypeId;
        entity.ProjectRoleId = request.ProjectRoleId;
        entity.ComplexityLevelId = request.ComplexityLevelId;
        entity.EngineeringDisciplineId = request.EngineeringDisciplineId;
        entity.ProjectDawaId = NormalizeNullable(request.ProjectDawaId);
        entity.ProjectStreetAddress = NormalizeNullable(request.ProjectStreetAddress);
        entity.ProjectPostalCode = NormalizeNullable(request.ProjectPostalCode);
        entity.ProjectCity = NormalizeNullable(request.ProjectCity);
        entity.ProjectMunicipality = NormalizeNullable(request.ProjectMunicipality);
        entity.ProjectRegion = NormalizeNullable(request.ProjectRegion);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await SavePartnersAsync(id, request.Partners, cancellationToken);
        await SyncOfferSegmentsAsync(id, request.SegmentIds, cancellationToken);

        var dto = MapToDto(entity);
        await ResolveOfferStatusNameAsync(dto, cancellationToken);
        dto.Partners = await LoadPartnersAsync(id, cancellationToken);
        dto.SegmentIds = await LoadOfferSegmentIdsAsync(id, cancellationToken);
        dto.Segments = await LoadOfferSegmentNamesAsync(id, cancellationToken);
        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = "OfferUpdated",
            EventTitle = $"Tilbud ændret: {entity.OfferNumber}",
            EntityType = "Offer",
            EntityId = entity.OfferId.ToString(),
            OldValue = oldValue,
            NewValue = $"{entity.Title} | {entity.EstimatedTotalHours:0.##} timer | status: {entity.PlanningStatus ?? "-"}",
            OldSnapshot = oldSnapshot,
            NewSnapshot = dto,
            ChangeReason = "UpdateOffer",
            Caller = caller,
            SourceModule = "OfferService"
        }, cancellationToken);

        return dto;
    }

    public async Task<ImportOffersResultDto> ImportAsync(
        Stream fileStream,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");

        var firstRow = worksheet.FirstRowUsed()
                       ?? throw new InvalidOperationException("The worksheet does not contain a header row.");

        var lastColumnNumber = firstRow.LastCellUsed()?.Address.ColumnNumber
                               ?? throw new InvalidOperationException("The worksheet does not contain any columns.");

        var headers = Enumerable.Range(1, lastColumnNumber)
            .Select(columnNumber => new
            {
                ColumnNumber = columnNumber,
                Header = NormalizeHeader(worksheet.Cell(firstRow.RowNumber(), columnNumber).GetString())
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Header))
            .ToDictionary(x => x.Header, x => x.ColumnNumber, StringComparer.OrdinalIgnoreCase);

        var rows = worksheet.RowsUsed().Where(x => x.RowNumber() > firstRow.RowNumber()).ToList();
        var result = new ImportOffersResultDto();

        var offerNumbers = rows
            .Select(row => NormalizeOfferNumber(GetCellString(row, headers, "Tilbudsnr.")))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingOffers = await _dbContext.Offers
            .Where(x => offerNumbers.Contains(x.OfferNumber))
            .ToDictionaryAsync(x => x.OfferNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var offerStatuses = await _dbContext.OfferStatuses
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var offerStatusLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in offerStatuses)
        {
            offerStatusLookup.TryAdd(s.Code, s.OfferStatusId);
            offerStatusLookup.TryAdd(s.Name, s.OfferStatusId);
        }

        var actor = ResolveActor(caller);
        var createdOfferNumbers = new List<string>();
        var updatedOfferNumbers = new List<string>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = row.RowNumber();
            var offerNumber = NormalizeOfferNumber(GetCellString(row, headers, "Tilbudsnr."));

            if (string.IsNullOrWhiteSpace(offerNumber))
            {
                result.SkippedCount++;
                continue;
            }

            try
            {
                var importModel = BuildImportModel(row, headers, offerNumber);

                if (importModel.OfferStatusName != null &&
                    offerStatusLookup.TryGetValue(importModel.OfferStatusName, out var resolvedStatusId))
                {
                    importModel.OfferStatusId = resolvedStatusId;
                }

                if (existingOffers.TryGetValue(offerNumber, out var existingOffer))
                {
                    ApplyImport(existingOffer, importModel, isCreate: false, actor);
                    existingOffer.UpdatedAtUtc = DateTime.UtcNow;
                    updatedOfferNumbers.Add(offerNumber);
                    result.UpdatedCount++;
                    continue;
                }

                var entity = new Offer
                {
                    OfferNumber = offerNumber,
                    CreatedAtUtc = DateTime.UtcNow
                };

                ApplyImport(entity, importModel, isCreate: true, actor);
                _dbContext.Offers.Add(entity);
                existingOffers[offerNumber] = entity;
                createdOfferNumbers.Add(offerNumber);
                result.CreatedCount++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                result.Errors = result.Errors.Append(new ImportOfferErrorDto
                {
                    RowNumber = rowNumber,
                    OfferNumber = offerNumber,
                    Message = ex.Message
                }).ToList();
                result.SkippedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = "OffersImported",
            EventTitle = "Tilbud importeret",
            EntityType = "OfferImport",
            EntityId = Guid.NewGuid().ToString(),
            NewValue = $"{result.CreatedCount} oprettet / {result.UpdatedCount} ændret / {result.SkippedCount} sprunget over",
            NewSnapshot = new
            {
                result.CreatedCount,
                result.UpdatedCount,
                result.SkippedCount,
                result.Errors,
                CreatedOfferNumbers = createdOfferNumbers,
                UpdatedOfferNumbers = updatedOfferNumbers
            },
            ChangeReason = "ImportOffers",
            Caller = caller,
            SourceModule = "OfferImport"
        }, cancellationToken);

        return result;
    }

    public async Task<ConvertOfferToProjectResult> ConvertToProjectAsync(
        int offerId,
        ConvertOfferToProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var offer = await _dbContext.Offers
            .FirstOrDefaultAsync(x => x.OfferId == offerId, cancellationToken);

        if (offer is null)
            throw new InvalidOperationException($"Offer {offerId} not found.");

        if (offer.ConvertedToProjectNumber.HasValue)
            throw new InvalidOperationException(
                $"Offer '{offer.OfferNumber}' has already been converted to project {offer.ConvertedToProjectNumber}.");

        if (request.SubProjectNames.Count > 99)
            throw new InvalidOperationException("A main project can have at most 99 sub-projects.");

        if (!offer.CustomerId.HasValue)
            throw new InvalidOperationException(
                $"Offer '{offer.OfferNumber}' has no customer. Set a customer before converting.");

        var customer = await _dbContext.Customers
            .Where(c => c.CustomerId == offer.CustomerId.Value)
            .Select(c => new { c.Name, c.ExtCustomerNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer?.ExtCustomerNumber is null)
        {
            var name = customer?.Name ?? $"id {offer.CustomerId}";
            throw new InvalidOperationException(
                $"Customer '{name}' does not exist in e-conomic. " +
                "Create the customer in e-conomic first, then run a sync before converting this offer.");
        }

        var economicCustomerNumber = customer.ExtCustomerNumber.Value;
        var projectName = !string.IsNullOrWhiteSpace(request.ProjectName)
            ? request.ProjectName.Trim()
            : offer.Title;

        var mainProjectNumber = await _projectNumberAllocator.CreateMainProjectAsync(
            name: projectName,
            projectGroupNumber: request.ProjectGroupNumber,
            customerNumber: (int?)economicCustomerNumber,
            responsibleEmployeeNumber: request.ResponsibleEmployeeNumber,
            cancellationToken: cancellationToken);

        // Insert the sync mirror row immediately so the FK on core.offers is satisfied right away
        // instead of waiting for the next scheduled sync. GET the project back from e-conomic
        // rather than guessing at its fields from our own create request.
        var stubExists = await _dbContext.Projects
            .AnyAsync(p => p.ProjectNumber == mainProjectNumber, cancellationToken);
        if (!stubExists)
        {
            var source = await _projectSourceClient.GetProjectByNumberAsync(mainProjectNumber, cancellationToken);

            if (source is not null)
            {
                _dbContext.Projects.Add(ExtProjectMapper.ToNewEntity(source));
            }
            else
            {
                _dbContext.Projects.Add(new ExtProject
                {
                    ProjectNumber = mainProjectNumber,
                    ProjectName = projectName,
                    IsMainProject = true,
                    SourceLastSyncedAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var vundedStatusId = await _dbContext.OfferStatuses
            .Where(s => s.Name == "Vundet" && s.IsActive)
            .Select(s => (int?)s.OfferStatusId)
            .FirstOrDefaultAsync(cancellationToken);

        var convertedAt = DateTime.UtcNow;
        offer.ConvertedToProjectNumber = mainProjectNumber;
        offer.ConvertedAtUtc = convertedAt;
        offer.OfferStatusId = vundedStatusId ?? offer.OfferStatusId;
        offer.UpdatedBy = request.ConvertedBy;
        offer.UpdatedAtUtc = convertedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);

        int? resourcePlanEntriesMigrated = null;
        if (request.MigrateResourcePlanEntries)
        {
            resourcePlanEntriesMigrated = await MigrateResourcePlanEntriesAsync(
                offerId, mainProjectNumber, offer.Title, request.ConvertedBy, cancellationToken);
        }

        // SharePoint IDs: prefer values passed by the caller, fall back to what's stored on the offer.
        var offerDriveId = !string.IsNullOrWhiteSpace(request.OfferSharePointDriveId)
            ? request.OfferSharePointDriveId
            : offer.OfferCaseDriveId;

        var offerFolderItemId = !string.IsNullOrWhiteSpace(request.OfferSharePointFolderItemId)
            ? request.OfferSharePointFolderItemId
            : offer.OfferCaseFolderItemId;

        var ownerUpn = request.OwnerUpn;
        if (!request.SkipWorkspaceProvisioning && string.IsNullOrWhiteSpace(ownerUpn))
        {
            ownerUpn = await _dbContext.Users
                .Where(u => u.EmployeeId == 122)
                .Select(u => u.UserPrincipalName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var subProjectsCreated = new List<SubProjectCreatedResult>();
        var subProjectFailures = new List<SubProjectFailure>();

        for (var i = 0; i < request.SubProjectNames.Count; i++)
        {
            var subNumber = mainProjectNumber + i + 1;
            var subName = $"{projectName} - {request.SubProjectNames[i]}";

            if (subProjectFailures.Count > 0)
            {
                subProjectFailures.Add(new SubProjectFailure
                {
                    SubProjectNumber = subNumber,
                    SubProjectName = subName,
                    WasAttempted = false,
                    Error = "Not attempted due to earlier sub-project failure. Create manually in e-conomic."
                });
                continue;
            }

            try
            {
                var actualSubNumber = await _projectNumberAllocator.CreateSubProjectAsync(
                    mainProjectNumber: mainProjectNumber,
                    preferredOffset: i + 1,
                    name: subName,
                    projectGroupNumber: request.ProjectGroupNumber,
                    customerNumber: economicCustomerNumber,
                    responsibleEmployeeNumber: request.ResponsibleEmployeeNumber,
                    cancellationToken: cancellationToken);

                subProjectsCreated.Add(new SubProjectCreatedResult
                {
                    SubProjectNumber = actualSubNumber,
                    SubProjectName = subName
                });
            }
            catch (InvalidOperationException ex)
            {
                subProjectFailures.Add(new SubProjectFailure
                {
                    SubProjectNumber = subNumber,
                    SubProjectName = subName,
                    WasAttempted = true,
                    Error = ex.Message
                });
            }
        }

        ProjectWorkspaceProvisioningResult? workspace = null;
        if (!request.SkipWorkspaceProvisioning)
        {
            try
            {
                workspace = await _workspaceProvisioningClient.ProvisionAsync(
                    new ProjectWorkspaceProvisioningRequest
                    {
                        MainProjectNumber = mainProjectNumber,
                        ProjectName = projectName,
                        IsPrivate = request.IsPrivate,
                        MemberUserIds = request.MemberUserIds,
                        CreateTeam = request.CreateTeam,
                        OwnerAadIdentifier = ownerUpn,
                        OfferSharePointDriveId = offerDriveId,
                        OfferSharePointFolderItemId = offerFolderItemId,
                        InitiatedBy = request.ConvertedBy
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                workspace = new ProjectWorkspaceProvisioningResult
                {
                    Warnings = [$"Workspace provisioning failed: {ex.Message}"]
                };
            }
        }

        await CreateProjectMetadataOnConversionAsync(
            offer, mainProjectNumber, request.ConvertedBy, workspace, cancellationToken);

        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = "OfferConvertedToProject",
            EventTitle = $"Tilbud konverteret til projekt: {offer.OfferNumber} -> {mainProjectNumber}",
            EntityType = "Offer",
            EntityId = offer.OfferId.ToString(),
            OldValue = offer.OfferNumber,
            NewValue = mainProjectNumber.ToString(),
            NewSnapshot = new { offer.OfferId, offer.OfferNumber, MainProjectNumber = mainProjectNumber, ProjectName = projectName, subProjectsCreated },
            ChangeReason = "ConvertOfferToProject",
            Caller = string.IsNullOrWhiteSpace(request.ConvertedBy)
                ? CallerInfo.System
                : new CallerInfo { UserId = request.ConvertedBy, Name = request.ConvertedBy, Email = string.Empty },
            SourceModule = "OfferService"
        }, cancellationToken);

        return new ConvertOfferToProjectResult
        {
            OfferId = offer.OfferId,
            OfferNumber = offer.OfferNumber,
            MainProjectNumber = mainProjectNumber,
            ProjectName = projectName,
            ConvertedAtUtc = convertedAt,
            SubProjectsCreated = subProjectsCreated,
            SubProjectFailures = subProjectFailures,
            ResourcePlanEntriesMigrated = resourcePlanEntriesMigrated,
            Workspace = workspace
        };
    }

    private async Task CreateProjectMetadataOnConversionAsync(
        Offer offer,
        int mainProjectNumber,
        string? convertedBy,
        ProjectWorkspaceProvisioningResult? workspace,
        CancellationToken cancellationToken)
    {
        var projectMeta = new ProjectMetadata
        {
            ProjectNumber = mainProjectNumber,
            OriginalOfferId = offer.OfferId,
            OriginalOfferNumber = offer.OfferNumber,
            BudgetHours = offer.EstimatedTotalHours,
            BudgetRevenue = offer.FeeAmount,
            StartDate = QuarterToStartDate(offer.ExpectedStartYear, offer.ExpectedStartQuarter),
            EndDate = QuarterToEndDate(offer.ExpectedEndYear, offer.ExpectedEndQuarter),
            IsVisibleInPlanner = true,
            // Copy all planning metadata from the offer at the moment of conversion
            PlanningCategory = offer.PlanningCategory,
            PlanningStatus = offer.PlanningStatus,
            DisciplineOwner = offer.DisciplineOwner,
            DefaultDescription = offer.DefaultDescription,
            ColorTag = offer.ColorTag,
            PlanningGroup = offer.PlanningGroup,
            Phase = offer.Phase,
            ProbabilityPercent = offer.ProbabilityPercent,
            LastPlanningReviewBy = offer.LastPlanningReviewBy,
            Priority = offer.Priority,
            IsBillableForPlanning = offer.IsBillableForPlanning,
            IsAbsence = offer.IsAbsence,
            IsInternal = offer.IsInternal,
            IsProbableCase = offer.IsProbableCase,
            DailyPlanningEnabled = offer.DailyPlanningEnabled,
            Notes = offer.Notes,
            SizeDescription = offer.SizeDescription,
            ResponsibleInitials = offer.ResponsibleInitials,
            ResponsibleOfficeCode = offer.ResponsibleOfficeCode,
            EntrepriseSum = offer.EntrepriseSum,
            EntrepriseForm = offer.EntrepriseForm,
            ArealM2 = offer.ArealM2,
            Raadgivningsform = offer.Raadgivningsform,
            Rolle = offer.Rolle,
            ByghherreKontaktperson = offer.ByghherreKontaktperson,
            CompetitionFormId = offer.CompetitionFormId,
            EnterpriseFormId = offer.EnterpriseFormId,
            ConsultantFormId = offer.ConsultantFormId,
            ProjectTypeId = offer.ProjectTypeId,
            ProjectRoleId = offer.ProjectRoleId,
            ComplexityLevelId = offer.ComplexityLevelId,
            EngineeringDisciplineId = offer.EngineeringDisciplineId,
            // Workspace links from the provisioning result
            ProjectArchiveGroupId = workspace?.GroupId,
            ProjectArchiveUrl = workspace?.SharePointSiteUrl,
            ProjectArchiveSiteId = workspace?.SiteId,
            ProjectArchiveDriveId = workspace?.DriveId,
            ProjectArchiveOutlookFolderId = workspace?.OutlookFolderId,
            // Offer case links copied from the offer (historical reference for the project)
            OfferCaseUrl = offer.OfferCaseUrl,
            OfferCasePath = offer.OfferCasePath,
            OfferCaseDriveId = offer.OfferCaseDriveId,
            OfferCaseFolderItemId = offer.OfferCaseFolderItemId,
            OfferCaseOutlookFolderId = offer.OfferCaseOutlookFolderId,
            // Project address — copied from offer at conversion time
            ProjectDawaId = offer.ProjectDawaId,
            ProjectStreetAddress = offer.ProjectStreetAddress,
            ProjectPostalCode = offer.ProjectPostalCode,
            ProjectCity = offer.ProjectCity,
            ProjectMunicipality = offer.ProjectMunicipality,
            ProjectRegion = offer.ProjectRegion,
            CreatedBy = convertedBy,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Write the project archive reference back to the offer (where the project ended up)
        offer.ProjectArchiveUrl = workspace?.SharePointSiteUrl;
        offer.ProjectArchiveSiteId = workspace?.SiteId;
        offer.ProjectArchiveDriveId = workspace?.DriveId;
        offer.ProjectArchiveOutlookFolderId = workspace?.OutlookFolderId;

        _dbContext.ProjectMetadata.Add(projectMeta);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Copy offer segments to the new project's metadata segments
        var offerSegments = await _dbContext.OfferSegments
            .Where(s => s.OfferId == offer.OfferId)
            .ToListAsync(cancellationToken);

        foreach (var seg in offerSegments)
        {
            _dbContext.ProjectMetadataSegments.Add(new ProjectMetadataSegment
            {
                ProjectMetadataId = projectMeta.ProjectMetadataId,
                SegmentId = seg.SegmentId
            });
        }

        if (offerSegments.Count > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateOnly? QuarterToStartDate(int? year, int? quarter)
    {
        if (year is null || quarter is null) return null;
        return new DateOnly(year.Value, (quarter.Value - 1) * 3 + 1, 1);
    }

    private static DateOnly? QuarterToEndDate(int? year, int? quarter)
    {
        if (year is null || quarter is null) return null;
        var month = quarter.Value * 3;
        return new DateOnly(year.Value, month, DateTime.DaysInMonth(year.Value, month));
    }

    private async Task<int> MigrateResourcePlanEntriesAsync(
        int offerId,
        int mainProjectNumber,
        string projectName,
        string? updatedBy,
        CancellationToken cancellationToken)
    {
        var offerTarget = await _dbContext.PlanningTargets
            .FirstOrDefaultAsync(pt => pt.OfferId == offerId, cancellationToken);

        if (offerTarget is null)
            return 0;

        var projectTarget = await _dbContext.PlanningTargets
            .FirstOrDefaultAsync(pt => pt.ExtProjectNumber == mainProjectNumber, cancellationToken);

        if (projectTarget is null)
        {
            projectTarget = new PlanningTarget
            {
                Code = mainProjectNumber.ToString(),
                Name = projectName,
                TargetType = "BillableProject",
                ExtProjectNumber = mainProjectNumber,
                IsActive = true,
                IsPlannable = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.PlanningTargets.Add(projectTarget);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var entries = await _dbContext.ResourcePlanEntries
            .Where(e => e.PlanningTargetId == offerTarget.PlanningTargetId)
            .ToListAsync(cancellationToken);

        if (entries.Count > 0)
        {
            var resourcePlanIds = entries.Select(e => e.ResourcePlanId).Distinct().ToList();
            var resourcePlans = await _dbContext.ResourcePlans
                .Where(rp => resourcePlanIds.Contains(rp.ResourcePlanId))
                .ToDictionaryAsync(rp => rp.ResourcePlanId, cancellationToken);

            var actor = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy;
            var correlationId = Guid.NewGuid();
            var oldPlanningTargetId = offerTarget.PlanningTargetId;
            var newPlanningTargetId = projectTarget.PlanningTargetId;
            var metadataJson = $$"""{"oldPlanningTargetId":{{oldPlanningTargetId}},"newPlanningTargetId":{{newPlanningTargetId}}}""";

            foreach (var entry in entries)
            {
                entry.PlanningTargetId = newPlanningTargetId;
                entry.UpdatedBy = actor;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var entry in entries)
            {
                resourcePlans.TryGetValue(entry.ResourcePlanId, out var plan);

                await _historyService.RecordAsync(new RecordResourcePlanEntryHistoryRequest
                {
                    ResourcePlanEntryId = entry.ResourcePlanEntryId,
                    ResourcePlanId = entry.ResourcePlanId,
                    EmployeeId = plan?.EmployeeId ?? 0,
                    ScenarioId = plan?.ScenarioId ?? 0,
                    PlanningTargetId = newPlanningTargetId,
                    PlanDate = entry.PlanDate,
                    OldHours = entry.Hours,
                    NewHours = entry.Hours,
                    ChangeType = "Updated",
                    ChangeReason = "OfferConvertedToProject",
                    ChangedByUserId = actor,
                    ChangedByName = actor,
                    SourceModule = "OfferService",
                    CorrelationId = correlationId,
                    MetadataJson = metadataJson
                }, cancellationToken);
            }
        }

        offerTarget.IsActive = false;
        offerTarget.IsPlannable = false;
        offerTarget.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entries.Count;
    }

    private static void ValidateQuarter(int? quarter, string fieldName)
    {
        if (quarter.HasValue && (quarter < 1 || quarter > 4))
            throw new InvalidOperationException($"{fieldName} must be between 1 and 4.");
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNullableUpper(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string ResolveActor(CallerInfo caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
            ? caller.UserId
            : !string.IsNullOrWhiteSpace(caller.Email)
                ? caller.Email
                : caller.Name;

    private static OfferDto MapToDto(Offer entity) => new()
    {
        OfferId = entity.OfferId,
        OfferNumber = entity.OfferNumber,
        Title = entity.Title,
        ResponsibleInitials = entity.ResponsibleInitials,
        ResponsibleOfficeCode = entity.ResponsibleOfficeCode,
        ProjectType = entity.ProjectType,
        FeeAmount = entity.FeeAmount,
        ExpectedStartYear = entity.ExpectedStartYear,
        ExpectedStartQuarter = entity.ExpectedStartQuarter,
        ExpectedEndYear = entity.ExpectedEndYear,
        ExpectedEndQuarter = entity.ExpectedEndQuarter,
        EstimatedTotalHours = entity.EstimatedTotalHours,
        WeightedHoursOverride = entity.WeightedHoursOverride,
        CalculatedWeightedHours = null,
        Notes = entity.Notes,
        SizeDescription = entity.SizeDescription,
        AddToPqCompetition = entity.AddToPqCompetition,
        EstimatedCompetitionStartDate = entity.EstimatedCompetitionStartDate,
        PqSubmissionDate = entity.PqSubmissionDate,
        DeliveredToPq = entity.DeliveredToPq,
        HasRelation = entity.HasRelation,
        ConvertedToProjectNumber = entity.ConvertedToProjectNumber,
        ConvertedAtUtc = entity.ConvertedAtUtc,
        OfferStatusId = entity.OfferStatusId,
        OfferStatusCode = null,
        OfferStatusName = null,
        CustomerId = entity.CustomerId,
        CustomerContactId = entity.CustomerContactId,
        IsActive = entity.IsActive,
        CreatedBy = entity.CreatedBy,
        UpdatedBy = entity.UpdatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
        PlanningCategory = entity.PlanningCategory,
        PlanningStatus = entity.PlanningStatus,
        DisciplineOwner = entity.DisciplineOwner,
        DefaultDescription = entity.DefaultDescription,
        ColorTag = entity.ColorTag,
        PlanningGroup = entity.PlanningGroup,
        Phase = entity.Phase,
        ProbabilityPercent = entity.ProbabilityPercent,
        LastPlanningReviewBy = entity.LastPlanningReviewBy,
        Priority = entity.Priority,
        IsBillableForPlanning = entity.IsBillableForPlanning,
        IsAbsence = entity.IsAbsence,
        IsInternal = entity.IsInternal,
        IsProbableCase = entity.IsProbableCase,
        IsVisibleInPlanner = entity.IsVisibleInPlanner,
        DailyPlanningEnabled = entity.DailyPlanningEnabled,
        EntrepriseSum = entity.EntrepriseSum,
        EntrepriseForm = entity.EntrepriseForm,
        ArealM2 = entity.ArealM2,
        Raadgivningsform = entity.Raadgivningsform,
        Rolle = entity.Rolle,
        ByghherreKontaktperson = entity.ByghherreKontaktperson,
        CompetitionFormId = entity.CompetitionFormId,
        EnterpriseFormId = entity.EnterpriseFormId,
        ConsultantFormId = entity.ConsultantFormId,
        ProjectTypeId = entity.ProjectTypeId,
        ProjectRoleId = entity.ProjectRoleId,
        ComplexityLevelId = entity.ComplexityLevelId,
        EngineeringDisciplineId = entity.EngineeringDisciplineId,
        OfferCaseUrl = entity.OfferCaseUrl,
        OfferCasePath = entity.OfferCasePath,
        OfferCaseDriveId = entity.OfferCaseDriveId,
        OfferCaseFolderItemId = entity.OfferCaseFolderItemId,
        OfferCaseOutlookFolderId = entity.OfferCaseOutlookFolderId,
        ProjectArchiveUrl = entity.ProjectArchiveUrl,
        ProjectArchiveSiteId = entity.ProjectArchiveSiteId,
        ProjectArchiveDriveId = entity.ProjectArchiveDriveId,
        ProjectArchiveOutlookFolderId = entity.ProjectArchiveOutlookFolderId,
        ProjectDawaId = entity.ProjectDawaId,
        ProjectStreetAddress = entity.ProjectStreetAddress,
        ProjectPostalCode = entity.ProjectPostalCode,
        ProjectCity = entity.ProjectCity,
        ProjectMunicipality = entity.ProjectMunicipality,
        ProjectRegion = entity.ProjectRegion
    };

    private async Task ResolveLookupNamesAsync(OfferDto dto, CancellationToken ct)
    {
        if (dto.CompetitionFormId.HasValue)
            dto.CompetitionFormName = await _dbContext.CompetitionForms
                .Where(x => x.CompetitionFormId == dto.CompetitionFormId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);

        if (dto.EnterpriseFormId.HasValue)
            dto.EnterpriseFormName = await _dbContext.EnterpriseForms
                .Where(x => x.EnterpriseFormId == dto.EnterpriseFormId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);

        if (dto.ConsultantFormId.HasValue)
            dto.ConsultantFormName = await _dbContext.ConsultantForms
                .Where(x => x.ConsultantFormId == dto.ConsultantFormId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);

        if (dto.ProjectTypeId.HasValue)
            dto.ProjectTypeName = await _dbContext.ProjectTypes
                .Where(x => x.ProjectTypeId == dto.ProjectTypeId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);

        if (dto.ProjectRoleId.HasValue)
            dto.ProjectRoleName = await _dbContext.ProjectRoles
                .Where(x => x.ProjectRoleId == dto.ProjectRoleId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);

        if (dto.ComplexityLevelId.HasValue)
            dto.ComplexityLevelName = await _dbContext.ComplexityLevels
                .Where(x => x.ComplexityLevelId == dto.ComplexityLevelId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);

        if (dto.EngineeringDisciplineId.HasValue)
            dto.EngineeringDisciplineName = await _dbContext.EngineeringDisciplines
                .Where(x => x.EngineeringDisciplineId == dto.EngineeringDisciplineId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
    }

    private async Task<int> FindOrCreatePlanningTargetForOfferAsync(
        int offerId,
        CancellationToken cancellationToken)
    {
        var existingTargetId = await _dbContext.PlanningTargets
            .Where(pt => pt.OfferId == offerId)
            .Select(pt => (int?)pt.PlanningTargetId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingTargetId is not null)
            return existingTargetId.Value;

        var offer = await _dbContext.Offers
            .Where(o => o.OfferId == offerId)
            .Select(o => new { o.OfferNumber, o.Title })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Offer {offerId} not found.");

        var target = new PlanningTarget
        {
            Code = offer.OfferNumber,
            Name = offer.Title,
            TargetType = "Offer",
            OfferId = offerId,
            IsActive = true,
            IsPlannable = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PlanningTargets.Add(target);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return target.PlanningTargetId;
    }

    private async Task SavePartnersAsync(
        int offerId,
        IEnumerable<OfferPartnerRequest> partners,
        CancellationToken cancellationToken)
    {
        var planningTargetId = await FindOrCreatePlanningTargetForOfferAsync(offerId, cancellationToken);

        var existing = await _dbContext.CustomerPartnerRoles
            .Where(x => x.PlanningTargetId == planningTargetId)
            .ToListAsync(cancellationToken);

        _dbContext.CustomerPartnerRoles.RemoveRange(existing);

        foreach (var p in partners)
        {
            _dbContext.CustomerPartnerRoles.Add(new CustomerPartnerRole
            {
                PlanningTargetId = planningTargetId,
                CustomerId = p.CustomerId,
                PlanningPartnerRoleTypeId = p.RoleTypeId,
                CompanyContactId = p.CustomerContactId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<OfferPartnerDto>> LoadPartnersAsync(
        int offerId,
        CancellationToken cancellationToken)
    {
        var planningTargetId = await _dbContext.PlanningTargets
            .Where(pt => pt.OfferId == offerId)
            .Select(pt => (int?)pt.PlanningTargetId)
            .FirstOrDefaultAsync(cancellationToken);

        if (planningTargetId is null)
            return [];

        return await (
            from r in _dbContext.CustomerPartnerRoles.AsNoTracking().Where(x => x.PlanningTargetId == planningTargetId)
            join c in _dbContext.Customers on r.CustomerId equals c.CustomerId
            join rt in _dbContext.PlanningPartnerRoleTypes on r.PlanningPartnerRoleTypeId equals rt.PlanningPartnerRoleTypeId
            from cc in _dbContext.CompanyContacts
                .Where(x => x.CompanyContactId == r.CompanyContactId)
                .DefaultIfEmpty()
            orderby rt.Name
            select new OfferPartnerDto
            {
                OfferPartnerId = r.CustomerPartnerRoleId,
                CustomerId = r.CustomerId,
                CustomerName = c.Name,
                CvrNumber = c.CvrNumber,
                RoleTypeId = rt.PlanningPartnerRoleTypeId,
                RoleTypeName = rt.Name,
                CustomerContactId = r.CompanyContactId,
                ContactPersonName = cc == null ? null : cc.Name,
                ContactPersonEmail = cc == null ? null : cc.Email,
                ContactPersonPhone = cc == null ? null : cc.Phone
            }
        ).ToListAsync(cancellationToken);
    }

    private async Task ValidateLookupIdsAsync(
        int? competitionFormId,
        int? enterpriseFormId,
        int? consultantFormId,
        int? projectTypeId,
        int? projectRoleId,
        int? complexityLevelId,
        int? engineeringDisciplineId,
        IReadOnlyList<int> segmentIds,
        CancellationToken ct)
    {
        if (competitionFormId.HasValue)
        {
            var exists = await _dbContext.CompetitionForms
                .AnyAsync(x => x.CompetitionFormId == competitionFormId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"CompetitionForm with id {competitionFormId.Value} does not exist.");
        }
        if (enterpriseFormId.HasValue)
        {
            var exists = await _dbContext.EnterpriseForms
                .AnyAsync(x => x.EnterpriseFormId == enterpriseFormId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"EnterpriseForm with id {enterpriseFormId.Value} does not exist.");
        }
        if (consultantFormId.HasValue)
        {
            var exists = await _dbContext.ConsultantForms
                .AnyAsync(x => x.ConsultantFormId == consultantFormId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ConsultantForm with id {consultantFormId.Value} does not exist.");
        }
        if (projectTypeId.HasValue)
        {
            var exists = await _dbContext.ProjectTypes
                .AnyAsync(x => x.ProjectTypeId == projectTypeId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ProjectType with id {projectTypeId.Value} does not exist.");
        }
        if (projectRoleId.HasValue)
        {
            var exists = await _dbContext.ProjectRoles
                .AnyAsync(x => x.ProjectRoleId == projectRoleId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ProjectRole with id {projectRoleId.Value} does not exist.");
        }
        if (complexityLevelId.HasValue)
        {
            var exists = await _dbContext.ComplexityLevels
                .AnyAsync(x => x.ComplexityLevelId == complexityLevelId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ComplexityLevel with id {complexityLevelId.Value} does not exist.");
        }
        if (engineeringDisciplineId.HasValue)
        {
            var exists = await _dbContext.EngineeringDisciplines
                .AnyAsync(x => x.EngineeringDisciplineId == engineeringDisciplineId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"EngineeringDiscipline with id {engineeringDisciplineId.Value} does not exist.");
        }
        if (segmentIds.Count > 0)
        {
            var validIds = await _dbContext.Segments
                .Where(x => x.IsActive)
                .Select(x => x.SegmentId)
                .ToListAsync(ct);

            var invalidIds = segmentIds.Except(validIds).ToList();
            if (invalidIds.Count > 0)
                throw new InvalidOperationException($"Segment ids {string.Join(", ", invalidIds)} do not exist.");
        }
    }

    private async Task SyncOfferSegmentsAsync(int offerId, IReadOnlyList<int> segmentIds, CancellationToken ct)
    {
        var existing = await _dbContext.OfferSegments
            .Where(x => x.OfferId == offerId)
            .ToListAsync(ct);

        _dbContext.OfferSegments.RemoveRange(existing);

        foreach (var segId in segmentIds.Distinct())
        {
            _dbContext.OfferSegments.Add(new OfferSegment
            {
                OfferId = offerId,
                SegmentId = segId
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<int>> LoadOfferSegmentIdsAsync(int offerId, CancellationToken ct)
    {
        return await _dbContext.OfferSegments
            .AsNoTracking()
            .Where(x => x.OfferId == offerId)
            .Select(x => x.SegmentId)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<string>> LoadOfferSegmentNamesAsync(int offerId, CancellationToken ct)
    {
        return await _dbContext.OfferSegments
            .AsNoTracking()
            .Where(x => x.OfferId == offerId)
            .Join(_dbContext.Segments, os => os.SegmentId, s => s.SegmentId, (os, s) => s.Name)
            .ToListAsync(ct);
    }

    private async Task ResolveOfferStatusNameAsync(OfferDto dto, CancellationToken ct)
    {
        if (!dto.OfferStatusId.HasValue)
            return;

        var status = await _dbContext.OfferStatuses
            .AsNoTracking()
            .Where(x => x.OfferStatusId == dto.OfferStatusId.Value)
            .Select(x => new { x.Code, x.Name })
            .FirstOrDefaultAsync(ct);

        if (status is null)
            return;

        dto.OfferStatusCode = status.Code;
        dto.OfferStatusName = status.Name;
    }

    private static ImportedOfferRow BuildImportModel(IXLRow row, IReadOnlyDictionary<string, int> headers, string offerNumber)
    {
        var title = GetCellString(row, headers, "Projektnavn") ?? offerNumber;
        var statusText = GetCellString(row, headers, "Status");

        ParseQuarterDate(GetCellString(row, headers, "Forventet start"), out var expectedStartYear, out var expectedStartQuarter);
        ParseQuarterDate(GetCellString(row, headers, "Forventet slut"), out var expectedEndYear, out var expectedEndQuarter);

        var notesParts = new[]
            {
                GetCellString(row, headers, "Bemærkninger"),
                GetCellString(row, headers, "Note")
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ImportedOfferRow
        {
            Title = title,
            ResponsibleInitials = GetCellString(row, headers, "Ansvarlig person"),
            ResponsibleOfficeCode = GetCellString(row, headers, "Ansvarligt kontor"),
            FeeAmount = ParseNullableDecimal(GetCellString(row, headers, "Honorar")),
            WeightedHoursOverride = ParseNullableDecimal(GetCellString(row, headers, "Vægtet Honorar")),
            ExpectedStartYear = expectedStartYear,
            ExpectedStartQuarter = expectedStartQuarter,
            ExpectedEndYear = expectedEndYear,
            ExpectedEndQuarter = expectedEndQuarter,
            Notes = notesParts.Count == 0 ? null : string.Join(Environment.NewLine, notesParts),
            SizeDescription = GetCellString(row, headers, "Størrelse"),
            HasRelation = ParseBoolean(GetCellString(row, headers, "Relation")),
            IsActive = ParseIsActive(GetCellString(row, headers, "Status")),
            OfferStatusName = statusText,
            ConvertedToProjectNumber = ParseNullableInt(GetCellString(row, headers, "Evt. sagsnr., hvis sagen realiseres")),
            ConvertedAtUtc = ParseNullableDateTime(GetCellString(row, headers, "Dato for tabt/vundet/aftale indgået")),
            ProjectType = GetCellString(row, headers, "Projekttype"),
            EstimatedCompetitionStartDate = ParseNullableDateOnly(GetCellString(row, headers, "Anslået opstart af konkurrence")),
            PqSubmissionDate = ParseNullableDateOnly(GetCellString(row, headers, "Dato for aflevering af PQ")),
            DeliveredToPq = ParseNullableBoolean(GetCellString(row, headers, "Leveret til PQ")),
            AddToPqCompetition = ParseBoolean(GetCellString(row, headers, "Er i resplan?"))
        };
    }

    private static void ApplyImport(Offer entity, ImportedOfferRow import, bool isCreate, string actor)
    {
        ValidateQuarter(import.ExpectedStartQuarter, nameof(import.ExpectedStartQuarter));
        ValidateQuarter(import.ExpectedEndQuarter, nameof(import.ExpectedEndQuarter));

        entity.Title = import.Title.Trim();
        entity.ResponsibleInitials = NormalizeNullableUpper(import.ResponsibleInitials);
        entity.ResponsibleOfficeCode = NormalizeNullableUpper(import.ResponsibleOfficeCode);
        entity.ProjectType = NormalizeNullable(import.ProjectType);
        entity.FeeAmount = import.FeeAmount;
        entity.ExpectedStartYear = import.ExpectedStartYear;
        entity.ExpectedStartQuarter = import.ExpectedStartQuarter;
        entity.ExpectedEndYear = import.ExpectedEndYear;
        entity.ExpectedEndQuarter = import.ExpectedEndQuarter;
        entity.EstimatedTotalHours = null;
        entity.WeightedHoursOverride = import.WeightedHoursOverride;
        entity.Notes = NormalizeNullable(import.Notes);
        entity.SizeDescription = NormalizeNullable(import.SizeDescription);
        entity.AddToPqCompetition = import.AddToPqCompetition;
        entity.EstimatedCompetitionStartDate = import.EstimatedCompetitionStartDate;
        entity.PqSubmissionDate = import.PqSubmissionDate;
        entity.DeliveredToPq = import.DeliveredToPq;
        entity.HasRelation = import.HasRelation;
        entity.ConvertedToProjectNumber = import.ConvertedToProjectNumber;
        entity.ConvertedAtUtc = import.ConvertedAtUtc;
        entity.OfferStatusId = import.OfferStatusId;
        entity.IsActive = import.IsActive;

        if (isCreate)
            entity.CreatedBy = actor;
        else
            entity.UpdatedBy = actor;
    }

    private static string NormalizeHeader(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? GetCellString(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(header, out var columnNumber))
            return null;

        var value = row.Cell(columnNumber).GetFormattedString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string NormalizeOfferNumber(string? offerNumber) =>
        string.IsNullOrWhiteSpace(offerNumber) ? string.Empty : offerNumber.Trim().ToUpperInvariant();

    private static decimal? ParseNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("kr.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim('-', ' ', '\t');

        if (string.IsNullOrWhiteSpace(normalized) || normalized == "-" || normalized.StartsWith('#'))
            return null;

        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("da-DK"), out var dkValue))
            return dkValue;

        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var invariantValue))
            return invariantValue;

        return null;
    }

    private static int? ParseNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value.Trim(), out var parsed) ? parsed : null;
    }

    private static DateOnly? ParseNullableDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (int.TryParse(trimmed, out var serial) && serial > 1000 && serial < 100000)
        {
            try { return DateOnly.FromDateTime(DateTime.FromOADate(serial)); }
            catch { return null; }
        }

        if (trimmed.Length == 4 && int.TryParse(trimmed, out _))
            return null;

        if (trimmed.Length == 8 && int.TryParse(trimmed, out _) &&
            DateOnly.TryParseExact(trimmed, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var compact))
            return compact;

        var klIndex = trimmed.IndexOf(" kl.", StringComparison.OrdinalIgnoreCase);
        if (klIndex > 0)
            trimmed = trimmed[..klIndex].Trim();

        if (trimmed.StartsWith('x') || trimmed.StartsWith("xx", StringComparison.OrdinalIgnoreCase))
            return null;

        var daDk = new System.Globalization.CultureInfo("da-DK");

        if (DateOnly.TryParse(trimmed, daDk, System.Globalization.DateTimeStyles.None, out var dateOnly))
            return dateOnly.Year < 1950 ? null : dateOnly;

        if (DateTime.TryParse(trimmed, daDk, System.Globalization.DateTimeStyles.None, out var dateTime))
            return dateTime.Year < 1950 ? null : DateOnly.FromDateTime(dateTime);

        if (DateOnly.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var usDate))
            return usDate.Year < 1950 ? null : usDate;

        return null;
    }

    private static DateTime? ParseNullableDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (int.TryParse(trimmed, out var serial) && serial > 1000 && serial < 100000)
        {
            try { return DateTime.FromOADate(serial); }
            catch { return null; }
        }

        var daDk = new System.Globalization.CultureInfo("da-DK");

        if (DateTime.TryParse(trimmed, daDk, System.Globalization.DateTimeStyles.AssumeLocal, out var parsed))
            return parsed.Year < 1950 ? null : parsed;

        if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var usParsed))
            return usParsed.Year < 1950 ? null : usParsed;

        return null;
    }

    private static void ParseQuarterDate(string? value, out int? year, out int? quarter)
    {
        year = null;
        quarter = null;

        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = value.Trim();
        var quarterMatch = QuarterPattern.Match(normalized);

        if (quarterMatch.Success)
        {
            var quarterGroup = quarterMatch.Groups["quarter"];
            var yearGroup = quarterMatch.Groups["year"];

            if (!quarterGroup.Success || !yearGroup.Success)
            {
                quarterGroup = quarterMatch.Groups["quarter2"];
                yearGroup = quarterMatch.Groups["year2"];
            }

            if (quarterGroup.Success && yearGroup.Success)
            {
                quarter = int.Parse(quarterGroup.Value);
                year = int.Parse(yearGroup.Value);
                return;
            }
        }

        var dateOnly = ParseNullableDateOnly(value);
        if (!dateOnly.HasValue)
            return;

        year = dateOnly.Value.Year;
        quarter = ((dateOnly.Value.Month - 1) / 3) + 1;
    }

    private static bool ParseBoolean(string? value) => ParseNullableBoolean(value) ?? false;

    private static bool? ParseNullableBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "ja" or "x" => true,
            "0" or "false" or "no" or "nej" => false,
            _ => null
        };
    }

    private static bool ParseIsActive(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;

        return status.Trim().ToLowerInvariant() is not ("tabt" or "lost" or "lukket" or "closed");
    }

    private sealed class ImportedOfferRow
    {
        public string Title { get; set; } = string.Empty;
        public string? ResponsibleInitials { get; set; }
        public string? ResponsibleOfficeCode { get; set; }
        public string? ProjectType { get; set; }
        public decimal? FeeAmount { get; set; }
        public int? ExpectedStartYear { get; set; }
        public int? ExpectedStartQuarter { get; set; }
        public int? ExpectedEndYear { get; set; }
        public int? ExpectedEndQuarter { get; set; }
        public decimal? WeightedHoursOverride { get; set; }
        public string? Notes { get; set; }
        public string? SizeDescription { get; set; }
        public bool AddToPqCompetition { get; set; }
        public DateOnly? EstimatedCompetitionStartDate { get; set; }
        public DateOnly? PqSubmissionDate { get; set; }
        public bool? DeliveredToPq { get; set; }
        public bool HasRelation { get; set; }
        public int? ConvertedToProjectNumber { get; set; }
        public DateTime? ConvertedAtUtc { get; set; }
        public int? OfferStatusId { get; set; }
        public string? OfferStatusName { get; set; }
        public bool IsActive { get; set; }
    }
}
