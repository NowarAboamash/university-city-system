using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IAllocationService
{
    Task<IReadOnlyList<CandidateRoomDto>> GetCandidateRoomsAsync(int? housingRequestId, int? housingGroupId);

    Task<AllocationDto> CreateAsync(CreateAllocationDto dto);

    Task<AllocationDto?> GetByIdAsync(int id);

    Task<PagedResult<AllocationDto>> GetAllAsync(int? buildingId, int? roomId, PaginationParams pagination);

    Task<AllocationDto?> GetMineAsync(string studentId);
}
