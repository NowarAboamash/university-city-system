using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IRoomService
{
    Task<RoomDto?> CreateAsync(int buildingId, CreateRoomDto dto);

    Task<IReadOnlyList<RoomDto>?> GetByBuildingAsync(int buildingId);

    Task<RoomDto?> GetByIdAsync(int buildingId, int id);

    Task<bool?> UpdateAsync(int buildingId, int id, UpdateRoomDto dto);
}
