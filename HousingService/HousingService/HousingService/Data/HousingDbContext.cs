using HousingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HousingService.Data;

public class HousingDbContext : DbContext
{
    public HousingDbContext(DbContextOptions<HousingDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Building> Buildings { get; set; } = null!;
    public DbSet<Room> Rooms { get; set; } = null!;
    public DbSet<HousingRequest> HousingRequests { get; set; } = null!;
    public DbSet<HousingRequestDocument> HousingRequestDocuments { get; set; } = null!;
    public DbSet<HousingGroup> HousingGroups { get; set; } = null!;
    public DbSet<GroupInvitation> GroupInvitations { get; set; } = null!;
    public DbSet<AdmissionDecision> AdmissionDecisions { get; set; } = null!;
    public DbSet<Allocation> Allocations { get; set; } = null!;
    public DbSet<HousingCycle> HousingCycles { get; set; } = null!;
    public DbSet<Governorate> Governorates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Building entity
        modelBuilder.Entity<Building>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasMany(e => e.Rooms)
                .WithOne(r => r.Building)
                .HasForeignKey(r => r.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure Room entity
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoomNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.BuildingId, e.RoomNumber }).IsUnique();
            entity.HasMany(e => e.Allocations)
                .WithOne(a => a.Room)
                .HasForeignKey(a => a.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure HousingRequest entity
        modelBuilder.Entity<HousingRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StudentId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DetailedAddress).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PreviousRoomNumber).HasMaxLength(50);
            entity.Property(e => e.SpecialNotes).HasMaxLength(1000);
            entity.HasMany(e => e.Documents)
                .WithOne(d => d.HousingRequest)
                .HasForeignKey(d => d.HousingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AdmissionDecision)
                .WithOne(a => a.HousingRequest)
                .HasForeignKey<AdmissionDecision>(a => a.HousingRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Allocations)
                .WithOne(a => a.HousingRequest)
                .HasForeignKey(a => a.HousingRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Governorate)
                .WithMany()
                .HasForeignKey(e => e.GovernorateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.HousingCycle)
                .WithMany()
                .HasForeignKey(e => e.HousingCycleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PreviousBuilding)
                .WithMany()
                .HasForeignKey(e => e.PreviousBuildingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SubmittedAt);
            entity.HasIndex(e => new { e.StudentId, e.HousingCycleId }).IsUnique();
        });

        // Configure HousingRequestDocument entity
        modelBuilder.Entity<HousingRequestDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);
            entity.HasIndex(e => e.HousingRequestId);
            entity.HasIndex(e => e.ReviewStatus);
        });

        // Configure HousingGroup entity
        modelBuilder.Entity<HousingGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LeaderId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasMany(e => e.Members)
                .WithOne(h => h.HousingGroup)
                .HasForeignKey(h => h.HousingGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Invitations)
                .WithOne(i => i.HousingGroup)
                .HasForeignKey(i => i.HousingGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Allocation)
                .WithOne(a => a.HousingGroup)
                .HasForeignKey<Allocation>(a => a.HousingGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.HousingCycle)
                .WithMany()
                .HasForeignKey(e => e.HousingCycleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.LeaderId);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure GroupInvitation entity
        modelBuilder.Entity<GroupInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvitedStudentId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.InvitedByStudentId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.InvitedStudentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.HousingGroupId, e.InvitedStudentId }).IsUnique();
        });

        // Configure AdmissionDecision entity
        modelBuilder.Entity<AdmissionDecision>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DecisionReason).HasMaxLength(500);
            entity.Property(e => e.ReviewedBy).HasMaxLength(100);
            entity.HasOne(e => e.HousingRequest)
                .WithOne(h => h.AdmissionDecision)
                .HasForeignKey<AdmissionDecision>(a => a.HousingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DecisionDate);
        });

        // Configure Allocation entity
        modelBuilder.Entity<Allocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.HousingRequestId);
            entity.HasIndex(e => e.HousingGroupId);
            entity.HasIndex(e => e.RoomId);
            entity.HasIndex(e => e.AllocatedAt);
            entity.HasIndex(e => e.VacatedAt);
        });

        // Configure HousingCycle entity
        modelBuilder.Entity<HousingCycle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        // Configure Governorate entity
        modelBuilder.Entity<Governorate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(new Governorate { Id = 1, Name = "حلب", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        });
    }
}
