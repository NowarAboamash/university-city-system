using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class AllocationServiceTests
{
    [Fact]
    public async Task TransferAsync_MovesOccupant_FreesOldRoom_OccupiesNewRoom()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var oldRoom = ctx.AddRoom(1000, building.Id, "101");
        var newRoom = ctx.AddRoom(1001, building.Id, "102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        var allocation = ctx.AddAllocation(1000, oldRoom.Id, housingRequestId: request.Id);

        var result = await ctx.AllocationService.TransferAsync(allocation.Id, new TransferAllocationDto { NewRoomId = newRoom.Id });

        Assert.NotNull(result);
        Assert.Equal(newRoom.Id, result!.RoomId);

        var refreshedOldRoom = ctx.Db.Rooms.Single(r => r.Id == oldRoom.Id);
        var refreshedNewRoom = ctx.Db.Rooms.Single(r => r.Id == newRoom.Id);
        Assert.Equal(RoomStatus.Available, refreshedOldRoom.Status);
        Assert.Equal(RoomStatus.Occupied, refreshedNewRoom.Status);

        Assert.Contains(ctx.Notifications.Sent, n => n.StudentIds is not null && n.StudentIds.Contains("student-1") && n.Title.Contains("نقل"));
    }

    [Fact]
    public async Task TransferAsync_SameRoom_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);
        var allocation = ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.TransferAsync(allocation.Id, new TransferAllocationDto { NewRoomId = room.Id }));
    }

    [Fact]
    public async Task TransferAsync_AlreadyVacated_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female);
        var room1 = ctx.AddRoom(1000, building.Id, "101");
        var room2 = ctx.AddRoom(1001, building.Id, "102");
        var allocation = ctx.AddAllocation(1000, room1.Id, housingRequestId: 1, vacatedAt: ctx.Clock.GetUtcNow().UtcDateTime);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.TransferAsync(allocation.Id, new TransferAllocationDto { NewRoomId = room2.Id }));
    }

    [Fact]
    public async Task TransferAsync_TargetBuildingGenderMismatch_Throws()
    {
        using var ctx = new TestContext();
        var femaleBuilding = ctx.AddBuilding(1000, Gender.Female);
        var maleBuilding = ctx.AddBuilding(1001, Gender.Male);
        var oldRoom = ctx.AddRoom(1000, femaleBuilding.Id);
        var maleRoom = ctx.AddRoom(1001, maleBuilding.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);
        var allocation = ctx.AddAllocation(1000, oldRoom.Id, housingRequestId: request.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.TransferAsync(allocation.Id, new TransferAllocationDto { NewRoomId = maleRoom.Id }));
        Assert.Contains("gender", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransferAsync_TargetRoomHasNoRemainingCapacity_Throws()
    {
        // Capacity 2, target room already has 1 occupant (Status Occupied, not Full — 1 free
        // seat) — transferring a 2-person group there needs 2 seats, so it must be rejected on
        // capacity specifically, distinct from the room being outright unavailable (Full/Maintenance).
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 2);
        var groupRoom = ctx.AddRoom(1000, building.Id, "101");
        var targetRoom = ctx.AddRoom(1001, building.Id, "102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var groupAllocation = ctx.AddAllocation(1000, groupRoom.Id, housingGroupId: group.Id);

        var occupyingRequest = ctx.AddRequest(1002, "student-occupying", cycle.Id, gov.Id, Gender.Female);
        ctx.AddAllocation(1001, targetRoom.Id, housingRequestId: occupyingRequest.Id); // 1 of 2 seats taken

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.TransferAsync(groupAllocation.Id, new TransferAllocationDto { NewRoomId = targetRoom.Id }));
        Assert.Contains("capacity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransferAsync_UnknownAllocation_ReturnsNull()
    {
        using var ctx = new TestContext();
        var result = await ctx.AllocationService.TransferAsync(999999, new TransferAllocationDto { NewRoomId = 1 });
        Assert.Null(result);
    }

    [Fact]
    public async Task VacateAsync_FreesRoomAndNotifiesOccupant()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);
        var allocation = ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id);

        var result = await ctx.AllocationService.VacateAsync(allocation.Id, new VacateAllocationDto());

        Assert.NotNull(result);
        Assert.NotNull(result!.VacatedAt);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Contains(ctx.Notifications.Sent, n => n.StudentIds is not null && n.StudentIds.Contains("student-1"));
    }

    [Fact]
    public async Task VacateAsync_AlreadyVacated_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female);
        var room = ctx.AddRoom(1000, building.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingRequestId: 1, vacatedAt: ctx.Clock.GetUtcNow().UtcDateTime);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.VacateAsync(allocation.Id, new VacateAllocationDto()));
    }

    [Fact]
    public async Task VacateAsync_UnknownAllocation_ReturnsNull()
    {
        using var ctx = new TestContext();
        var result = await ctx.AllocationService.VacateAsync(999999, new VacateAllocationDto());
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveGroupMemberAsync_NotLastMember_KeepsRestHoused_RecomputesOccupancy()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        var result = await ctx.AllocationService.RemoveGroupMemberAsync(allocation.Id, "member-2");

        Assert.NotNull(result);
        Assert.Null(result!.VacatedAt);
        Assert.DoesNotContain("member-2", result.OccupantStudentIds);
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Null(ctx.Db.HousingRequests.Single(r => r.StudentId == "member-2").HousingGroupId);
    }

    [Fact]
    public async Task RemoveGroupMemberAsync_LastMember_FullyVacatesAllocationAndFreesRoom()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        var result = await ctx.AllocationService.RemoveGroupMemberAsync(allocation.Id, "leader");

        Assert.NotNull(result);
        Assert.NotNull(result!.VacatedAt);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Null(ctx.Db.HousingGroups.SingleOrDefault(g => g.Id == group.Id)); // group deleted
    }

    [Fact]
    public async Task RemoveGroupMemberAsync_IndividualAllocation_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female);
        var room = ctx.AddRoom(1000, building.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingRequestId: 1);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.RemoveGroupMemberAsync(allocation.Id, "someone"));
        Assert.Contains("individual", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveGroupMemberAsync_StudentNotInGroup_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.RemoveGroupMemberAsync(allocation.Id, "not-a-member"));
    }

    [Fact]
    public async Task GetHistoryForStudentAsync_ReturnsBothActiveAndVacatedAllocations()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room1 = ctx.AddRoom(1000, building.Id, "101");
        var room2 = ctx.AddRoom(1001, building.Id, "102");
        var cycleA = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var oldRequest = ctx.AddRequest(1000, "student-h", cycleA.Id, gov.Id, Gender.Female);
        ctx.AddAllocation(1000, room1.Id, housingRequestId: oldRequest.Id, vacatedAt: ctx.Clock.GetUtcNow().UtcDateTime);

        var cycleB = ctx.Db.HousingCycles.Add(new HousingService.Domain.Entities.HousingCycle
        {
            Id = 2,
            Name = "Cycle2",
            Status = HousingCycleStatus.Closed,
            CreatedAt = ctx.Clock.GetUtcNow().UtcDateTime
        }).Entity;
        ctx.Db.SaveChanges();

        var newRequest = ctx.AddRequest(1001, "student-h", cycleB.Id, gov.Id, Gender.Female); // different cycle, same student
        ctx.AddAllocation(1001, room2.Id, housingRequestId: newRequest.Id);

        var history = await ctx.AllocationService.GetHistoryForStudentAsync("student-h");

        Assert.Equal(2, history.Count);
        Assert.Contains(history, a => a.VacatedAt is not null);
        Assert.Contains(history, a => a.VacatedAt is null);
    }

    [Fact]
    public async Task VacateStudentAsync_IndividualStudent_EndsTheirAllocation()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);
        var allocation = ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id);

        var result = await ctx.AllocationService.VacateStudentAsync("student-1", new VacateAllocationDto());

        Assert.NotNull(result);
        Assert.Equal(allocation.Id, result!.Id);
        Assert.NotNull(result.VacatedAt);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
    }

    [Fact]
    public async Task VacateStudentAsync_GroupedStudentWithRoommates_KeepsRestHoused()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        var result = await ctx.AllocationService.VacateStudentAsync("member-2", new VacateAllocationDto());

        Assert.NotNull(result);
        Assert.Equal(allocation.Id, result!.Id);
        Assert.Null(result.VacatedAt);
        Assert.DoesNotContain("member-2", result.OccupantStudentIds);
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
    }

    [Fact]
    public async Task VacateStudentAsync_StudentNotHoused_ReturnsNull()
    {
        using var ctx = new TestContext();
        var result = await ctx.AllocationService.VacateStudentAsync("nobody", new VacateAllocationDto());
        Assert.Null(result);
    }

    [Fact]
    public async Task TransferStudentAsync_IndividualStudent_MovesTheirAllocation()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var oldRoom = ctx.AddRoom(1000, building.Id, "101");
        var newRoom = ctx.AddRoom(1001, building.Id, "102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);
        var allocation = ctx.AddAllocation(1000, oldRoom.Id, housingRequestId: request.Id);

        var result = await ctx.AllocationService.TransferStudentAsync("student-1", new TransferAllocationDto { NewRoomId = newRoom.Id });

        Assert.NotNull(result);
        Assert.Equal(allocation.Id, result!.Id); // same allocation, just moved
        Assert.Equal(newRoom.Id, result.RoomId);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == oldRoom.Id).Status);
    }

    [Fact]
    public async Task TransferStudentAsync_GroupedStudentWithRoommates_SplitsIntoOwnNewAllocation()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var oldRoom = ctx.AddRoom(1000, building.Id, "101");
        var newRoom = ctx.AddRoom(1001, building.Id, "102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var groupAllocation = ctx.AddAllocation(1000, oldRoom.Id, housingGroupId: group.Id);

        var result = await ctx.AllocationService.TransferStudentAsync("member-2", new TransferAllocationDto { NewRoomId = newRoom.Id });

        Assert.NotNull(result);
        Assert.NotEqual(groupAllocation.Id, result!.Id); // a brand-new individual allocation, not the group's
        Assert.Equal(newRoom.Id, result.RoomId);
        Assert.Equal(["member-2"], result.OccupantStudentIds);

        // The rest of the group stays behind in the old room.
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == oldRoom.Id).Status);
        var refreshedGroupAllocation = ctx.Db.Allocations.Single(a => a.Id == groupAllocation.Id);
        Assert.Null(refreshedGroupAllocation.VacatedAt);
        var refreshedGroup = ctx.Db.HousingGroups.Single(g => g.Id == group.Id);
        Assert.DoesNotContain(refreshedGroup.Members, m => m.StudentId == "member-2");

        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == newRoom.Id).Status);
        Assert.Null(ctx.Db.HousingRequests.Single(r => r.StudentId == "member-2").HousingGroupId);
    }

    [Fact]
    public async Task TransferStudentAsync_GroupedOnlyMember_MovesWholeAllocation()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var oldRoom = ctx.AddRoom(1000, building.Id, "101");
        var newRoom = ctx.AddRoom(1001, building.Id, "102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var groupAllocation = ctx.AddAllocation(1000, oldRoom.Id, housingGroupId: group.Id);

        var result = await ctx.AllocationService.TransferStudentAsync("leader", new TransferAllocationDto { NewRoomId = newRoom.Id });

        Assert.NotNull(result);
        Assert.Equal(groupAllocation.Id, result!.Id); // same (group) allocation, just moved
        Assert.Equal(newRoom.Id, result.RoomId);
    }

    [Fact]
    public async Task TransferStudentAsync_StudentNotHoused_ReturnsNull()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female);
        var room = ctx.AddRoom(1000, building.Id);
        var result = await ctx.AllocationService.TransferStudentAsync("nobody", new TransferAllocationDto { NewRoomId = room.Id });
        Assert.Null(result);
    }

    // --- GetCandidateRoomsAsync: must also serve already-housed targets (transfer scenario) ---

    [Fact]
    public async Task GetCandidateRoomsAsync_IndividualAlreadyHoused_ReturnsRoomsExcludingCurrent()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var oldRoom = ctx.AddRoom(1000, building.Id, "T101");
        var newRoom = ctx.AddRoom(1001, building.Id, "T102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, oldRoom.Id, housingRequestId: request.Id);

        var rooms = await ctx.AllocationService.GetCandidateRoomsAsync(request.Id, null);

        Assert.Contains(rooms, r => r.RoomId == newRoom.Id);
        Assert.DoesNotContain(rooms, r => r.RoomId == oldRoom.Id);
    }

    [Fact]
    public async Task GetCandidateRoomsAsync_GroupAlreadyHoused_ReturnsRoomsExcludingCurrent()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var oldRoom = ctx.AddRoom(1000, building.Id, "T101");
        var newRoom = ctx.AddRoom(1001, building.Id, "T102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, oldRoom.Id, housingGroupId: group.Id);

        var rooms = await ctx.AllocationService.GetCandidateRoomsAsync(null, group.Id);

        Assert.Contains(rooms, r => r.RoomId == newRoom.Id);
        Assert.DoesNotContain(rooms, r => r.RoomId == oldRoom.Id);
        Assert.All(rooms, r => Assert.True(r.RemainingCapacity >= 2)); // must fit the whole group
    }

    [Fact]
    public async Task GetCandidateRoomsAsync_NotAcceptedTarget_StillThrows()
    {
        using var ctx = new TestContext();
        ctx.AddBuilding(1000, Gender.Female);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female); // no decision

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.GetCandidateRoomsAsync(request.Id, null));
    }
}
