using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IAllocationService
{
    Task<IReadOnlyList<CandidateRoomDto>> GetCandidateRoomsAsync(int? housingRequestId, int? housingGroupId);

    Task<AllocationDto> CreateAsync(CreateAllocationDto dto);

    Task<AllocationDto?> GetByIdAsync(int id);

    Task<PagedResult<AllocationDto>> GetAllAsync(int? buildingId, int? roomId, PaginationParams pagination);

    Task<AllocationDto?> GetMineAsync(string studentId);

    /// <returns>null if the allocation was not found. Throws ArgumentException for validation failures (already vacated, same room, gender/capacity mismatch, room not found).</returns>
    Task<AllocationDto?> TransferAsync(int allocationId, TransferAllocationDto dto);

    /// <summary>Removes a single allocation's occupant(s) from their room without assigning a new one (frees the room's capacity).</summary>
    /// <returns>null if the allocation was not found. Throws ArgumentException if already vacated.</returns>
    Task<AllocationDto?> VacateAsync(int allocationId, VacateAllocationDto dto);
}
