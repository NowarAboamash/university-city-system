using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IBuildingEvacuationService
{
    /// <returns>null if the building doesn't exist; otherwise how many residents were notified.</returns>
    Task<int?> AnnounceAsync(int buildingId, AnnounceEvacuationDto dto);

    /// <returns>null if the building doesn't exist.</returns>
    Task<EvacuationResultDto?> ExecuteAsync(int buildingId, ExecuteEvacuationDto dto);
}
