using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Data.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(int id);
    Task<IEnumerable<TEntity>> GetAllAsync();
    Task AddAsync(TEntity entity);
    Task AddRangeAsync(IEnumerable<TEntity> entities);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
    Task SaveChangesAsync();
}

public interface IBuildingRepository : IRepository<Building>
{
    Task<Building?> GetByNameAsync(string name);
    Task<IEnumerable<Building>> GetActiveAsync();
}

public interface IRoomRepository : IRepository<Room>
{
    Task<IEnumerable<Room>> GetByBuildingIdAsync(int buildingId);
    Task<Room?> GetByBuildingAndRoomNumberAsync(int buildingId, string roomNumber);
    Task<IEnumerable<Room>> GetAvailableRoomsAsync(int buildingId);
    Task<Room?> GetByIdWithOccupantsAsync(int id);
    Task<IEnumerable<Room>> GetByBuildingIdWithOccupantsAsync(int buildingId);
    Task<IEnumerable<Room>> GetAllWithOccupantsAsync();
}

public interface IHousingCycleRepository : IRepository<HousingCycle>
{
    Task<HousingCycle?> GetOpenAsync();
    Task<HousingCycle?> GetByNameAsync(string name);
}

public interface IHousingRequestRepository : IRepository<HousingRequest>
{
    Task<IEnumerable<HousingRequest>> GetByStudentIdAsync(string studentId);
    Task<HousingRequest?> GetByStudentAndCycleAsync(string studentId, int housingCycleId);
    Task<IEnumerable<HousingRequest>> GetByStatusAsync(HousingRequestStatus status);
    Task<IEnumerable<HousingRequest>> GetByGroupIdAsync(int groupId);
    Task<HousingRequest?> GetByIdWithDocumentsAsync(int id);
    Task<(IEnumerable<HousingRequest> Items, int TotalCount)> GetAllWithFiltersAsync(HousingRequestFilterParams filter, PaginationParams pagination);
    /// <summary>Accepted, unpaid requests whose payment reminder hasn't been sent yet and whose
    /// due date falls before <paramref name="dueDateCutoffExclusive"/>.</summary>
    Task<IEnumerable<HousingRequest>> GetDueForPaymentReminderAsync(DateTime dueDateCutoffExclusive);
}

public interface IHousingRequestDocumentRepository : IRepository<HousingRequestDocument>
{
    Task<IEnumerable<HousingRequestDocument>> GetByHousingRequestIdAsync(int requestId);
    Task<IEnumerable<HousingRequestDocument>> GetByTypeAsync(DocumentType type);
    Task<IEnumerable<HousingRequestDocument>> GetByReviewStatusAsync(DocumentReviewStatus status);
    Task<HousingRequestDocument?> GetByIdWithRequestAsync(int id);
}

public interface IGovernorateRepository : IRepository<Governorate>
{
    Task<Governorate?> GetByNameAsync(string name);
}

public interface IHousingSettingsRepository : IRepository<HousingSettings>
{
    /// <summary>The single settings row, created with defaults if it somehow doesn't exist yet.</summary>
    Task<HousingSettings> GetAsync();
}

public interface IHousingGroupRepository : IRepository<HousingGroup>
{
    Task<HousingGroup?> GetByLeaderIdAsync(string leaderId);
    Task<IEnumerable<HousingGroup>> GetByStatusAsync(HousingGroupStatus status);
    Task<IEnumerable<HousingGroup>> GetGroupsWithMembersAsync();
    Task<HousingGroup?> GetByCodeAsync(string code);
    Task<HousingGroup?> GetByIdWithDetailsAsync(int id);
    Task<(IEnumerable<HousingGroup> Items, int TotalCount)> GetAllWithDetailsAsync(int? housingCycleId, PaginationParams pagination);
    Task<HousingGroup?> GetByIdWithMembersAndDecisionsAsync(int id);
}

public interface IGroupInvitationRepository : IRepository<GroupInvitation>
{
    Task<IEnumerable<GroupInvitation>> GetByInvitedStudentIdAsync(string studentId);
    Task<IEnumerable<GroupInvitation>> GetByGroupIdAsync(int groupId);
    Task<IEnumerable<GroupInvitation>> GetPendingInvitationsAsync(string studentId);
    Task<GroupInvitation?> GetPendingByGroupAndStudentAsync(int groupId, string studentId);
}

public interface IAdmissionDecisionRepository : IRepository<AdmissionDecision>
{
    Task<AdmissionDecision?> GetByHousingRequestIdAsync(int requestId);
    Task<IEnumerable<AdmissionDecision>> GetByStatusAsync(AdmissionDecisionStatus status);
    Task<IEnumerable<AdmissionDecision>> GetAcceptedDecisionsAsync();
}

public interface IAllocationRepository : IRepository<Allocation>
{
    /// <summary>Active (not vacated) allocation for this request, if any.</summary>
    Task<Allocation?> GetByHousingRequestIdAsync(int requestId);
    /// <summary>Active (not vacated) allocation for this group, if any.</summary>
    Task<Allocation?> GetByGroupIdAsync(int groupId);
    Task<IEnumerable<Allocation>> GetByRoomIdAsync(int roomId);
    Task<Allocation?> GetByIdWithDetailsAsync(int id);
    Task<(IEnumerable<Allocation> Items, int TotalCount)> GetAllWithDetailsAsync(int? buildingId, int? roomId, PaginationParams pagination);
    Task<IEnumerable<Allocation>> GetActiveByBuildingIdAsync(int buildingId);
    /// <summary>Every allocation (active or vacated) this student has ever had, individually or via a group they currently belong to.</summary>
    Task<IEnumerable<Allocation>> GetHistoryByStudentIdAsync(string studentId);
    /// <summary>The student's current active (not vacated) allocation, individually or via a group they currently belong to, if any.</summary>
    Task<Allocation?> GetActiveByStudentIdAsync(string studentId);
}
