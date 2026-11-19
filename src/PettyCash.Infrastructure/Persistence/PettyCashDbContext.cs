using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PettyCash.Domain.Entities;

namespace PettyCash.Infrastructure.Persistence;

public class PettyCashDbContext(DbContextOptions<PettyCashDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<BudgetCode> BudgetCodes => Set<BudgetCode>();
    public DbSet<ApprovalMatrix> ApprovalMatrices => Set<ApprovalMatrix>();
    public DbSet<CashRequest> CashRequests => Set<CashRequest>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();
    public DbSet<FloatAccount> FloatAccounts => Set<FloatAccount>();
    public DbSet<FloatLedgerEntry> FloatLedgerEntries => Set<FloatLedgerEntry>();
    public DbSet<ApproverAvailability> ApproverAvailabilities => Set<ApproverAvailability>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReceiptReminder> ReceiptReminders => Set<ReceiptReminder>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Emoji).HasMaxLength(32);
            e.Property(x => x.PasswordResetCodeHash).HasMaxLength(64);
            e.HasOne(x => x.Department).WithMany(d => d.Users).HasForeignKey(x => x.DepartmentId);
        });

        builder.Entity<Department>(e => e.Property(x => x.Name).HasMaxLength(200));
        builder.Entity<BusinessUnit>(e => e.Property(x => x.Name).HasMaxLength(200));
        builder.Entity<BudgetCode>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(200);
        });

        builder.Entity<ApprovalMatrix>(e =>
        {
            e.HasOne(x => x.Department).WithOne(d => d.ApprovalMatrix).HasForeignKey<ApprovalMatrix>(x => x.DepartmentId);
            e.HasOne(x => x.PrimaryApprover).WithMany().HasForeignKey(x => x.PrimaryApproverUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ActingApprover).WithMany().HasForeignKey(x => x.ActingApproverUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CashRequest>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Amount).HasColumnType("numeric(12,2)");
            e.HasOne(x => x.Claimant).WithMany().HasForeignKey(x => x.ClaimantUserId);
        });

        builder.Entity<ApprovalTask>(e =>
        {
            e.HasOne(x => x.CashRequest).WithMany(r => r.ApprovalTasks).HasForeignKey(x => x.CashRequestId);
            e.HasOne(x => x.AssignedApprover).WithMany().HasForeignKey(x => x.AssignedApproverUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OnBehalfOf).WithMany().HasForeignKey(x => x.OnBehalfOfUserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SignatureHash).HasMaxLength(128);
            e.HasIndex(x => x.CashRequestId);
        });

        builder.Entity<FloatAccount>(e =>
        {
            e.Property(x => x.Capacity).HasColumnType("numeric(12,2)");
            e.Property(x => x.Balance).HasColumnType("numeric(12,2)");
        });

        builder.Entity<FloatLedgerEntry>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(12,2)");
        });

        builder.Entity<ApproverAvailability>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User).WithOne().HasForeignKey<ApproverAvailability>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Source).HasMaxLength(100);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(50);
        });

        builder.Entity<ReceiptReminder>(e =>
        {
            e.HasKey(x => x.CashRequestId);
            e.HasOne(x => x.CashRequest)
                .WithOne()
                .HasForeignKey<ReceiptReminder>(x => x.CashRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
