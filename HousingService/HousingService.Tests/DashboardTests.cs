using HousingService.Domain.Enums;

namespace HousingService.Tests;

public class DashboardTests
{
    [Fact]
    public async Task Dashboard_EmptySystem_ReturnsSeededRoomBaselineAndSevenDayTrend()
    {
        using var ctx = new TestContext();

        var d = await ctx.DashboardService.GetAsync();

        Assert.Equal(0, d.PendingRequests);
        Assert.Equal(0, d.OccupiedBeds);
        Assert.Equal(0, d.TotalHousedStudents);
        Assert.Equal(0.0, d.OccupancyRate);
        Assert.Empty(d.RecentRequests);

        // HousingDbContext seeds 20 buildings * 6 * 44 = 5280 rooms, all Available, capacity 4.
        Assert.Equal(5280, d.Rooms.Total);
        Assert.Equal(5280, d.Rooms.Available);
        Assert.Equal(0, d.Rooms.Occupied);
        Assert.Equal(0, d.Rooms.OutOfService);
        Assert.Equal(5280 * 4, d.TotalBeds);

        Assert.Equal(7, d.WeeklyOccupancy.Count);
        Assert.All(d.WeeklyOccupancy, p => Assert.Equal(0, p.OccupiedBeds));
        var today = DateOnly.FromDateTime(ctx.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal(today, d.WeeklyOccupancy[^1].Date);
        Assert.Equal(today.AddDays(-6), d.WeeklyOccupancy[0].Date);
        for (var i = 1; i < d.WeeklyOccupancy.Count; i++)
        {
            Assert.Equal(d.WeeklyOccupancy[i - 1].Date.AddDays(1), d.WeeklyOccupancy[i].Date);
        }
    }

    [Fact]
    public async Task Dashboard_PendingCountAndRecentRequests()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var group = ctx.AddGroup(1000, "s3", cycle.Id);

        ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Male);
        ctx.Clock.Advance(TimeSpan.FromHours(1));
        ctx.AddRequest(1001, "s2", cycle.Id, gov.Id, Gender.Male, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.Clock.Advance(TimeSpan.FromHours(1));
        ctx.AddRequest(1002, "s3", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id);

        var d = await ctx.DashboardService.GetAsync();

        Assert.Equal(2, d.PendingRequests); // 1000 and 1002 have no decision; 1001 does
        Assert.Equal(3, d.RecentRequests.Count);
        Assert.Equal(1002, d.RecentRequests[0].Id); // newest SubmittedAt first
        Assert.True(d.RecentRequests[0].IsGroup);
        Assert.Equal(AdmissionDecisionStatus.Pending, d.RecentRequests[0].AdmissionStatus);
        Assert.Equal(AdmissionDecisionStatus.Accepted, d.RecentRequests.Single(r => r.Id == 1001).AdmissionStatus);
    }

    [Fact]
    public async Task Dashboard_IndividualAllocation_CountsOneBedAndOneStudent()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var building = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var req = ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Male, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, room.Id, housingRequestId: req.Id);

        var d = await ctx.DashboardService.GetAsync();

        Assert.Equal(1, d.OccupiedBeds);
        Assert.Equal(1, d.TotalHousedStudents);
        Assert.Equal(1, d.Rooms.Occupied); // AddAllocation flipped the room to Occupied
        Assert.Equal(1, d.WeeklyOccupancy[^1].OccupiedBeds);
        Assert.Equal(Math.Round(1.0 / d.TotalBeds * 100, 1), d.OccupancyRate);
    }

    [Fact]
    public async Task Dashboard_GroupAllocation_CountsEachMemberAsABed()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var building = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var group = ctx.AddGroup(1000, "leader", cycle.Id);
        ctx.AddRequest(1000, "leader", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "m2", cycle.Id, gov.Id, Gender.Male, housingGroupId: group.Id, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, room.Id, housingGroupId: group.Id);

        var d = await ctx.DashboardService.GetAsync();

        Assert.Equal(2, d.OccupiedBeds);
        Assert.Equal(2, d.TotalHousedStudents);
    }

    [Fact]
    public async Task Dashboard_WeeklyTrend_ReflectsAllocationDate()
    {
        using var ctx = new TestContext(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var building = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var req = ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Male, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddAllocation(1000, room.Id, housingRequestId: req.Id); // AllocatedAt = 2026-09-01

        ctx.Clock.Advance(TimeSpan.FromDays(3)); // "today" is now 2026-09-04

        var d = await ctx.DashboardService.GetAsync();

        Assert.Equal(new DateOnly(2026, 8, 29), d.WeeklyOccupancy[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 4), d.WeeklyOccupancy[6].Date);
        Assert.Equal(0, d.WeeklyOccupancy[0].OccupiedBeds); // 08-29, before allocation
        Assert.Equal(0, d.WeeklyOccupancy[2].OccupiedBeds); // 08-31, before allocation
        Assert.Equal(1, d.WeeklyOccupancy[3].OccupiedBeds); // 09-01, allocation day
        Assert.Equal(1, d.WeeklyOccupancy[6].OccupiedBeds); // 09-04, still active
        Assert.Equal(1, d.OccupiedBeds);
    }

    [Fact]
    public async Task Dashboard_OutOfServiceRoom_ExcludedFromTotalBeds()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Male, capacity: 4);
        ctx.AddRoom(1000, building.Id, status: RoomStatus.Maintenance);

        var d = await ctx.DashboardService.GetAsync();

        Assert.Equal(1, d.Rooms.OutOfService);
        Assert.Equal(5281, d.Rooms.Total);
        Assert.Equal(5280 * 4, d.TotalBeds); // the maintenance room's 4 beds are not counted
    }
}
