using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IAllocationService
{
    Task<IReadOnlyList<CandidateRoomDto>> GetCandidateRoomsAsync(int? housingRequestId, int? housingGroupId);

    Task<AllocationDto> CreateAsync(CreateAllocationDto dto);

    Task<AllocationDto?> GetByIdAsync(int id);

    Task<IReadOnlyList<AllocationDto>> GetAllAsync(int? buildingId, int? roomId);

    Task<AllocationDto?> GetMineAsync(string studentId);
}
