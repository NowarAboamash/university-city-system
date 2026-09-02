using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
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
    public DbSet<HousingSettings> HousingSettings { get; set; } = null!;

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
            entity.HasData(GetSeedBuildings());
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
            entity.HasData(GetSeedRooms());
        });

        // Configure HousingRequest entity
        modelBuilder.Entity<HousingRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StudentId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DetailedAddress).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PreviousRoomNumber).HasMaxLength(50);
            entity.Property(e => e.SpecialNotes).HasMaxLength(1000);
            entity.Property(e => e.FeeAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountPaid).HasColumnType("decimal(18,2)");
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
            entity.HasMany(e => e.Allocations)
                .WithOne(a => a.HousingGroup)
                .HasForeignKey(a => a.HousingGroupId)
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
            // Only PENDING invitations must be unique per (group, student) — a student who
            // was rejected (or cancelled) must be able to request to join again later, which
            // means a new row for the same pair, not an update of the old one (keeps history).
            entity.HasIndex(e => new { e.HousingGroupId, e.InvitedStudentId })
                .IsUnique()
                .HasFilter("[Status] = 0");
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
            // A group may own many Allocation rows over time (vacated history), but only one
            // may be active at once — filtered so the constraint ignores vacated rows.
            entity.HasIndex(e => e.HousingGroupId)
                .IsUnique()
                .HasFilter("[HousingGroupId] IS NOT NULL AND [VacatedAt] IS NULL");
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

        // Configure HousingSettings entity — a single fixed row (Id = 1) holding the
        // admin-editable payment deadline / reminder / fee configuration.
        modelBuilder.Entity<HousingSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HousingFeeAmount).HasColumnType("decimal(18,2)");
            entity.HasData(new HousingSettings
            {
                Id = 1,
                PaymentDeadlineDays = 15,
                ReminderDaysBefore = 3,
                HousingFeeAmount = 0m,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });
    }

    // Simulation of Aleppo University's university city: 20 buildings (1-10 female, 11-20 male),
    // each with 6 floors of 44 rooms apiece, numbered {floor}{01-44} (e.g. 101..144, 201..244, ...).
    private const int SeedBuildingCount = 20;
    private const int SeedFloorsPerBuilding = 6;
    private const int SeedRoomsPerFloor = 44;
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Building[] GetSeedBuildings()
    {
        var buildings = new Building[SeedBuildingCount];

        for (var buildingId = 1; buildingId <= SeedBuildingCount; buildingId++)
        {
            buildings[buildingId - 1] = new Building
            {
                Id = buildingId,
                Name = buildingId.ToString(),
                Gender = buildingId <= 10 ? Gender.Female : Gender.Male,
                Status = BuildingStatus.Active,
                FloorsCount = SeedFloorsPerBuilding,
                StandardRoomCapacity = 4,
                CreatedAt = SeedTimestamp
            };
        }

        return buildings;
    }

    private static Room[] GetSeedRooms()
    {
        var rooms = new Room[SeedBuildingCount * SeedFloorsPerBuilding * SeedRoomsPerFloor];
        var roomId = 1;

        for (var buildingId = 1; buildingId <= SeedBuildingCount; buildingId++)
        {
            for (var floor = 1; floor <= SeedFloorsPerBuilding; floor++)
            {
                for (var roomIndex = 1; roomIndex <= SeedRoomsPerFloor; roomIndex++)
                {
                    rooms[roomId - 1] = new Room
                    {
                        Id = roomId,
                        BuildingId = buildingId,
                        RoomNumber = $"{floor}{roomIndex:D2}",
                        Floor = floor,
                        CurrentOccupancy = 0,
                        Status = RoomStatus.Available,
                        CreatedAt = SeedTimestamp
                    };
                    roomId++;
                }
            }
        }

        return rooms;
    }
}
