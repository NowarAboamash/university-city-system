using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class HousingGroupServiceTests
{
    [Fact]
    public async Task RespondToInvitationAsync_ApprovingIntoHousedGroup_UpdatesRoomStatus_NotifiesNewMember()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id); // 1 of 4 seats used

        ctx.AddRequest(1001, "new-member", cycle.Id, gov.Id, Gender.Female); // eligible joiner
        var invitation = ctx.AddPendingInvitation(1000, group.Id, "new-member");

        var result = await ctx.GroupService.RespondToInvitationAsync("leader", invitation.Id, new RespondToInvitationDto { Approve = true });

        Assert.True(result);
        Assert.Equal(group.Id, ctx.Db.HousingRequests.Single(r => r.StudentId == "new-member").HousingGroupId);
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Contains(ctx.Notifications.Sent, n => n.StudentId == "new-member" && n.Title.Contains("تخصيص"));
    }

    [Fact]
    public async Task RespondToInvitationAsync_ApprovingIntoHousedGroup_NoRemainingCapacity_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 1); // exactly 1 seat total
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Open); // MaxMembers 4, still "Open"
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id); // fills the room's only seat

        ctx.AddRequest(1001, "new-member", cycle.Id, gov.Id, Gender.Female);
        var invitation = ctx.AddPendingInvitation(1000, group.Id, "new-member");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.GroupService.RespondToInvitationAsync("leader", invitation.Id, new RespondToInvitationDto { Approve = true }));
        Assert.Contains("capacity", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Rejected join attempt must not have touched the room or the roster.
        Assert.Equal(RoomStatus.Full, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Null(ctx.Db.HousingRequests.Single(r => r.StudentId == "new-member").HousingGroupId);
    }

    [Fact]
    public async Task LeaveAsync_LastMemberOfHousedGroup_VacatesAllocationAndFreesRoom()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        var left = await ctx.GroupService.LeaveAsync("leader");

        Assert.True(left);
        Assert.Null(ctx.Db.HousingGroups.SingleOrDefault(g => g.Id == group.Id));
        Assert.NotNull(ctx.Db.Allocations.Single(a => a.Id == allocation.Id).VacatedAt);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
    }

    [Fact]
    public async Task RemoveMemberAsync_NonLastMemberOfHousedGroup_RecomputesRoomStatus_NotifiesRemaining()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id);
        var allocation = ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id); // 2 of 4 seats used

        var removed = await ctx.GroupService.RemoveMemberAsync(group.Id, "member-2");

        Assert.True(removed);
        Assert.Null(ctx.Db.Allocations.Single(a => a.Id == allocation.Id).VacatedAt);
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Contains(ctx.Notifications.Sent, n => n.StudentId == "leader" && n.Title.Contains("مغادرة"));
    }

    [Fact]
    public async Task RemoveMemberAsLeaderAsync_LeaderRemovesMember_DropsFromRoster_NotifiesRemovedStudent()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id);

        var result = await ctx.GroupService.RemoveMemberAsLeaderAsync("leader", "member-2");

        Assert.True(result);
        Assert.Null(ctx.Db.HousingRequests.Single(r => r.StudentId == "member-2").HousingGroupId);
        Assert.Equal("leader", ctx.Db.HousingGroups.Single(g => g.Id == group.Id).LeaderId);
        Assert.Contains(ctx.Notifications.Sent, n => n.StudentId == "member-2" && n.Title.Contains("إزالتك"));
    }

    [Fact]
    public async Task RemoveMemberAsLeaderAsync_NonLeaderCaller_ReturnsNull()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id);
        ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id);

        var result = await ctx.GroupService.RemoveMemberAsLeaderAsync("member-2", "leader");

        Assert.Null(result);
        Assert.Equal(group.Id, ctx.Db.HousingRequests.Single(r => r.StudentId == "leader").HousingGroupId);
    }

    [Fact]
    public async Task RemoveMemberAsLeaderAsync_LeaderTargetsSelf_Throws()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.GroupService.RemoveMemberAsLeaderAsync("leader", "leader"));
    }
}
