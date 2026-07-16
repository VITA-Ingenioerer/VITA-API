using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class ProjectWorkspaceProvisioningClient : IProjectWorkspaceProvisioningClient
{
    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _graphSettings;
    private readonly ProjectWorkspaceSettings _workspaceSettings;
    private readonly IProjectLifecycleLogService _lifecycleLogService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProjectWorkspaceProvisioningClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> graphOptions,
        IOptions<ProjectWorkspaceSettings> workspaceOptions,
        IProjectLifecycleLogService lifecycleLogService)
    {
        _httpClient = httpClient;
        _graphSettings = graphOptions.Value;
        _workspaceSettings = workspaceOptions.Value;
        _lifecycleLogService = lifecycleLogService;
    }

    /// <summary>
    /// Best-effort append to core.project_lifecycle_log. A logging failure must
    /// never abort the actual Graph provisioning it's describing.
    /// </summary>
    private async Task LogStepAsync(
        int mainProjectNumber,
        string? initiatedBy,
        string eventType,
        string status,
        string? message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _lifecycleLogService.CreateAsync(new CreateProjectLifecycleLogRequest
            {
                TargetType = "Project",
                ProjectNumber = mainProjectNumber,
                EventType = eventType,
                EventTitle = $"{eventType}: {status}",
                EventDescription = message,
                NewValue = status,
                CreatedBy = initiatedBy
            }, cancellationToken);
        }
        catch
        {
            // Best-effort — swallow so a logging hiccup never masks the real provisioning result.
        }
    }

    public async Task<ProjectWorkspaceProvisioningResult> ProvisionAsync(
        ProjectWorkspaceProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateGraphSettings();

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var warnings = new List<string>();
        var mainProjectNumber = request.MainProjectNumber;
        var initiatedBy = request.InitiatedBy;

        // Step 1: Create M365 Group — auto-provisions SharePoint site at /sites/{mailNickname}
        string groupId;
        try
        {
            groupId = await CreateGroupAsync(accessToken, request, cancellationToken);
            await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateGroup", "Success", groupId, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateGroup", "Failed", ex.Message, cancellationToken);
            throw;
        }

        // Step 2: Create Teams team on the group (best-effort; group may not be fully settled yet)
        string? teamsId = null;
        if (request.CreateTeam)
        {
            try
            {
                teamsId = await CreateTeamAsync(accessToken, groupId, cancellationToken);
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateTeam", "Success", teamsId, cancellationToken);
            }
            catch (Exception ex)
            {
                warnings.Add($"Teams team creation failed — create it manually in a few minutes: {ex.Message}");
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateTeam", "Failed", ex.Message, cancellationToken);
            }
        }

        // Step 3: Add members (best-effort per member; only meaningful for private groups)
        if (request.MemberUserIds.Count > 0)
        {
            var memberFailures = new List<string>();
            foreach (var userId in request.MemberUserIds)
            {
                try
                {
                    await AddMemberAsync(accessToken, groupId, userId, cancellationToken);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not add member '{userId}': {ex.Message}");
                    memberFailures.Add($"{userId}: {ex.Message}");
                }
            }

            var addedCount = request.MemberUserIds.Count - memberFailures.Count;
            await LogStepAsync(
                mainProjectNumber,
                initiatedBy,
                "WorkspaceStep:AddMembers",
                memberFailures.Count == 0 ? "Success" : "PartialFailure",
                $"{addedCount}/{request.MemberUserIds.Count} added." +
                    (memberFailures.Count > 0 ? $" Failures: {string.Join("; ", memberFailures)}" : string.Empty),
                cancellationToken);
        }

        // Step 4: Get the project SharePoint site ID and drive ID (best-effort; site may still be provisioning).
        // If the site is not ready yet, the URL will be null — the background poller fills it in later.
        var hasOfferFolder = !string.IsNullOrWhiteSpace(request.OfferSharePointDriveId)
            && !string.IsNullOrWhiteSpace(request.OfferSharePointFolderItemId);

        var (projectSiteId, projectSiteDriveId, projectSiteWebUrl) = await TryGetGroupSiteAndDriveAsync(accessToken, groupId, cancellationToken);

        // Step 5: Copy offer folder contents to project site (offer conversions only)
        bool offerFolderCopied = false;
        bool workbookCopied = false;

        if (hasOfferFolder && projectSiteDriveId is not null)
        {
            // 5a: Copy entire offer folder into D1 Grundlag/{offer folder name}
            try
            {
                await CopyOfferFolderToD1GrundlagAsync(
                    accessToken,
                    projectSiteDriveId,
                    request.OfferSharePointDriveId!,
                    request.OfferSharePointFolderItemId!,
                    cancellationToken);
                offerFolderCopied = true;
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CopyOfferFolder", "Success", _workspaceSettings.OfferArchiveFolderPath, cancellationToken);
            }
            catch (Exception ex)
            {
                warnings.Add($"Offer folder copy to '{_workspaceSettings.OfferArchiveFolderPath}' failed: {ex.Message}");
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CopyOfferFolder", "Failed", ex.Message, cancellationToken);
            }

            // 5b: Copy economics workbook to C03 Økonomi/C03.10 VITA Projektøkonomi/
            try
            {
                await CopyWorkbookToProjectSiteAsync(
                    accessToken,
                    projectSiteDriveId,
                    request.OfferSharePointDriveId!,
                    request.OfferSharePointFolderItemId!,
                    cancellationToken);
                workbookCopied = true;
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CopyWorkbook", "Success", _workspaceSettings.EconomicsWorkbookFolderPath, cancellationToken);
            }
            catch (Exception ex)
            {
                warnings.Add($"Workbook copy to '{_workspaceSettings.EconomicsWorkbookFolderPath}' failed: {ex.Message}");
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CopyWorkbook", "Failed", ex.Message, cancellationToken);
            }
        }
        else if (hasOfferFolder)
        {
            const string skippedMessage = "SharePoint site not yet provisioned — copy offer folder and workbook manually.";
            warnings.Add(
                "Offer folder not copied — SharePoint site not yet provisioned. " +
                "Copy the offer folder to 'D1 Grundlag' and " +
                $"'{_workspaceSettings.EconomicsWorkbookFileName}' to " +
                $"'{_workspaceSettings.EconomicsWorkbookFolderPath}' manually.");
            await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CopyOfferFolder", "Skipped", skippedMessage, cancellationToken);
            await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CopyWorkbook", "Skipped", skippedMessage, cancellationToken);
        }

        // Step 6: Create folder in project mailbox (e.g., 26-mail@vitaing.dk)
        // Year is encoded in the project number: 26000100 / 1_000_000 = 26 → 2000 + 26 = 2026.
        string? outlookFolderId = null;
        var projectYear = 2000 + request.MainProjectNumber / 1_000_000;
        var mailboxEmail = _workspaceSettings.GetMailboxEmail(projectYear);
        if (!string.IsNullOrWhiteSpace(mailboxEmail))
        {
            try
            {
                outlookFolderId = await CreateProjectMailFolderAsync(
                    accessToken,
                    mailboxEmail,
                    request.MainProjectNumber,
                    request.ProjectName,
                    cancellationToken);
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateOutlookFolder", "Success", outlookFolderId, cancellationToken);
            }
            catch (Exception ex)
            {
                warnings.Add($"Outlook folder creation failed: {ex.Message}");
                await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateOutlookFolder", "Failed", ex.Message, cancellationToken);
            }
        }
        else
        {
            // Every other skip/failure path in this method leaves a trace; this one didn't —
            // ProjectWorkspace:ProjectMailboxEmailTemplate missing/blank for this year silently
            // produced no folder and no record of why, which is exactly what made it look like
            // nothing was even attempted.
            const string skippedMessage = "Outlook folder not created — ProjectWorkspace:ProjectMailboxEmailTemplate is not configured for this year.";
            warnings.Add(skippedMessage);
            await LogStepAsync(mainProjectNumber, initiatedBy, "WorkspaceStep:CreateOutlookFolder", "Skipped", skippedMessage, cancellationToken);
        }

        return new ProjectWorkspaceProvisioningResult
        {
            GroupId = groupId,
            SiteId = projectSiteId,
            DriveId = projectSiteDriveId,
            SharePointSiteUrl = projectSiteWebUrl,
            TeamsId = teamsId,
            OutlookFolderId = outlookFolderId,
            OfferFolderCopied = offerFolderCopied,
            WorkbookCopied = workbookCopied,
            Warnings = warnings
        };
    }

    private async Task<string> CreateGroupAsync(
        string accessToken,
        ProjectWorkspaceProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["displayName"] = $"{request.MainProjectNumber} - {request.ProjectName}",
            ["mailNickname"] = request.MainProjectNumber.ToString(),
            ["mailEnabled"] = true,
            ["securityEnabled"] = false,
            ["groupTypes"] = new[] { "Unified" },
            ["visibility"] = request.IsPrivate ? "Private" : "Public"
        };

        // Look up the owner via Graph to get a verified object ID.
        // Falls back to DefaultOwnerUserId from settings if no identifier was resolved.
        var ownerIdentifier = request.OwnerAadIdentifier
            ?? _workspaceSettings.DefaultOwnerUserId;

        if (!string.IsNullOrWhiteSpace(ownerIdentifier))
        {
            var ownerObjectId = await LookupUserObjectIdAsync(accessToken, ownerIdentifier, cancellationToken);
            if (ownerObjectId is not null)
            {
                payload["owners@odata.bind"] = new[]
                {
                    $"https://graph.microsoft.com/v1.0/users/{ownerObjectId}"
                };
            }
        }

        var json = JsonSerializer.Serialize(payload);

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/groups");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create M365 Group. StatusCode: {(int)response.StatusCode}. Body: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var group = await JsonSerializer.DeserializeAsync<GraphIdResponse>(stream, JsonOptions, cancellationToken);

        if (string.IsNullOrWhiteSpace(group?.Id))
            throw new InvalidOperationException("Microsoft Graph returned an empty group ID after creation.");

        return group.Id;
    }

    private async Task<string?> CreateTeamAsync(
        string accessToken,
        string groupId,
        CancellationToken cancellationToken)
    {
        // Teams team creation often fails immediately after group creation because
        // the group hasn't fully propagated in Azure AD. Retry once after a short wait.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);

            var url = $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(groupId)}/team";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Put, url);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var team = await JsonSerializer.DeserializeAsync<GraphIdResponse>(stream, JsonOptions, cancellationToken);
                return team?.Id;
            }

            // 404 / 400 / 409 = group not yet fully provisioned, retry
            if (response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.BadRequest
                or HttpStatusCode.Conflict)
            {
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create Teams team. StatusCode: {(int)response.StatusCode}. Body: {body}");
        }

        throw new InvalidOperationException(
            "Failed to create Teams team — the group is still provisioning. Create the team manually from Teams.");
    }

    private async Task AddMemberAsync(
        string accessToken,
        string groupId,
        string userId,
        CancellationToken cancellationToken)
    {
        var url = $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(groupId)}/members/$ref";
        var json = $$"""{"@odata.id": "https://graph.microsoft.com/v1.0/directoryObjects/{{userId}}"}""";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"StatusCode: {(int)response.StatusCode}. Body: {body}");
        }
    }

    public async Task<(string? siteId, string? driveId, string? siteWebUrl)> TryGetGroupSiteAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        return await TryGetGroupSiteAndDriveAsync(accessToken, groupId, cancellationToken);
    }

    private async Task<(string? siteId, string? driveId, string? siteWebUrl)> TryGetGroupSiteAndDriveAsync(
        string accessToken,
        string groupId,
        CancellationToken cancellationToken)
    {
        string? siteId = null;
        string? driveId = null;
        string? siteWebUrl = null;

        var siteUrl = $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(groupId)}/sites/root?$select=id,webUrl";
        using var siteRequest = new HttpRequestMessage(HttpMethod.Get, siteUrl);
        siteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var siteResponse = await _httpClient.SendAsync(siteRequest, cancellationToken);
        if (siteResponse.IsSuccessStatusCode)
        {
            await using var siteStream = await siteResponse.Content.ReadAsStreamAsync(cancellationToken);
            var site = await JsonSerializer.DeserializeAsync<GraphSiteResponse>(siteStream, JsonOptions, cancellationToken);
            siteId = string.IsNullOrWhiteSpace(site?.Id) ? null : site.Id;
            siteWebUrl = string.IsNullOrWhiteSpace(site?.WebUrl) ? null : site.WebUrl;
        }

        var driveUrl = $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(groupId)}/sites/root/drive?$select=id";
        using var driveRequest = new HttpRequestMessage(HttpMethod.Get, driveUrl);
        driveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var driveResponse = await _httpClient.SendAsync(driveRequest, cancellationToken);
        if (driveResponse.IsSuccessStatusCode)
        {
            await using var driveStream = await driveResponse.Content.ReadAsStreamAsync(cancellationToken);
            var drive = await JsonSerializer.DeserializeAsync<GraphIdResponse>(driveStream, JsonOptions, cancellationToken);
            driveId = string.IsNullOrWhiteSpace(drive?.Id) ? null : drive.Id;
        }

        return (siteId, driveId, siteWebUrl);
    }

    private async Task CopyOfferFolderToD1GrundlagAsync(
        string accessToken,
        string projectSiteDriveId,
        string offerDriveId,
        string offerFolderItemId,
        CancellationToken cancellationToken)
    {
        // Resolve the offer folder name so the copy keeps it under D1 Grundlag/{name}
        var offerFolderName = await GetDriveItemNameAsync(
            accessToken, offerDriveId, offerFolderItemId, cancellationToken);

        // Ensure D1 Grundlag exists in the project site
        var d1GrundlagItemId = await EnsureFolderPathAsync(
            accessToken, projectSiteDriveId, _workspaceSettings.OfferArchiveFolderPath, cancellationToken);

        // Copy the entire offer folder (Graph copies all children recursively)
        var copyUrl =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(offerDriveId)}" +
            $"/items/{Uri.EscapeDataString(offerFolderItemId)}/copy";

        var copyPayload = new Dictionary<string, object>
        {
            ["parentReference"] = new Dictionary<string, string>
            {
                ["driveId"] = projectSiteDriveId,
                ["id"] = d1GrundlagItemId
            },
            ["name"] = offerFolderName
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, copyUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(copyPayload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to copy offer folder '{offerFolderName}' to " +
                $"'{_workspaceSettings.OfferArchiveFolderPath}'. " +
                $"StatusCode: {(int)response.StatusCode}. Body: {body}");
        }
    }

    private async Task<string> GetDriveItemNameAsync(
        string accessToken,
        string driveId,
        string itemId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(driveId)}" +
            $"/items/{Uri.EscapeDataString(itemId)}?$select=id,name";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to get drive item info. StatusCode: {(int)response.StatusCode}. Body: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var item = await JsonSerializer.DeserializeAsync<GraphNamedItemResponse>(
            stream, JsonOptions, cancellationToken);

        return item?.Name
            ?? throw new InvalidOperationException("Microsoft Graph returned no name for the offer folder item.");
    }

    private async Task CopyWorkbookToProjectSiteAsync(
        string accessToken,
        string projectSiteDriveId,
        string offerDriveId,
        string offerFolderItemId,
        CancellationToken cancellationToken)
    {
        var workbookItemId = await FindWorkbookInOfferFolderAsync(
            accessToken, offerDriveId, offerFolderItemId, cancellationToken);

        if (workbookItemId is null)
            throw new InvalidOperationException(
                $"'{_workspaceSettings.EconomicsWorkbookFileName}' not found in the offer folder.");

        var targetFolderItemId = await EnsureFolderPathAsync(
            accessToken, projectSiteDriveId, _workspaceSettings.EconomicsWorkbookFolderPath, cancellationToken);

        var copyUrl =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(offerDriveId)}" +
            $"/items/{Uri.EscapeDataString(workbookItemId)}/copy";

        var copyPayload = new Dictionary<string, object>
        {
            ["parentReference"] = new Dictionary<string, string>
            {
                ["driveId"] = projectSiteDriveId,
                ["id"] = targetFolderItemId
            },
            ["name"] = _workspaceSettings.EconomicsWorkbookFileName
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, copyUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(copyPayload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to copy workbook. StatusCode: {(int)response.StatusCode}. Body: {body}");
        }
    }

    private async Task<string?> FindWorkbookInOfferFolderAsync(
        string accessToken,
        string offerDriveId,
        string offerFolderItemId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(offerDriveId)}" +
            $"/items/{Uri.EscapeDataString(offerFolderItemId)}/children?$select=id,name";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to list offer folder contents. StatusCode: {(int)response.StatusCode}. Body: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<GraphCollectionResponse<GraphNamedItemResponse>>(
            stream, JsonOptions, cancellationToken);

        var workbook = result?.Value?.FirstOrDefault(item =>
            string.Equals(item.Name, _workspaceSettings.EconomicsWorkbookFileName, StringComparison.OrdinalIgnoreCase));

        return workbook?.Id;
    }

    private async Task<string> EnsureFolderPathAsync(
        string accessToken,
        string driveId,
        string folderPath,
        CancellationToken cancellationToken)
    {
        var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;
        var currentId = string.Empty;

        foreach (var segment in segments)
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

            var encodedPath = string.Join("/",
                currentPath.Split('/').Select(Uri.EscapeDataString));

            // Check if folder exists at this path
            var getUrl =
                $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(driveId)}/root:/{encodedPath}:";

            using var getRequest = new HttpRequestMessage(HttpMethod.Get, getUrl);
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var getResponse = await _httpClient.SendAsync(getRequest, cancellationToken);

            if (getResponse.IsSuccessStatusCode)
            {
                await using var getStream = await getResponse.Content.ReadAsStreamAsync(cancellationToken);
                var existing = await JsonSerializer.DeserializeAsync<GraphIdResponse>(
                    getStream, JsonOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(existing?.Id))
                {
                    currentId = existing.Id;
                    continue;
                }
            }

            // Folder does not exist — create it under the current parent
            var createUrl = string.IsNullOrEmpty(currentId)
                ? $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(driveId)}/root/children"
                : $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(driveId)}/items/{Uri.EscapeDataString(currentId)}/children";

            var createPayload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["name"] = segment,
                ["folder"] = new { },
                ["@microsoft.graph.conflictBehavior"] = "fail"
            });

            using var createReq = new HttpRequestMessage(HttpMethod.Post, createUrl);
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            createReq.Content = new StringContent(createPayload, Encoding.UTF8, "application/json");

            using var createResp = await _httpClient.SendAsync(createReq, cancellationToken);

            if (createResp.StatusCode == HttpStatusCode.Conflict)
            {
                // Created concurrently — fetch existing ID
                using var retryReq = new HttpRequestMessage(HttpMethod.Get, getUrl);
                retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var retryResp = await _httpClient.SendAsync(retryReq, cancellationToken);
                await using var retryStream = await retryResp.Content.ReadAsStreamAsync(cancellationToken);
                var retryItem = await JsonSerializer.DeserializeAsync<GraphIdResponse>(
                    retryStream, JsonOptions, cancellationToken);
                currentId = retryItem?.Id
                    ?? throw new InvalidOperationException($"Could not resolve ID for conflicting folder '{segment}'.");
                continue;
            }

            if (!createResp.IsSuccessStatusCode)
            {
                var body = await createResp.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Failed to create folder '{segment}'. StatusCode: {(int)createResp.StatusCode}. Body: {body}");
            }

            await using var createStream = await createResp.Content.ReadAsStreamAsync(cancellationToken);
            var created = await JsonSerializer.DeserializeAsync<GraphIdResponse>(
                createStream, JsonOptions, cancellationToken);
            currentId = created?.Id
                ?? throw new InvalidOperationException($"Empty ID after creating folder '{segment}'.");
        }

        return string.IsNullOrWhiteSpace(currentId)
            ? throw new InvalidOperationException($"Failed to ensure folder path '{folderPath}'.")
            : currentId;
    }

    private async Task<string?> CreateProjectMailFolderAsync(
        string accessToken,
        string mailboxEmail,
        int mainProjectNumber,
        string projectName,
        CancellationToken cancellationToken)
    {
        var folderName = SanitizeFolderName($"{mainProjectNumber} - {projectName}");
        var url = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(mailboxEmail)}/mailFolders";

        var json = JsonSerializer.Serialize(new
        {
            displayName = folderName,
            isHidden = false
        });

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(
                $"A folder named '{folderName}' already exists in '{mailboxEmail}'.");

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create Outlook folder '{folderName}' in '{mailboxEmail}'. StatusCode: {(int)response.StatusCode}. Body: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var folder = await JsonSerializer.DeserializeAsync<GraphIdResponse>(stream, JsonOptions, cancellationToken);
        return folder?.Id;
    }

    private async Task<string?> LookupUserObjectIdAsync(
        string accessToken,
        string identifier,
        CancellationToken cancellationToken)
    {
        var url = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(identifier)}?$select=id";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var user = await JsonSerializer.DeserializeAsync<GraphIdResponse>(stream, JsonOptions, cancellationToken);
        return string.IsNullOrWhiteSpace(user?.Id) ? null : user.Id;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var app = ConfidentialClientApplicationBuilder
            .Create(_graphSettings.ClientId)
            .WithClientSecret(_graphSettings.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_graphSettings.TenantId}")
            .Build();

        var authResult = await app
            .AcquireTokenForClient(["https://graph.microsoft.com/.default"])
            .ExecuteAsync(cancellationToken);

        return authResult.AccessToken;
    }

    private void ValidateGraphSettings()
    {
        if (string.IsNullOrWhiteSpace(_graphSettings.TenantId))
            throw new InvalidOperationException("MicrosoftGraph:TenantId is not configured.");

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientId))
            throw new InvalidOperationException("MicrosoftGraph:ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientSecret))
            throw new InvalidOperationException("MicrosoftGraph:ClientSecret is not configured.");
    }

    private static string SanitizeFolderName(string value)
    {
        var cleaned = value.Trim();
        foreach (var c in "\"*:<>?/\\|")
            cleaned = cleaned.Replace(c, '-');
        return string.IsNullOrWhiteSpace(cleaned) ? "Unknown" : cleaned;
    }

    private sealed class GraphIdResponse
    {
        public string? Id { get; init; }
    }

    private sealed class GraphSiteResponse
    {
        public string? Id { get; init; }
        public string? WebUrl { get; init; }
    }

    private sealed class GraphNamedItemResponse
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
    }

    private sealed class GraphCollectionResponse<T>
    {
        public List<T>? Value { get; init; }
    }
}
