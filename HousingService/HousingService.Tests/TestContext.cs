using HousingService.Data;
using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.Interfaces;
using HousingService.Services;
using HousingService.Tests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace HousingService.Tests;

/// <summary>
/// Wires up the real repositories and services against a fresh, isolated EF Core InMemory
/// database (one per instance) — only the external-service boundaries (notifications, image
/// upload, AuthService user lookup) are faked. This exercises the actual query/Include logic
/// instead of mocking repositories, while staying fast and independent of the real hosted DB,
/// a real JWT, or a running AuthService/NotificationService/Cloudinary.
/// </summary>
public sealed class TestContext : IDisposable
{
    public HousingDbContext Db { get; }
    public FixedTimeProvider Clock { get; }
    public FakeNotificationPublisher Notifications { get; } = new();
    public FakeImageUploader ImageUploader { get; } = new();
    public FakeWalletClient WalletClient { get; } = new();

    public IBuildingRepository BuildingRepository { get; }
    public IRoomRepository RoomRepository { get; }
    public IHousingCycleRepository CycleRepository { get; }
    public IGovernorateRepository GovernorateRepository { get; }
    public IHousingRequestRepository RequestRepository { get; }
    public IHousingGroupRepository GroupRepository { get; }
    public IGroupInvitationRepository InvitationRepository { get; }
    public IAllocationRepository AllocationRepository { get; }
    public IHousingSettingsRepository SettingsRepository { get; }

    public IHousingGroupService GroupService { get; }
    public IAllocationService AllocationService { get; }
    public IHousingRequestService RequestService { get; }
    public IRoomService RoomService { get; }
    public IBuildingService BuildingService { get; }
    public IBuildingEvacuationService EvacuationService { get; }
    public IHousingSettingsService SettingsService { get; }
    public IPaymentReminderService PaymentReminderService { get; }

    public TestContext(DateTimeOffset? now = null)
    {
        var options = new DbContextOptionsBuilder<HousingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new HousingDbContext(options);
        Db.Database.EnsureCreated();

        Clock = new FixedTimeProvider(now ?? new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        BuildingRepository = new BuildingRepository(Db);
        RoomRepository = new RoomRepository(Db);
        CycleRepository = new HousingCycleRepository(Db);
        GovernorateRepository = new GovernorateRepository(Db);
        RequestRepository = new HousingRequestRepository(Db);
        GroupRepository = new HousingGroupRepository(Db);
        InvitationRepository = new GroupInvitationRepository(Db);
        AllocationRepository = new AllocationRepository(Db);
        SettingsRepository = new HousingSettingsRepository(Db);

        var userLookup = new FakeUserLookupService();

        GroupService = new HousingGroupService(
            GroupRepository, InvitationRepository, RequestRepository, CycleRepository,
            AllocationRepository, RoomRepository, userLookup, Notifications, Clock);

        AllocationService = new AllocationService(
            AllocationRepository, RoomRepository, RequestRepository, GroupRepository,
            GroupService, CycleRepository, Notifications, Clock);

        RequestService = new HousingRequestService(
            RequestRepository, GovernorateRepository, CycleRepository, BuildingRepository,
            AllocationRepository, InvitationRepository, ImageUploader, Notifications,
            GroupService, AllocationService, SettingsRepository, WalletClient, Clock);

        RoomService = new RoomService(RoomRepository, BuildingRepository, Clock);
        BuildingService = new BuildingService(BuildingRepository, AllocationRepository, Clock);
        EvacuationService = new BuildingEvacuationService(BuildingRepository, AllocationRepository, RoomRepository, Notifications, Clock);
        SettingsService = new HousingSettingsService(SettingsRepository, Clock);
        PaymentReminderService = new PaymentReminderService(RequestRepository, SettingsRepository, Notifications, Clock);
    }

    // --- Seed helpers -----------------------------------------------------
    // Use high ids (>= 1000) to stay clear of HousingDbContext's own HasData seed
    // (20 buildings / 5280 rooms with ids 1..~5280) that always applies regardless of provider.

    public Building AddBuilding(int id, Gender gender, int capacity = 4, BuildingStatus status = BuildingStatus.Active)
    {
        var building = new Building
        {
            Id = id,
            Name = $"TestBuilding{id}",
            Gender = gender,
            Status = status,
            StandardRoomCapacity = capacity,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Buildings.Add(building);
        Db.SaveChanges();
        return building;
    }

    // Offset well clear of the 1..5280 range HousingDbContext's own HasData seed always
    // occupies (20 buildings * 6 floors * 44 rooms) — that seed applies regardless of provider.
    private const int RoomIdOffset = 100_000;

    public Room AddRoom(int id, int buildingId, string roomNumber = "101", RoomStatus status = RoomStatus.Available)
    {
        var room = new Room
        {
            Id = id + RoomIdOffset,
            BuildingId = buildingId,
            RoomNumber = roomNumber,
            Floor = 1,
            Status = status,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Rooms.Add(room);
        Db.SaveChanges();
        return room;
    }

    public HousingCycle AddOpenCycle(int id)
    {
        var cycle = new HousingCycle
        {
            Id = id,
            Name = $"Cycle{id}",
            Status = HousingCycleStatus.Open,
            OpenedAt = Clock.GetUtcNow().UtcDateTime,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.HousingCycles.Add(cycle);
        Db.SaveChanges();
        return cycle;
    }

    public Governorate AddGovernorate(int id)
    {
        var gov = new Governorate { Id = id, Name = $"Gov{id}", CreatedAt = Clock.GetUtcNow().UtcDateTime };
        Db.Governorates.Add(gov);
        Db.SaveChanges();
        return gov;
    }

    /// <summary>Creates a HousingRequest, optionally already Accepted, optionally already in a group.</summary>
    public HousingRequest AddRequest(int id, string studentId, int cycleId, int governorateId, Gender gender,
        int? housingGroupId = null, AdmissionDecisionStatus? decisionStatus = null,
        DateTime? paymentDueDate = null, bool isPaid = false, bool reminderSent = false)
    {
        var request = new HousingRequest
        {
            Id = id,
            StudentId = studentId,
            Gender = gender,
            GovernorateId = governorateId,
            AcademicLevel = AcademicLevel.First,
            HousingCycleId = cycleId,
            DetailedAddress = "Test address",
            HousingGroupId = housingGroupId,
            Status = HousingRequestStatus.Locked,
            PaymentDueDate = paymentDueDate,
            IsPaid = isPaid,
            PaidAt = isPaid ? Clock.GetUtcNow().UtcDateTime : null,
            ReminderSent = reminderSent,
            SubmittedAt = Clock.GetUtcNow().UtcDateTime,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.HousingRequests.Add(request);
        Db.SaveChanges();

        if (decisionStatus is not null)
        {
            Db.AdmissionDecisions.Add(new AdmissionDecision
            {
                HousingRequestId = id,
                Status = decisionStatus.Value,
                DecisionDate = Clock.GetUtcNow().UtcDateTime,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            });
            Db.SaveChanges();
        }

        return request;
    }

    public HousingGroup AddGroup(int id, string leaderId, int cycleId, HousingGroupStatus status = HousingGroupStatus.Open)
    {
        var group = new HousingGroup
        {
            Id = id,
            LeaderId = leaderId,
            Code = $"GRP-TEST-{id}",
            HousingCycleId = cycleId,
            Status = status,
            MaxMembers = 4,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.HousingGroups.Add(group);
        Db.SaveChanges();
        return group;
    }

    public GroupInvitation AddPendingInvitation(int id, int groupId, string invitedStudentId)
    {
        var invitation = new GroupInvitation
        {
            Id = id,
            HousingGroupId = groupId,
            InvitedStudentId = invitedStudentId,
            InvitedByStudentId = invitedStudentId,
            Status = InvitationStatus.Pending,
            SentAt = Clock.GetUtcNow().UtcDateTime,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.GroupInvitations.Add(invitation);
        Db.SaveChanges();
        return invitation;
    }

    public Allocation AddAllocation(int id, int roomId, int? housingRequestId = null, int? housingGroupId = null, DateTime? vacatedAt = null)
    {
        var allocation = new Allocation
        {
            Id = id,
            RoomId = roomId,
            HousingRequestId = housingRequestId,
            HousingGroupId = housingGroupId,
            AllocatedAt = Clock.GetUtcNow().UtcDateTime,
            VacatedAt = vacatedAt,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        };
        Db.Allocations.Add(allocation);
        Db.SaveChanges();

        // Keep the room's Status consistent with the allocation being seeded, same as the
        // real AllocationService.CreateAsync would after allocating.
        if (vacatedAt is null)
        {
            var room = Db.Rooms.First(r => r.Id == roomId);
            var seats = housingRequestId is not null ? 1 : Db.HousingRequests.Count(r => r.HousingGroupId == housingGroupId);
            var building = Db.Buildings.First(b => b.Id == room.BuildingId);
            room.Status = seats >= building.StandardRoomCapacity ? RoomStatus.Full : RoomStatus.Occupied;
            Db.SaveChanges();
        }

        return allocation;
    }

    public void Dispose() => Db.Dispose();
}
