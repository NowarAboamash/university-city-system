using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class UnpaidEvictionTests
{
    // Clock is fixed at 2026-09-01T00:00:00Z, so "today" is 2026-09-01.
    private static DateTime Yesterday(TestContext ctx) => ctx.Clock.GetUtcNow().UtcDateTime.Date.AddDays(-1);

    [Fact]
    public async Task Evicts_IndividualOverdueUnpaid_RejectsNonPayment_AndFreesRoom()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id, "101");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: Yesterday(ctx));
        ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id);

        var evicted = await ctx.UnpaidEvictionService.RunAsync();

        Assert.Equal(1, evicted);
        var decision = ctx.Db.AdmissionDecisions.Single(d => d.HousingRequestId == request.Id);
        Assert.Equal(AdmissionDecisionStatus.Rejected, decision.Status);
        Assert.Equal(RejectionReason.NonPayment, decision.RejectionReason);
        Assert.NotNull(ctx.Db.Allocations.Single(a => a.HousingRequestId == request.Id).VacatedAt);
        Assert.Equal(RoomStatus.Available, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
        Assert.Contains(ctx.Notifications.Sent, n => n.Body.Contains("لم يتم دفع رسوم السكن خلال المهلة"));
    }

    [Fact]
    public async Task DoesNotEvict_WhenDueDateIsTodayOrLater()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var today = ctx.Clock.GetUtcNow().UtcDateTime.Date;
        ctx.AddRequest(1000, "today", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: today);
        ctx.AddRequest(1001, "future", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: today.AddDays(10));

        var evicted = await ctx.UnpaidEvictionService.RunAsync();

        Assert.Equal(0, evicted);
    }

    [Fact]
    public async Task DoesNotEvict_Paid_Or_NotAccepted()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        ctx.AddRequest(1000, "paid", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: Yesterday(ctx), isPaid: true);
        ctx.AddRequest(1001, "waitlist", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.WaitingList, paymentDueDate: Yesterday(ctx));
        ctx.AddRequest(1002, "no-due-date", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted);

        var evicted = await ctx.UnpaidEvictionService.RunAsync();

        Assert.Equal(0, evicted);
    }

    [Fact]
    public async Task Evicts_OnlyTheUnpaidGroupMember_KeepsRoommatesHoused()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id, "101");
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "leader", cycle.Id, HousingGroupStatus.Allocated);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: Yesterday(ctx), isPaid: true);
        var unpaid = ctx.AddRequest(1001, "mate", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: Yesterday(ctx));
        ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        var evicted = await ctx.UnpaidEvictionService.RunAsync();

        Assert.Equal(1, evicted);
        Assert.Null(ctx.Db.HousingRequests.Single(r => r.Id == unpaid.Id).HousingGroupId); // dropped from group
        Assert.Equal(RejectionReason.NonPayment, ctx.Db.AdmissionDecisions.Single(d => d.HousingRequestId == unpaid.Id).RejectionReason);

        var allocation = ctx.Db.Allocations.Single(a => a.HousingGroupId == group.Id);
        Assert.Null(allocation.VacatedAt); // roommates stay
        Assert.Equal(RoomStatus.Occupied, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
    }

    [Fact]
    public async Task Evicts_AcceptedOverdue_EvenWithNoAllocation()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "never-housed", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: Yesterday(ctx));

        var evicted = await ctx.UnpaidEvictionService.RunAsync();

        Assert.Equal(1, evicted);
        var decision = ctx.Db.AdmissionDecisions.Single(d => d.HousingRequestId == request.Id);
        Assert.Equal(AdmissionDecisionStatus.Rejected, decision.Status);
        Assert.Equal(RejectionReason.NonPayment, decision.RejectionReason);
    }

    [Fact]
    public async Task Run_NothingOverdue_ReturnsZero()
    {
        using var ctx = new TestContext();
        ctx.AddOpenCycle(1000);
        var evicted = await ctx.UnpaidEvictionService.RunAsync();
        Assert.Equal(0, evicted);
    }
}
