using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class AutoAssignTests
{
    // Every test here controls its own room pool, so wipe HousingDbContext's 5280-room seed.
    private static TestContext NewContext()
    {
        var ctx = new TestContext();
        ctx.ClearSeededInventory();
        return ctx;
    }

    [Fact]
    public async Task DryRun_ReturnsPlan_WritesNothing()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        ctx.AddRoom(1000, building.Id, "101");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "s2", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true });

        Assert.True(result.DryRun);
        Assert.Equal(2, result.PlacedTargets);
        Assert.Equal(2, result.HousedStudents);
        Assert.Equal(2, result.Assignments.Count);
        Assert.Empty(ctx.Db.Allocations);
        Assert.All(ctx.Db.Rooms, r => Assert.Equal(RoomStatus.Available, r.Status));
        Assert.Empty(ctx.Notifications.Sent);
    }

    [Fact]
    public async Task Commit_PlacesIndividualsAndGroup_Persists_AndNotifies()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        var roomA = ctx.AddRoom(1000, building.Id, "101");
        var roomB = ctx.AddRoom(1001, building.Id, "102");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "mate", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1002, "solo", cycle.Id, gov.Id, Gender.Male, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = false });

        Assert.False(result.DryRun);
        Assert.Equal(2, result.PlacedTargets);   // one group + one individual
        Assert.Equal(3, result.HousedStudents);  // 2 in the group + 1 solo
        Assert.Equal(2, ctx.Db.Allocations.Count(a => a.VacatedAt == null));

        var groupAlloc = ctx.Db.Allocations.Single(a => a.HousingGroupId == group.Id);
        var soloAlloc = ctx.Db.Allocations.Single(a => a.HousingRequestId == 1002);
        // Consolidation: the solo fills the group's leftover bed rather than opening a fresh room.
        Assert.Equal(groupAlloc.RoomId, soloAlloc.RoomId);
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == groupAlloc.RoomId).Status); // 3 of 4
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == roomB.Id).Status);
        Assert.Equal(HousingGroupStatus.Allocated, ctx.Db.HousingGroups.Single(g => g.Id == group.Id).Status);
        Assert.Contains(ctx.Notifications.Sent, n => n.Title.Contains("تخصيص"));
    }

    [Fact]
    public async Task Groups_Placed_LargestFirst_IntoTightestFittingRoom()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var roomEmpty = ctx.AddRoom(1000, building.Id, "101");            // remaining 4
        var roomTight = ctx.AddRoom(1001, building.Id, "102");            // remaining 2 after the seed below
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        // Pre-occupy roomTight with two individuals so it has exactly 2 free beds.
        ctx.AddRequest(900, "occ1", cycle.Id, gov.Id, Gender.Female);
        ctx.AddRequest(901, "occ2", cycle.Id, gov.Id, Gender.Female);
        ctx.AddAllocation(900, roomTight.Id, housingRequestId: 900);
        ctx.AddAllocation(901, roomTight.Id, housingRequestId: 901);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "mate", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true });

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(group.Id, assignment.HousingGroupId);
        Assert.Equal(roomTight.Id, assignment.RoomId); // 2-bed gap beats the empty 4-bed room
    }

    [Fact]
    public async Task Individuals_Consolidate_IntoPartlyFullRoom()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var roomAlmostFull = ctx.AddRoom(1000, building.Id, "101"); // remaining 1
        var roomEmpty = ctx.AddRoom(1001, building.Id, "102");      // remaining 4
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        for (var i = 0; i < 3; i++)
        {
            ctx.AddRequest(900 + i, $"occ{i}", cycle.Id, gov.Id, Gender.Female);
            ctx.AddAllocation(900 + i, roomAlmostFull.Id, housingRequestId: 900 + i);
        }

        ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true });

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(roomAlmostFull.Id, assignment.RoomId);
    }

    [Fact]
    public async Task MaximizesHousedCount_ThenSkipsWhoDoesNotFit()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        ctx.AddRoom(1000, building.Id, "101"); // the only room: 4 beds
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "m2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1002, "m3", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1003, "solo-a", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1004, "solo-b", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = false });

        Assert.Equal(2, result.PlacedTargets);   // group of 3 + one solo
        Assert.Equal(4, result.HousedStudents);  // room filled exactly
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("individual", skipped.TargetType);
        Assert.Equal(RoomStatus.Full, ctx.Db.Rooms.Single(r => r.Id == 1000 + 100_000).Status);
    }

    [Fact]
    public async Task Skips_Group_WhenNotEveryMemberAccepted()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        ctx.AddRoom(1000, building.Id, "101");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "pending-mate", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.WaitingList);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true });

        Assert.Empty(result.Assignments);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("group", skipped.TargetType);
        Assert.Contains("accepted", skipped.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Skips_AlreadyHousedAccepted_Silently()
    {
        using var ctx = NewContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id, "101");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var housed = ctx.AddRequest(1000, "already", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, room.Id, housingRequestId: housed.Id);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true });

        Assert.Empty(result.Assignments);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public async Task Skips_Individual_WhenNoGenderMatchingRoomHasABed()
    {
        using var ctx = NewContext();
        var maleBuilding = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        ctx.AddRoom(1000, maleBuilding.Id, "101"); // only a male room exists
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        ctx.AddRequest(1000, "she", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true });

        Assert.Empty(result.Assignments);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public async Task Throws_WhenNoOpenCycle()
    {
        using var ctx = NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.AllocationService.AutoAssignAsync(new AutoAssignRequestDto { DryRun = true }));
    }
}
