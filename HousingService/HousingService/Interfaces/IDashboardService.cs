using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IDashboardService
{
    /// <summary>Builds the admin housing dashboard overview (counts, occupancy, recent requests, weekly trend).</summary>
    Task<HousingDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
