using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IGovernorateService
{
    Task<GovernorateDto> CreateAsync(CreateGovernorateDto dto);

    Task<IReadOnlyList<GovernorateDto>> GetAllAsync();

    Task<GovernorateDto?> GetByIdAsync(int id);

    Task<bool> UpdateAsync(int id, CreateGovernorateDto dto);
}
