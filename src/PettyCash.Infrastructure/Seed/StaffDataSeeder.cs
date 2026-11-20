using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PettyCash.Application.Common;
using PettyCash.Application.Options;
using PettyCash.Domain;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Email;
using PettyCash.Infrastructure.Security;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Seed;

public class StaffDataSeeder(
    PettyCashDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IHostEnvironment env,
    IConfiguration configuration,
    IOptions<EmailOptions> emailOptions,
    ILogger<StaffDataSeeder> logger)
{
    private sealed record StaffDirectoryRoot(
        List<string> Departments,
        List<StaffMember> Staff,
        Dictionary<string, MatrixEntry> ApprovalMatrix,
        string AdminUsername);

    private sealed record StaffMember(string Department, string FullName, string Title, string Username);
    private sealed record MatrixEntry(
        [property: JsonPropertyName("primary")] string? Primary,
        [property: JsonPropertyName("acting")] string? Acting);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync();
        var path = Path.Combine(env.ContentRootPath, "Seed", "staff-directory.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "Seed", "staff-directory.json");
        }

        if (!File.Exists(path))
        {
            logger.LogWarning("Staff directory seed file not found at {Path}", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var root = await JsonSerializer.DeserializeAsync<StaffDirectoryRoot>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
        if (root is null)
        {
            return;
        }

        var deptMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var deptName in root.Departments)
        {
            var dept = await db.Departments.FirstOrDefaultAsync(d => d.Name == deptName, cancellationToken);
            if (dept is null)
            {
                dept = new Department { Id = Guid.NewGuid(), Name = deptName };
                db.Departments.Add(dept);
            }

            deptMap[deptName] = dept.Id;

            var bu = await db.BusinessUnits.FirstOrDefaultAsync(b => b.DepartmentId == dept.Id && b.Name == deptName, cancellationToken);
            if (bu is null)
            {
                bu = new BusinessUnit { Id = Guid.NewGuid(), Name = deptName, DepartmentId = dept.Id };
                db.BusinessUnits.Add(bu);
            }

            var code = await db.BudgetCodes.FirstOrDefaultAsync(c => c.BusinessUnitId == bu.Id && c.Code == "PC-001", cancellationToken);
            if (code is null)
            {
                db.BudgetCodes.Add(new BudgetCode
                {
                    Id = Guid.NewGuid(),
                    Code = "PC-001",
                    Description = "Petty Cash",
                    BusinessUnitId = bu.Id
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var passwordRows = new List<(string Department, string FullName, string Title, string Username, string Password)>();
        var createdAccounts = 0;
        var usernameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in root.Staff)
        {
            if (!deptMap.TryGetValue(member.Department, out var departmentId))
            {
                continue;
            }

            var user = await userManager.FindByNameAsync(member.Username);
            if (user is null)
            {
                var tempPassword = PasswordForNewAccount();
                if (tempPassword is null)
                {
                    logger.LogWarning("Skipped new account {Username} because Seed:InitialPassword is not set.", member.Username);
                    continue;
                }

                user = new AppUser
                {
                    UserName = member.Username,
                    DisplayName = member.FullName,
                    Title = member.Title,
                    DepartmentId = departmentId,
                    MustChangePassword = true,
                    Email = WorkEmail(member.Username),
                    EmailConfirmed = true
                };

                var create = await userManager.CreateAsync(user, tempPassword);
                if (!create.Succeeded)
                {
                    logger.LogError("Failed to create user {Username}: {Errors}", member.Username, string.Join(", ", create.Errors.Select(e => e.Description)));
                    continue;
                }

                await userManager.AddToRoleAsync(user, AppRoles.Employee);
                createdAccounts++;
                if (env.IsDevelopment())
                {
                    passwordRows.Add((member.Department, member.FullName, member.Title, WorkEmail(member.Username), tempPassword));
                }
            }
            else
            {
                user.DisplayName = member.FullName;
                user.Title = member.Title;
                user.DepartmentId = departmentId;
                if (ShouldReplaceEmail(user.Email, member.Username))
                {
                    user.Email = WorkEmail(member.Username);
                }

                await userManager.UpdateAsync(user);
            }

            usernameToId[member.Username] = user.Id;
        }

        foreach (var shared in root.Staff
            .GroupBy(member => WorkEmail(member.Username), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            logger.LogWarning(
                "These staff share {Email} and will receive each other's petty cash mail until an AddressOverrides entry splits them: {Names}",
                shared.Key,
                string.Join(", ", shared.Select(member => member.FullName)));
        }

        foreach (var (deptName, entry) in root.ApprovalMatrix)
        {
            if (!deptMap.TryGetValue(deptName, out var departmentId))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Primary) || !usernameToId.TryGetValue(entry.Primary, out var primaryId))
            {
                continue;
            }

            string? actingId = null;
            if (!string.IsNullOrWhiteSpace(entry.Acting))
            {
                usernameToId.TryGetValue(entry.Acting, out actingId);
            }

            var matrix = await db.ApprovalMatrices.FirstOrDefaultAsync(m => m.DepartmentId == departmentId, cancellationToken);
            if (matrix is null)
            {
                db.ApprovalMatrices.Add(new ApprovalMatrix
                {
                    Id = Guid.NewGuid(),
                    DepartmentId = departmentId,
                    PrimaryApproverUserId = primaryId,
                    ActingApproverUserId = actingId
                });
            }
            else
            {
                matrix.PrimaryApproverUserId = primaryId;
                matrix.ActingApproverUserId = actingId;
            }

            var primaryUser = await userManager.FindByIdAsync(primaryId);
            if (primaryUser is not null)
            {
                await userManager.AddToRoleAsync(primaryUser, AppRoles.Approver);
            }

            if (actingId is not null)
            {
                var actingUser = await userManager.FindByIdAsync(actingId);
                if (actingUser is not null)
                {
                    await userManager.AddToRoleAsync(actingUser, AppRoles.Approver);
                }
            }
        }

        await SyncApproverMailboxEmailsAsync(root, usernameToId);

        await SyncFinanceAdminRoleAsync(root.AdminUsername, usernameToId);

        if (!await db.FloatAccounts.AnyAsync(cancellationToken))
        {
            var floatId = Guid.NewGuid();
            db.FloatAccounts.Add(new FloatAccount
            {
                Id = floatId,
                Capacity = PettyCashPolicy.FloatCapacity,
                Balance = PettyCashPolicy.FloatCapacity
            });
            db.FloatLedgerEntries.Add(new FloatLedgerEntry
            {
                Id = Guid.NewGuid(),
                FloatAccountId = floatId,
                EntryType = Domain.Enums.FloatLedgerEntryType.Opening,
                Amount = PettyCashPolicy.FloatCapacity,
                CreatedAtUtc = DateTime.UtcNow,
                Note = "Initial float"
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (passwordRows.Count > 0 && env.IsDevelopment())
        {
            WritePasswordWorkbook(passwordRows);
        }
        else if (createdAccounts > 0)
        {
            logger.LogInformation(
                "Created {Count} new accounts. They use the configured first password and must change it at sign-in. The password was not written to a file.",
                createdAccounts);
        }
    }

    public async Task ExportTemporaryPasswordsAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .Include(u => u.Department)
            .OrderBy(u => u.Department.Name)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        var rows = new List<(string Department, string FullName, string Title, string Username, string Password)>();

        foreach (var user in users)
        {
            var tempPassword = GenerateTemporaryPassword();
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, tempPassword);
            if (!result.Succeeded)
            {
                logger.LogError("Failed to reset password for {Username}: {Errors}", user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            user.MustChangePassword = true;
            await userManager.UpdateAsync(user);

            rows.Add((
                user.Department.Name,
                user.DisplayName,
                user.Title,
                user.Email ?? user.UserName ?? "",
                tempPassword));
        }

        WritePasswordWorkbook(rows);
    }

    public async Task<int> ResetAllPasswordsAsync(string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .Include(u => u.Department)
            .OrderBy(u => u.Department.Name)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        var rows = new List<(string Department, string FullName, string Title, string Username, string Password)>();
        var reset = 0;

        foreach (var user in users)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, temporaryPassword);
            if (!result.Succeeded)
            {
                logger.LogError(
                    "Failed to reset password for {Username}: {Errors}",
                    user.UserName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            user.MustChangePassword = true;
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                logger.LogError(
                    "Password was reset but the change-on-login flag failed for {Username}: {Errors}",
                    user.UserName,
                    string.Join(", ", update.Errors.Select(e => e.Description)));
                continue;
            }

            reset++;
            rows.Add((
                user.Department.Name,
                user.DisplayName,
                user.Title,
                user.Email ?? user.UserName ?? "",
                temporaryPassword));
        }

        if (rows.Count > 0)
        {
            WritePasswordWorkbook(rows);
        }

        logger.LogInformation("Reset {Count} of {Total} accounts to the shared temporary password. Each person must change it at sign-in.", reset, users.Count);
        return reset;
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in new[] { AppRoles.Employee, AppRoles.Approver, AppRoles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private async Task SyncApproverMailboxEmailsAsync(
        StaffDirectoryRoot root,
        Dictionary<string, string> usernameToId)
    {
        var usernames = root.ApprovalMatrix.Values
            .SelectMany(e => new[] { e.Primary, e.Acting })
            .Append(root.AdminUsername)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var username in usernames)
        {
            if (!usernameToId.TryGetValue(username!, out var userId))
            {
                continue;
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                continue;
            }

            var desired = WorkEmail(username!);
            if (string.IsNullOrWhiteSpace(desired)
                || string.Equals(user.Email, desired, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            user.Email = desired;
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task SyncFinanceAdminRoleAsync(
        string? adminUsername,
        Dictionary<string, string> usernameToId)
    {
        string? designatedAdminId = null;
        if (!string.IsNullOrWhiteSpace(adminUsername)
            && usernameToId.TryGetValue(adminUsername, out var adminId))
        {
            designatedAdminId = adminId;
            var admin = await userManager.FindByIdAsync(adminId);
            if (admin is not null)
            {
                await userManager.AddToRoleAsync(admin, AppRoles.Admin);
                await userManager.AddToRoleAsync(admin, AppRoles.Approver);
            }
        }

        var currentAdmins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        foreach (var user in currentAdmins)
        {
            if (designatedAdminId is null
                || !string.Equals(user.Id, designatedAdminId, StringComparison.Ordinal))
            {
                var removed = await userManager.RemoveFromRoleAsync(user, AppRoles.Admin);
                if (!removed.Succeeded)
                {
                    logger.LogWarning(
                        "Could not remove Admin role from {Username}: {Errors}",
                        user.UserName,
                        string.Join(", ", removed.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private void WritePasswordWorkbook(List<(string Department, string FullName, string Title, string Username, string Password)> rows)
    {
        if (!env.IsDevelopment())
        {
            logger.LogWarning("Refused to save temporary passwords to a file outside Development.");
            return;
        }

        var secretsDir = configuration["Secrets:Directory"]
            ?? Path.Combine(env.ContentRootPath, "..", "..", "secrets");
        if (!Path.IsPathRooted(secretsDir))
        {
            secretsDir = Path.GetFullPath(secretsDir);
        }

        Directory.CreateDirectory(secretsDir);
        var filePath = Path.Combine(secretsDir, "staff-temporary-passwords.xlsx");
        var csvPath = Path.Combine(secretsDir, "staff-temporary-passwords.csv");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("TemporaryPasswords");
        sheet.Cell(1, 1).Value = "Department";
        sheet.Cell(1, 2).Value = "Full Name";
        sheet.Cell(1, 3).Value = "Title";
        sheet.Cell(1, 4).Value = "Email";
        sheet.Cell(1, 5).Value = "Temporary Password";
        sheet.Cell(1, 6).Value = "Must Change On First Login";

        var rowIndex = 2;
        foreach (var row in rows.OrderBy(r => r.Department).ThenBy(r => r.FullName))
        {
            sheet.Cell(rowIndex, 1).Value = row.Department;
            sheet.Cell(rowIndex, 2).Value = row.FullName;
            sheet.Cell(rowIndex, 3).Value = row.Title;
            sheet.Cell(rowIndex, 4).Value = row.Username;
            sheet.Cell(rowIndex, 5).Value = row.Password;
            sheet.Cell(rowIndex, 6).Value = "Yes";
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);

        var ordered = rows.OrderBy(r => r.Department).ThenBy(r => r.FullName).ToList();
        using (var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine("Department,Full Name,Title,Email,Temporary Password,Must Change On First Login");
            foreach (var row in ordered)
            {
                static string Esc(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
                writer.WriteLine(string.Join(',', Esc(row.Department), Esc(row.FullName), Esc(row.Title), Esc(row.Username), Esc(row.Password), "\"Yes\""));
            }
        }

        SecretFiles.TryRestrict(filePath);
        SecretFiles.TryRestrict(csvPath);
        logger.LogInformation("Wrote temporary passwords for {Count} new accounts into the local secrets folder.", rows.Count);
    }

    private bool ShouldReplaceEmail(string? current, string username)
    {
        if (string.IsNullOrWhiteSpace(current)
            || current.EndsWith("@pettycash.local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return emailOptions.Value.AddressOverrides.ContainsKey(username);
    }

    private string WorkEmail(string username)
    {
        var address = FoodBevEmailAddressResolver.Resolve(
            new AppUser
            {
                UserName = username,
                Email = $"{username}@pettycash.local"
            },
            emailOptions.Value);

        return string.IsNullOrWhiteSpace(address) ? $"{username}@pettycash.local" : address;
    }

    private string? PasswordForNewAccount()
    {
        if (env.IsDevelopment())
        {
            return GenerateTemporaryPassword();
        }

        var configured = configuration["Seed:InitialPassword"];
        return DeploymentSettings.IsAcceptableInitialPassword(configured) ? configured : null;
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        Span<char> chars = stackalloc char[16];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        return new string(chars);
    }
}
