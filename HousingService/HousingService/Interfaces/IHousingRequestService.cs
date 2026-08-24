using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IHousingRequestService
{
    Task<HousingRequestDto> CreateAsync(string studentId, CreateHousingRequestDto dto);

    Task<IReadOnlyList<HousingRequestDto>> GetMineAsync(string studentId);

    Task<HousingRequestDto?> GetMineByIdAsync(string studentId, int id);

    Task<PagedResult<HousingRequestDto>> GetAllAsync(int? housingCycleId, int? governorateId, HousingRequestStatus? status, AdmissionDecisionStatus? admissionStatus, PaginationParams pagination);

    Task<HousingRequestDto?> GetByIdAsync(int id);

    Task<bool?> UpdateMineAsync(string studentId, int id, UpdateHousingRequestDto dto);

    Task<bool?> ReviewDocumentAsync(int requestId, int documentId, ReviewDocumentDto dto, string reviewedBy);

    Task<HousingRequestDto?> MakeDecisionAsync(int requestId, MakeAdmissionDecisionDto dto, string reviewedBy);
}
