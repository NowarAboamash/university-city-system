using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class HousingRequestServiceTests
{
    [Fact]
    public async Task MakeDecisionAsync_IndividualReversedFromAccepted_VacatesActiveAllocation()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        var allocation = ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id);

        var result = await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Rejected }, "admin-1");

        Assert.NotNull(result);
        Assert.NotNull(ctx.Db.Allocations.Single(a => a.Id == allocation.Id).VacatedAt);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
    }

    [Fact]
    public async Task MakeDecisionAsync_GroupedMemberReversedToWaitingList_RemovesFromGroup()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        var memberRequest = ctx.AddRequest(1001, "member-2", cycle.Id, gov.Id, Gender.Female, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.RequestService.MakeDecisionAsync(memberRequest.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.WaitingList }, "admin-1");

        Assert.NotNull(result);
        Assert.Null(ctx.Db.HousingRequests.Single(r => r.Id == memberRequest.Id).HousingGroupId);
        var refreshedGroup = ctx.Db.HousingGroups.Single(g => g.Id == group.Id);
        Assert.DoesNotContain(refreshedGroup.Members, m => m.StudentId == "member-2");
    }

    [Fact]
    public async Task DeleteAsync_BlockedWhenActiveAllocationExists()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.RequestService.DeleteAsync(request.Id, "student-1", performedByAdmin: false));
        Assert.Contains("evacuated", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(ctx.Db.HousingRequests.Find(request.Id)); // still there, not deleted
    }

    [Fact]
    public async Task DeleteAsync_CascadesGroupLeaveAndCancelsPendingInvitations()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        // The deleting student is a non-leader member of an unhoused group (no allocation, so
        // the delete isn't blocked) alongside one other member.
        var ownGroup = ctx.AddGroup(1000, "other-member", cycle.Id);
        ctx.AddRequest(1000, "other-member", cycle.Id, gov.Id, Gender.Female, housingGroupId: ownGroup.Id);
        var deletingRequest = ctx.AddRequest(1001, "student-d", cycle.Id, gov.Id, Gender.Female, housingGroupId: ownGroup.Id);

        // The same student also has a pending join request against a completely different group.
        var otherGroup = ctx.AddGroup(1001, "other-leader", cycle.Id);
        var pendingInvitation = ctx.AddPendingInvitation(1000, otherGroup.Id, "student-d");

        var result = await ctx.RequestService.DeleteAsync(deletingRequest.Id, "student-d", performedByAdmin: false);

        Assert.True(result);
        Assert.Null(ctx.Db.HousingRequests.Find(deletingRequest.Id));

        var refreshedOwnGroup = ctx.Db.HousingGroups.Single(g => g.Id == ownGroup.Id);
        Assert.DoesNotContain(refreshedOwnGroup.Members, m => m.StudentId == "student-d");
        Assert.Single(refreshedOwnGroup.Members);

        Assert.Equal(InvitationStatus.Cancelled, ctx.Db.GroupInvitations.Single(i => i.Id == pendingInvitation.Id).Status);
    }

    [Fact]
    public async Task DeleteAsync_AdminDeletingSomeoneElse_NotifiesTheStudent()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);

        var result = await ctx.RequestService.DeleteAsync(request.Id, "admin-1", performedByAdmin: true);

        Assert.True(result);
        Assert.Contains(ctx.Notifications.Sent, n => n.StudentId == "student-1");
    }

    [Fact]
    public async Task DeleteAsync_UnknownRequest_ReturnsNull()
    {
        using var ctx = new TestContext();
        var result = await ctx.RequestService.DeleteAsync(999999, "admin-1", performedByAdmin: true);
        Assert.Null(result);
    }
}
