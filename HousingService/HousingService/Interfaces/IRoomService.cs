using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IRoomService
{
    Task<RoomDto?> CreateAsync(int buildingId, CreateRoomDto dto);

    Task<IReadOnlyList<RoomDto>?> GetByBuildingAsync(int buildingId);

    /// <summary>Minimal id/floor/roomNumber list for a student picker. Null if the building doesn't exist.</summary>
    Task<IReadOnlyList<RoomLookupDto>?> GetLookupByBuildingAsync(int buildingId);

    Task<RoomDto?> GetByIdAsync(int buildingId, int id);

    Task<bool?> UpdateAsync(int buildingId, int id, UpdateRoomDto dto);
}
