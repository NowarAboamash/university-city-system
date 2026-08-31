using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class RoomServiceTests
{
    [Fact]
    public async Task UpdateAsync_OccupiedRoom_CannotBeSetToAvailable_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);
        ctx.AddAllocation(1000, room.Id, housingRequestId: request.Id); // occupies it

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.RoomService.UpdateAsync(building.Id, room.Id, new UpdateRoomDto { RoomNumber = "101", Floor = 1, Status = RoomStatus.Available }));
        Assert.Contains("occupant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_EmptyRoom_CannotBeMarkedOccupied_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.RoomService.UpdateAsync(building.Id, room.Id, new UpdateRoomDto { RoomNumber = "101", Floor = 1, Status = RoomStatus.Occupied }));
        Assert.Contains("no active occupants", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_EmptyRoom_CanBeMarkedMaintenance()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);

        var result = await ctx.RoomService.UpdateAsync(building.Id, room.Id, new UpdateRoomDto { RoomNumber = "101", Floor = 1, Status = RoomStatus.Maintenance });

        Assert.True(result);
        Assert.Equal(RoomStatus.Maintenance, ctx.Db.Rooms.Single(r => r.Id == room.Id).Status);
    }

    [Fact]
    public async Task GetLookupByBuildingAsync_ReturnsMinimalRoomsOrderedByFloorThenNumber()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        ctx.AddRoom(1002, building.Id, "205", floor: 2);
        ctx.AddRoom(1001, building.Id, "104", floor: 1);
        ctx.AddRoom(1003, building.Id, "201", floor: 2, status: RoomStatus.Maintenance);

        var lookup = await ctx.RoomService.GetLookupByBuildingAsync(building.Id);

        Assert.NotNull(lookup);
        Assert.Equal(new[] { "104", "201", "205" }, lookup!.Select(r => r.RoomNumber));
        Assert.Equal(new[] { 1, 2, 2 }, lookup.Select(r => r.Floor));
        // RoomLookupDto exposes only id/floor/roomNumber — no status, no occupant ids
    }

    [Fact]
    public async Task GetLookupByBuildingAsync_UnknownBuilding_ReturnsNull()
    {
        using var ctx = new TestContext();
        var lookup = await ctx.RoomService.GetLookupByBuildingAsync(999999);
        Assert.Null(lookup);
    }
}
