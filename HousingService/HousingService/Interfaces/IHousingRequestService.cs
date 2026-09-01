using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IHousingRequestService
{
    Task<HousingRequestDto> CreateAsync(string studentId, CreateHousingRequestDto dto);

    Task<IReadOnlyList<HousingRequestDto>> GetMineAsync(string studentId);

    Task<HousingRequestDto?> GetMineByIdAsync(string studentId, int id);

    Task<PagedResult<HousingRequestDto>> GetAllAsync(HousingRequestFilterParams filter, PaginationParams pagination);

    Task<HousingRequestDto?> GetByIdAsync(int id);

    Task<bool?> UpdateMineAsync(string studentId, int id, UpdateHousingRequestDto dto);

    Task<bool?> ReviewDocumentAsync(int requestId, int documentId, ReviewDocumentDto dto, string reviewedBy);

    Task<HousingRequestDto?> MakeDecisionAsync(int requestId, MakeAdmissionDecisionDto dto, string reviewedBy);

    /// <summary>Charges the housing fee from the student's wallet and marks the request paid on success.</summary>
    Task<PayHousingRequestResultDto> PayAsync(string studentId, int requestId);

    /// <summary>Financial roll-up for the admin payments dashboard.</summary>
    Task<PaymentSummaryDto> GetPaymentSummaryAsync(int? housingCycleId, DateTime? paidFrom, DateTime? paidTo);

    /// <returns>null if not found, true once deleted. Throws ArgumentException if the request (or its group) has an active room allocation.</returns>
    Task<bool?> DeleteAsync(int id, string performedBy, bool performedByAdmin);
}
