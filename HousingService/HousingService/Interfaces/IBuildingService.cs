using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IBuildingService
{
    Task<BuildingDto> CreateAsync(CreateBuildingDto dto);

    Task<IReadOnlyList<BuildingDto>> GetAllAsync();

    Task<BuildingDto?> GetByIdAsync(int id);

    Task<bool> UpdateAsync(int id, UpdateBuildingDto dto);

    Task<IReadOnlyList<BuildingLookupDto>> GetLookupAsync();
}
