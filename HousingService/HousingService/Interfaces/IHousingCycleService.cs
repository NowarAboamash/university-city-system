using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IHousingCycleService
{
    Task<HousingCycleDto> CreateAsync(CreateHousingCycleDto dto);

    Task<IReadOnlyList<HousingCycleDto>> GetAllAsync();

    Task<HousingCycleDto?> GetByIdAsync(int id);

    Task<HousingCycleDto?> GetCurrentOpenAsync();

    Task<bool> OpenAsync(int id);

    Task<bool> CloseAsync(int id);
}
