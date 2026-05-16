using AdvertisingService.DTOs;
using AdvertisingService.Enums;

namespace AdvertisingService.Interfaces;

public interface IAdvertisementService
{
    Task<AdvertisementDto> CreateAsync(CreateAdvertisementDto dto, Guid createdBy);

    Task<IReadOnlyList<AdvertisementDto>> GetAllAsync();

    Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(TargetGender targetGender, int? collegeId, int? governorateId);

    Task<AdvertisementDto?> GetByIdAsync(Guid id);

    Task<bool> UpdateAsync(Guid id, UpdateAdvertisementDto dto);

    Task<bool> DeleteAsync(Guid id);
}
