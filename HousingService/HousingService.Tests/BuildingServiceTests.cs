using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class BuildingServiceTests
{
    private static UpdateBuildingDto MakeDto(HousingService.Domain.Entities.Building building, BuildingStatus status) => new()
    {
        Name = building.Name,
        Gender = building.Gender,
        Status = status,
        StandardRoomCapacity = building.StandardRoomCapacity
    };

    [Fact]
    public async Task UpdateAsync_CannotSetInactiveWhileResidentsAreHoused_Throws()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);
        var room = ctx.AddRoom(1000, building.Id);
        ctx.AddAllocation(1000, room.Id, housingRequestId: 1);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.BuildingService.UpdateAsync(building.Id, MakeDto(building, BuildingStatus.Inactive)));
        Assert.Contains("evacuat", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(BuildingStatus.Active, ctx.Db.Buildings.Single(b => b.Id == building.Id).Status);
    }

    [Fact]
    public async Task UpdateAsync_CanSetInactiveWhenNoResidents()
    {
        using var ctx = new TestContext();
        var building = ctx.AddBuilding(1000, Gender.Female, capacity: 4);

        var updated = await ctx.BuildingService.UpdateAsync(building.Id, MakeDto(building, BuildingStatus.Inactive));

        Assert.True(updated);
        Assert.Equal(BuildingStatus.Inactive, ctx.Db.Buildings.Single(b => b.Id == building.Id).Status);
    }

    [Fact]
    public async Task GetLookupAsync_ExposesIdNameAndFloorsCountOnly()
    {
        using var ctx = new TestContext();
        ctx.AddBuilding(1000, Gender.Female, capacity: 4, floorsCount: 6);

        var lookup = await ctx.BuildingService.GetLookupAsync();

        var b = Assert.Single(lookup, x => x.Id == 1000);
        Assert.Equal("TestBuilding1000", b.Name);
        Assert.Equal(6, b.FloorsCount);
    }
}
