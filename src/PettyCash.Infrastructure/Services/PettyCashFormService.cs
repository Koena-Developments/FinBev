using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Application.Options;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class PettyCashFormService(
    PettyCashDbContext db,
    IApprovalRoutingService routingService,
    IOptions<PettyCashFormOptions> formOptions) : IPettyCashFormService
{
    private static readonly string TemplateFileName = "PettyCashForm-2026.docx";

    public Task<bool> RequestExistsAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        db.CashRequests.AsNoTracking().AnyAsync(r => r.Id == requestId, cancellationToken);

    public async Task<PettyCashFormDto?> GetFormAsync(
        Guid requestId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (!await CanAccessFormAsync(request, userId, isAdmin, cancellationToken))
        {
            return null;
        }

        return MapForm(request);
    }

    public async Task<IReadOnlyList<PettyCashFormCopyItemDto>> ListFinanceCopiesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var canView = await IsFinanceCopyRecipientAsync(userId, cancellationToken)
            || await IsFinanceSeniorManagerAsync(userId, cancellationToken);
        if (!canView)
        {
            return [];
        }

        var items = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.Claimant)
            .Where(r => r.FinanceFormReleasedAtUtc != null
                && r.Status != RequestStatus.Draft
                && r.Status != RequestStatus.Cancelled)
            .OrderByDescending(r => r.FinanceFormReleasedAtUtc)
            .ToListAsync(cancellationToken);

        return items.Select(r => new PettyCashFormCopyItemDto(
            r.Id,
            ToDisplayId(r.Id),
            r.Reason,
            r.Amount,
            r.Department.Name,
            r.Claimant.DisplayName,
            r.FinanceFormReleasedAtUtc!.Value)).ToList();
    }

    public async Task<byte[]> GenerateDocxAsync(
        Guid requestId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var form = await GetFormAsync(requestId, userId, isAdmin, cancellationToken)
            ?? throw new InvalidOperationException("The petty cash form for this request is not available.");

        return await BuildDocxBytesAsync(form, cancellationToken);
    }

    public async Task<byte[]> GenerateDocxForNotificationAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, cancellationToken)
            ?? throw new InvalidOperationException("This petty cash request could not be found.");

        return await BuildDocxBytesAsync(MapForm(request), cancellationToken);
    }

    private static async Task<byte[]> BuildDocxBytesAsync(PettyCashFormDto form, CancellationToken cancellationToken)
    {
        var templatePath = ResolveTemplatePath();
        await using var templateStream = File.OpenRead(templatePath);
        using var output = new MemoryStream();
        await templateStream.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("word/document.xml")
                ?? throw new InvalidOperationException("The petty cash form could not be prepared. Contact Finance.");
            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var xml = await reader.ReadToEndAsync(cancellationToken);

            xml = ApplyReplacements(xml, form);

            entry.Delete();
            var newEntry = archive.CreateEntry("word/document.xml");
            await using var writer = new StreamWriter(newEntry.Open(), new UTF8Encoding(false));
            await writer.WriteAsync(xml);
        }

        return output.ToArray();
    }

    public async Task ReleaseToFinanceCopiesAsync(Guid cashRequestId, CancellationToken cancellationToken = default)
    {
        var request = await db.CashRequests.FirstOrDefaultAsync(r => r.Id == cashRequestId, cancellationToken)
            ?? throw new InvalidOperationException("This petty cash request could not be found.");

        if (request.FinanceFormReleasedAtUtc is not null)
        {
            return;
        }

        var deptTask = await db.ApprovalTasks.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.CashRequestId == cashRequestId
                    && t.Stage == ApprovalStage.Department
                    && t.Decision == ApprovalDecision.Approved,
                cancellationToken);

        if (deptTask is null)
        {
            throw new InvalidOperationException("Department senior manager approval is required before releasing the form.");
        }

        if (request.ClaimantSignaturePng is null || request.ClaimantSignaturePng.Length == 0)
        {
            throw new InvalidOperationException("Claimant signature is missing.");
        }

        if (deptTask.SignatureImagePng is null || deptTask.SignatureImagePng.Length == 0)
        {
            throw new InvalidOperationException("Department senior manager signature is missing.");
        }

        request.FinanceFormReleasedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Domain.Entities.CashRequest?> LoadRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return await db.CashRequests.AsNoTracking()
            .Include(r => r.Claimant)
            .Include(r => r.Department)
            .Include(r => r.BusinessUnit)
            .Include(r => r.BudgetCode)
            .Include(r => r.ApprovalTasks)
            .ThenInclude(t => t.AssignedApprover)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
    }

    private Task<bool> CanAccessFormAsync(
        Domain.Entities.CashRequest request,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken) =>
        FormAccessHelper.CanViewAsync(
            db,
            request,
            userId,
            isAdmin,
            ct => IsFinanceSeniorManagerAsync(userId, ct),
            IsFinanceCopyRecipientAsync,
            cancellationToken);

    private async Task<bool> IsFinanceSeniorManagerAsync(string userId, CancellationToken cancellationToken)
    {
        var route = await routingService.ResolveFinanceAsync(cancellationToken);
        return route.UserId == userId
            || (!string.IsNullOrEmpty(route.OnBehalfOfUserId) && route.OnBehalfOfUserId == userId);
    }

    private async Task<bool> IsFinanceCopyRecipientAsync(string userId, CancellationToken cancellationToken)
    {
        var username = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync(cancellationToken);

        if (username is null)
        {
            return false;
        }

        return formOptions.Value.FinanceDocumentCopyUsernames
            .Any(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase));
    }

    private static PettyCashFormDto MapForm(Domain.Entities.CashRequest request)
    {
        var deptTask = request.ApprovalTasks
            .Where(t => t.Stage == ApprovalStage.Department && t.Decision == ApprovalDecision.Approved)
            .OrderByDescending(t => t.DecidedAtUtc)
            .FirstOrDefault();

        var pendingDeptTask = request.ApprovalTasks
            .FirstOrDefault(t => t.Stage == ApprovalStage.Department && t.Decision == ApprovalDecision.Pending);

        var financeTask = request.ApprovalTasks
            .Where(t => t.Stage == ApprovalStage.Finance && t.Decision == ApprovalDecision.Approved)
            .OrderByDescending(t => t.DecidedAtUtc)
            .FirstOrDefault();

        return new PettyCashFormDto(
            request.Id,
            ToDisplayId(request.Id),
            request.Claimant.DisplayName,
            request.Department.Name,
            request.BusinessUnit.Name,
            request.BudgetCode.Code,
            request.Amount,
            request.Reason,
            request.ExpenseDate,
            request.Status.ToString(),
            request.ClaimantSignedAtUtc,
            ToBase64(request.ClaimantSignaturePng),
            deptTask?.DecidedAtUtc,
            ToBase64(deptTask?.SignatureImagePng),
            deptTask?.AssignedApprover?.DisplayName ?? pendingDeptTask?.AssignedApprover?.DisplayName,
            financeTask?.DecidedAtUtc,
            ToBase64(financeTask?.SignatureImagePng),
            financeTask?.AssignedApprover?.DisplayName,
            request.FinanceFormReleasedAtUtc is not null);
    }

    private static string ApplyReplacements(string xml, PettyCashFormDto form)
    {
        var amount = form.Amount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("en-ZA"));
        var claimantDate = FormatDate(form.ClaimantSignedAtUtc);
        var deptDate = FormatDate(form.DepartmentManagerSignedAtUtc);
        var financeDate = FormatDate(form.FinanceManagerSignedAtUtc);

        xml = Regex.Replace(
            xml,
            @"xPetty cash voucher No:\s*[\.…]+",
            $"xPetty cash voucher No: {EscapeXml(form.DisplayId)}");

        xml = Regex.Replace(
            xml,
            @"Department[\.…\s]+Cost Centre[\.…\s]+",
            $"Department {EscapeXml(form.DepartmentName)}    Cost Centre {EscapeXml(form.BusinessUnitName)} ({EscapeXml(form.BudgetCode)}) ",
            RegexOptions.Singleline);

        xml = Regex.Replace(
            xml,
            @"Claimed By[\.…\s]+",
            $"Claimed By {EscapeXml(form.ClaimantName)} ",
            RegexOptions.Singleline);

        xml = Regex.Replace(
            xml,
            @"Amount[\.…]+",
            $"Amount R {amount} ",
            RegexOptions.Singleline);

        xml = Regex.Replace(
            xml,
            @"Reason for Claim[\.…]+",
            $"Reason for Claim {EscapeXml(form.Reason)} ",
            RegexOptions.Singleline);

        if (claimantDate != "—")
        {
            xml = xml.Replace(
                "Claimant Signature                   ",
                $"Claimant Signature (signed {claimantDate}) ");
        }

        if (deptDate != "—" && form.DepartmentManagerName is not null)
        {
            xml = xml.Replace(
                "Senior Manager’s Signature                  ",
                $"Senior Manager’s Signature ({EscapeXml(form.DepartmentManagerName)}, {deptDate}) ");
        }

        if (financeDate != "—" && form.FinanceManagerName is not null)
        {
            xml = xml.Replace(
                "Senior Manager:Finance .............…………….…………………...Date: ____/___/",
                $"Senior Manager: Finance {EscapeXml(form.FinanceManagerName)}    Date: {financeDate}");
        }

        return xml;
    }

    private static string FormatDate(DateTime? utc)
    {
        if (utc is null)
        {
            return "—";
        }

        return utc.Value.ToLocalTime().ToString("dd/MM/yyyy");
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string? ToBase64(byte[]? png) =>
        png is { Length: > 0 } ? Convert.ToBase64String(png) : null;

    private static string ToDisplayId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private static string ResolveTemplatePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Templates", TemplateFileName),
            Path.Combine(baseDir, TemplateFileName)
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("The petty cash form could not be prepared. Contact Finance.");
    }
}
