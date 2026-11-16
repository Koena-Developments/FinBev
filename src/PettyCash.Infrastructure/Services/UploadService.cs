using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class UploadService(IHostEnvironment env, IConfiguration configuration, PettyCashDbContext db) : IUploadService
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task<IReadOnlyList<UploadedFileDto>> ListMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        return ListInFolderAsync(UserFolder(userId), cancellationToken);
    }

    public async Task<IReadOnlyList<UploadedFileDto>> ListForFinanceAsync(CancellationToken cancellationToken = default)
    {
        var root = UploadsRoot();
        if (!Directory.Exists(root))
        {
            return [];
        }

        var stored = new List<StoredFile>();
        var ownerByFile = new Dictionary<Guid, string>();
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            var ownerId = Path.GetFileName(folder);
            foreach (var meta in await ReadFolderAsync(folder, cancellationToken))
            {
                stored.Add(meta);
                ownerByFile[meta.Id] = ownerId;
            }
        }

        var enriched = await EnrichAsync(stored, cancellationToken);
        var ownerIds = ownerByFile.Values.Distinct().ToList();
        var people = ownerIds.Count == 0
            ? new Dictionary<string, AppUser>()
            : await db.Users.AsNoTracking()
                .Include(user => user.Department)
                .Where(user => ownerIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, cancellationToken);

        return enriched.Select(file =>
        {
            if (!string.IsNullOrEmpty(file.ClaimantName)
                || !ownerByFile.TryGetValue(file.Id, out var ownerId)
                || !people.TryGetValue(ownerId, out var person))
            {
                return file;
            }

            return file with
            {
                ClaimantName = person.DisplayName,
                DepartmentName = person.Department?.Name ?? file.DepartmentName
            };
        }).ToList();
    }

    public async Task<IReadOnlySet<Guid>> ListCashRequestIdsWithUploadsAsync(CancellationToken cancellationToken = default)
    {
        var root = UploadsRoot();
        var ids = new HashSet<Guid>();
        if (!Directory.Exists(root))
        {
            return ids;
        }

        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            foreach (var metaPath in Directory.EnumerateFiles(folder, "*.json"))
            {
                StoredFile? meta;
                try
                {
                    await using var stream = File.OpenRead(metaPath);
                    meta = await JsonSerializer.DeserializeAsync<StoredFile>(stream, JsonOptions, cancellationToken);
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    continue;
                }

                if (meta?.CashRequestId is Guid cashRequestId
                    && cashRequestId != Guid.Empty
                    && File.Exists(DataPath(folder, meta.Id)))
                {
                    ids.Add(cashRequestId);
                }
            }
        }

        return ids;
    }

    public async Task<UploadedFileDto> AssignClaimAsync(
        string userId,
        Guid fileId,
        Guid cashRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.Claimant)
            .FirstOrDefaultAsync(r => r.Id == cashRequestId, cancellationToken);
        if (request is null)
        {
            throw new InvalidOperationException("That claim could not be found.");
        }

        if (request.ClaimantUserId != userId)
        {
            throw new InvalidOperationException("You can only attach documents to your own claims.");
        }

        var folder = UserFolder(userId);
        var meta = ReadOne(folder, fileId);
        if (meta is null)
        {
            throw new InvalidOperationException("That file could not be found.");
        }

        var updated = meta with { CashRequestId = cashRequestId };
        await File.WriteAllTextAsync(MetaPath(folder, fileId), JsonSerializer.Serialize(updated, JsonOptions), cancellationToken);
        return ToDto(updated, request);
    }

    public async Task<UploadedFileDto> SaveAsync(
        string userId,
        Guid cashRequestId,
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        _ = contentType;
        var request = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.Claimant)
            .FirstOrDefaultAsync(r => r.Id == cashRequestId, cancellationToken);
        if (request is null)
        {
            throw new InvalidOperationException("That claim could not be found.");
        }

        if (request.ClaimantUserId != userId)
        {
            throw new InvalidOperationException("You can only attach documents to your own claims.");
        }

        var safeName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Contains('\0'))
        {
            throw new InvalidOperationException("Choose a file to upload.");
        }

        var extension = Path.GetExtension(safeName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Upload a PDF, Word document, or image (PNG, JPG, WEBP).");
        }

        var folder = UserFolder(userId);
        Directory.CreateDirectory(folder);

        var id = Guid.NewGuid();
        var dataPath = DataPath(folder, id);
        await using (var output = File.Create(dataPath))
        {
            await content.CopyToAsync(output, cancellationToken);
            if (output.Length == 0)
            {
                output.Close();
                File.Delete(dataPath);
                throw new InvalidOperationException("That file is empty.");
            }

            if (output.Length > MaxBytes)
            {
                output.Close();
                File.Delete(dataPath);
                throw new InvalidOperationException("Each file must be 10 MB or smaller.");
            }
        }

        if (!HasAllowedSignature(extension, dataPath))
        {
            File.Delete(dataPath);
            throw new InvalidOperationException("That file content does not match a PDF, Word document, or image.");
        }

        var stored = new StoredFile(
            id,
            safeName,
            DownloadContentType(safeName),
            new FileInfo(dataPath).Length,
            DateTime.UtcNow,
            cashRequestId);
        await File.WriteAllTextAsync(MetaPath(folder, id), JsonSerializer.Serialize(stored, JsonOptions), cancellationToken);
        return ToDto(stored, request);
    }

    public async Task<(UploadedFileDto Meta, Stream Content)?> OpenAsync(
        string userId,
        Guid id,
        bool asFinance,
        CancellationToken cancellationToken = default)
    {
        var located = Locate(userId, id, asFinance);
        if (located is null)
        {
            return null;
        }

        var enriched = await EnrichAsync([located.Value.Meta], cancellationToken);
        Stream stream = File.OpenRead(located.Value.DataPath);
        return (enriched[0], stream);
    }

    public Task<bool> DeleteAsync(string userId, Guid id, CancellationToken cancellationToken = default)
    {
        var folder = UserFolder(userId);
        var metaPath = MetaPath(folder, id);
        var dataPath = DataPath(folder, id);
        var existed = File.Exists(metaPath) || File.Exists(dataPath);
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        if (File.Exists(dataPath))
        {
            File.Delete(dataPath);
        }

        return Task.FromResult(existed);
    }

    private async Task<IReadOnlyList<UploadedFileDto>> ListInFolderAsync(string folder, CancellationToken cancellationToken)
    {
        var stored = await ReadFolderAsync(folder, cancellationToken);
        return await EnrichAsync(stored, cancellationToken);
    }

    private static async Task<List<StoredFile>> ReadFolderAsync(string folder, CancellationToken cancellationToken)
    {
        var stored = new List<StoredFile>();
        if (!Directory.Exists(folder))
        {
            return stored;
        }

        foreach (var metaPath in Directory.EnumerateFiles(folder, "*.json"))
        {
            await using var stream = File.OpenRead(metaPath);
            var meta = await JsonSerializer.DeserializeAsync<StoredFile>(stream, JsonOptions, cancellationToken);
            if (meta is not null && File.Exists(DataPath(folder, meta.Id)))
            {
                stored.Add(meta);
            }
        }

        return stored;
    }

    private async Task<IReadOnlyList<UploadedFileDto>> EnrichAsync(IReadOnlyList<StoredFile> files, CancellationToken cancellationToken)
    {
        var ids = files.Select(file => file.CashRequestId).Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var claims = ids.Count == 0
            ? new Dictionary<Guid, CashRequest>()
            : await db.CashRequests.AsNoTracking()
                .Include(r => r.Department)
                .Include(r => r.Claimant)
                .Where(r => ids.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

        return files
            .OrderByDescending(file => file.CreatedAtUtc)
            .Select(file => ToDto(file, file.CashRequestId is Guid id && claims.TryGetValue(id, out var claim) ? claim : null))
            .ToList();
    }

    private (StoredFile Meta, string DataPath)? Locate(string userId, Guid id, bool asFinance)
    {
        var ownFolder = UserFolder(userId);
        var own = ReadOne(ownFolder, id);
        if (own is not null)
        {
            return (own, DataPath(ownFolder, id));
        }

        if (!asFinance)
        {
            return null;
        }

        var root = UploadsRoot();
        if (!Directory.Exists(root))
        {
            return null;
        }

        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            var meta = ReadOne(folder, id);
            if (meta is not null)
            {
                return (meta, DataPath(folder, id));
            }
        }

        return null;
    }

    private static StoredFile? ReadOne(string folder, Guid id)
    {
        var metaPath = MetaPath(folder, id);
        if (!File.Exists(metaPath) || !File.Exists(DataPath(folder, id)))
        {
            return null;
        }

        return JsonSerializer.Deserialize<StoredFile>(File.ReadAllText(metaPath), JsonOptions);
    }

    private string UploadsRoot()
    {
        var secretsDir = configuration["Secrets:Directory"]
            ?? Path.Combine(env.ContentRootPath, "..", "..", "secrets");
        if (!Path.IsPathRooted(secretsDir))
        {
            secretsDir = Path.GetFullPath(secretsDir);
        }

        return Path.Combine(secretsDir, "uploads");
    }

    private string UserFolder(string userId)
    {
        var safeUser = string.Concat(userId.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        return Path.Combine(UploadsRoot(), safeUser);
    }

    private static string DataPath(string folder, Guid id) => Path.Combine(folder, id.ToString("N"));

    private static string MetaPath(string folder, Guid id) => Path.Combine(folder, id.ToString("N") + ".json");

    public static string DownloadContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }

    private static bool HasAllowedSignature(string extension, string path)
    {
        Span<byte> header = stackalloc byte[16];
        int read;
        using (var stream = File.OpenRead(path))
        {
            read = stream.Read(header);
        }

        header = header[..read];
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => header.StartsWith("%PDF-"u8),
            ".png" => header.StartsWith(PngSignature),
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".webp" => read >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
            ".docx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4B && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07) && (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08),
            ".doc" => read >= 8 && header.StartsWith(OleSignature),
            _ => false
        };
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static string ToDisplayId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private static UploadedFileDto ToDto(StoredFile file, CashRequest? request)
    {
        var claimId = request?.Id ?? file.CashRequestId ?? Guid.Empty;
        return new UploadedFileDto(
            file.Id,
            file.FileName,
            file.ContentType,
            file.SizeBytes,
            file.CreatedAtUtc,
            claimId,
            claimId == Guid.Empty ? "" : ToDisplayId(claimId),
            request?.Reason ?? "",
            request?.Claimant?.DisplayName ?? "",
            request?.Department?.Name ?? "",
            request?.Amount ?? 0,
            request?.Status.ToString() ?? "");
    }

    private sealed record StoredFile(
        Guid Id,
        string FileName,
        string ContentType,
        long SizeBytes,
        DateTime CreatedAtUtc,
        Guid? CashRequestId = null);
}
