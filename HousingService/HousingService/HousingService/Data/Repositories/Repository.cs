using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HousingService.Data.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly HousingDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public Repository(HousingDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task AddAsync(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }

    public void Update(TEntity entity)
    {
        _dbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<TEntity> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

public class BuildingRepository : Repository<Building>, IBuildingRepository
{
    public BuildingRepository(HousingDbContext context) : base(context) { }

    public async Task<Building?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(b => b.Name == name);
    }

    public async Task<IEnumerable<Building>> GetActiveAsync()
    {
        return await _dbSet
            .Where(b => b.Status == BuildingStatus.Active)
            .ToListAsync();
    }
}

public class RoomRepository : Repository<Room>, IRoomRepository
{
    public RoomRepository(HousingDbContext context) : base(context) { }

    public async Task<IEnumerable<Room>> GetByBuildingIdAsync(int buildingId)
    {
        return await _dbSet
            .Where(r => r.BuildingId == buildingId)
            .ToListAsync();
    }

    public async Task<Room?> GetByBuildingAndRoomNumberAsync(int buildingId, string roomNumber)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.BuildingId == buildingId && r.RoomNumber == roomNumber);
    }

    public async Task<IEnumerable<Room>> GetAvailableRoomsAsync(int buildingId)
    {
        return await _dbSet
            .Include(r => r.Building)
            .Where(r => r.BuildingId == buildingId &&
                   r.CurrentOccupancy < r.Building.StandardRoomCapacity &&
                   (r.Status == RoomStatus.Available || r.Status == RoomStatus.Occupied))
            .ToListAsync();
    }

    public async Task<Room?> GetByIdWithOccupantsAsync(int id)
    {
        return await _dbSet
            .Include(r => r.Building)
            .Include(r => r.Allocations).ThenInclude(a => a.HousingRequest)
            .Include(r => r.Allocations).ThenInclude(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Room>> GetByBuildingIdWithOccupantsAsync(int buildingId)
    {
        return await _dbSet
            .Include(r => r.Building)
            .Include(r => r.Allocations).ThenInclude(a => a.HousingRequest)
            .Include(r => r.Allocations).ThenInclude(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .Where(r => r.BuildingId == buildingId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Room>> GetAllWithOccupantsAsync()
    {
        return await _dbSet
            .Include(r => r.Building)
            .Include(r => r.Allocations).ThenInclude(a => a.HousingRequest)
            .Include(r => r.Allocations).ThenInclude(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .ToListAsync();
    }
}

public class HousingCycleRepository : Repository<HousingCycle>, IHousingCycleRepository
{
    public HousingCycleRepository(HousingDbContext context) : base(context) { }

    public async Task<HousingCycle?> GetOpenAsync()
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.Status == HousingCycleStatus.Open);
    }

    public async Task<HousingCycle?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.Name == name);
    }
}

public class HousingRequestRepository : Repository<HousingRequest>, IHousingRequestRepository
{
    public HousingRequestRepository(HousingDbContext context) : base(context) { }

    public async Task<IEnumerable<HousingRequest>> GetByStudentIdAsync(string studentId)
    {
        return await _dbSet
            .Include(h => h.Documents)
            .Include(h => h.AdmissionDecision)
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.SubmittedAt)
            .ToListAsync();
    }

    public async Task<HousingRequest?> GetByStudentAndCycleAsync(string studentId, int housingCycleId)
    {
        return await _dbSet
            .Include(h => h.AdmissionDecision)
            .FirstOrDefaultAsync(h => h.StudentId == studentId && h.HousingCycleId == housingCycleId);
    }

    public async Task<IEnumerable<HousingRequest>> GetByStatusAsync(HousingRequestStatus status)
    {
        return await _dbSet
            .Where(h => h.Status == status)
            .ToListAsync();
    }

    public async Task<IEnumerable<HousingRequest>> GetByGroupIdAsync(int groupId)
    {
        return await _dbSet
            .Where(h => h.HousingGroupId == groupId)
            .ToListAsync();
    }

    public async Task<HousingRequest?> GetByIdWithDocumentsAsync(int id)
    {
        return await _dbSet
            .Include(h => h.Documents)
            .Include(h => h.AdmissionDecision)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<(IEnumerable<HousingRequest> Items, int TotalCount)> GetAllWithFiltersAsync(HousingRequestFilterParams filter, PaginationParams pagination)
    {
        var query = _dbSet.Include(h => h.Documents).Include(h => h.AdmissionDecision).AsQueryable();

        if (filter.HousingCycleId.HasValue)
        {
            query = query.Where(h => h.HousingCycleId == filter.HousingCycleId.Value);
        }

        if (filter.GovernorateId.HasValue)
        {
            query = query.Where(h => h.GovernorateId == filter.GovernorateId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(h => h.Status == filter.Status.Value);
        }

        if (filter.AdmissionStatus.HasValue)
        {
            query = query.Where(h => h.AdmissionDecision != null && h.AdmissionDecision.Status == filter.AdmissionStatus.Value);
        }

        if (filter.StudentIds is { Count: > 0 })
        {
            var studentIds = filter.StudentIds;
            query = query.Where(h => studentIds.Contains(h.StudentId));
        }

        if (filter.AcademicLevel.HasValue)
        {
            query = query.Where(h => h.AcademicLevel == filter.AcademicLevel.Value);
        }

        if (filter.Gender.HasValue)
        {
            query = query.Where(h => h.Gender == filter.Gender.Value);
        }

        if (filter.IsPaid.HasValue)
        {
            query = query.Where(h => h.IsPaid == filter.IsPaid.Value);
        }

        if (filter.HasSpecialNeeds.HasValue)
        {
            query = query.Where(h => h.HasSpecialNeeds == filter.HasSpecialNeeds.Value);
        }

        if (filter.IsPreviousResident.HasValue)
        {
            query = query.Where(h => h.IsPreviousResident == filter.IsPreviousResident.Value);
        }

        if (filter.IsGrouped == true)
        {
            query = query.Where(h => h.HousingGroupId != null);
        }
        else if (filter.IsGrouped == false)
        {
            query = query.Where(h => h.HousingGroupId == null);
        }

        if (filter.SubmittedFrom.HasValue)
        {
            query = query.Where(h => h.SubmittedAt >= filter.SubmittedFrom.Value);
        }

        if (filter.SubmittedTo.HasValue)
        {
            query = query.Where(h => h.SubmittedAt <= filter.SubmittedTo.Value);
        }

        query = query.OrderByDescending(h => h.SubmittedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<HousingRequest>> GetDueForPaymentReminderAsync(DateTime dueDateCutoffExclusive)
    {
        return await _dbSet
            .Include(h => h.AdmissionDecision)
            .Where(h => !h.IsPaid
                && !h.ReminderSent
                && h.PaymentDueDate != null
                && h.PaymentDueDate < dueDateCutoffExclusive
                && h.AdmissionDecision != null
                && h.AdmissionDecision.Status == AdmissionDecisionStatus.Accepted)
            .ToListAsync();
    }

    public async Task<PaymentSummaryData> GetPaymentSummaryAsync(int? housingCycleId, DateTime? paidFrom, DateTime? paidTo)
    {
        var accepted = _dbSet.Where(h =>
            h.AdmissionDecision != null && h.AdmissionDecision.Status == AdmissionDecisionStatus.Accepted);

        if (housingCycleId.HasValue)
        {
            accepted = accepted.Where(h => h.HousingCycleId == housingCycleId.Value);
        }

        var countAccepted = await accepted.CountAsync();
        var countPaid = await accepted.CountAsync(h => h.IsPaid);
        var totalRequired = await accepted.SumAsync(h => h.FeeAmount ?? 0m);
        var totalPaid = await accepted.Where(h => h.IsPaid).SumAsync(h => h.AmountPaid ?? h.FeeAmount ?? 0m);

        var inRange = accepted.Where(h => h.IsPaid);
        if (paidFrom.HasValue)
        {
            inRange = inRange.Where(h => h.PaidAt >= paidFrom.Value);
        }
        if (paidTo.HasValue)
        {
            inRange = inRange.Where(h => h.PaidAt <= paidTo.Value);
        }

        var countPaidInRange = await inRange.CountAsync();
        var paidInRange = await inRange.SumAsync(h => h.AmountPaid ?? h.FeeAmount ?? 0m);

        return new PaymentSummaryData(countAccepted, countPaid, totalRequired, totalPaid, countPaidInRange, paidInRange);
    }
}

public class HousingRequestDocumentRepository : Repository<HousingRequestDocument>, IHousingRequestDocumentRepository
{
    public HousingRequestDocumentRepository(HousingDbContext context) : base(context) { }

    public async Task<IEnumerable<HousingRequestDocument>> GetByHousingRequestIdAsync(int requestId)
    {
        return await _dbSet
            .Where(d => d.HousingRequestId == requestId)
            .ToListAsync();
    }

    public async Task<IEnumerable<HousingRequestDocument>> GetByTypeAsync(DocumentType type)
    {
        return await _dbSet
            .Where(d => d.Type == type)
            .ToListAsync();
    }

    public async Task<IEnumerable<HousingRequestDocument>> GetByReviewStatusAsync(DocumentReviewStatus status)
    {
        return await _dbSet
            .Where(d => d.ReviewStatus == status)
            .ToListAsync();
    }

    public async Task<HousingRequestDocument?> GetByIdWithRequestAsync(int id)
    {
        return await _dbSet
            .Include(d => d.HousingRequest)
            .FirstOrDefaultAsync(d => d.Id == id);
    }
}

public class GovernorateRepository : Repository<Governorate>, IGovernorateRepository
{
    public GovernorateRepository(HousingDbContext context) : base(context) { }

    public async Task<Governorate?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(g => g.Name == name);
    }
}

public class HousingSettingsRepository : Repository<HousingSettings>, IHousingSettingsRepository
{
    public HousingSettingsRepository(HousingDbContext context) : base(context) { }

    public async Task<HousingSettings> GetAsync()
    {
        var settings = await _dbSet.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings is null)
        {
            // Defensive: the row is seeded via migration/HasData, but never let a missing
            // row take down the reminder job or the settings endpoint.
            settings = new HousingSettings { Id = 1 };
            await _dbSet.AddAsync(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }
}

public class HousingGroupRepository : Repository<HousingGroup>, IHousingGroupRepository
{
    public HousingGroupRepository(HousingDbContext context) : base(context) { }

    public async Task<HousingGroup?> GetByLeaderIdAsync(string leaderId)
    {
        return await _dbSet
            .Include(g => g.Members)
            .Include(g => g.Invitations)
            .FirstOrDefaultAsync(g => g.LeaderId == leaderId);
    }

    public async Task<IEnumerable<HousingGroup>> GetByStatusAsync(HousingGroupStatus status)
    {
        return await _dbSet
            .Where(g => g.Status == status)
            .Include(g => g.Members)
            .ToListAsync();
    }

    public async Task<IEnumerable<HousingGroup>> GetGroupsWithMembersAsync()
    {
        return await _dbSet
            .Include(g => g.Members)
            .Where(g => g.Members.Any())
            .ToListAsync();
    }

    public async Task<HousingGroup?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Include(g => g.Members)
            .Include(g => g.Invitations)
            .FirstOrDefaultAsync(g => g.Code == code);
    }

    public async Task<HousingGroup?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(g => g.Members)
            .Include(g => g.Invitations)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<(IEnumerable<HousingGroup> Items, int TotalCount)> GetAllWithDetailsAsync(int? housingCycleId, PaginationParams pagination)
    {
        var query = _dbSet.Include(g => g.Members).Include(g => g.Invitations).AsQueryable();

        if (housingCycleId.HasValue)
        {
            query = query.Where(g => g.HousingCycleId == housingCycleId.Value);
        }

        query = query.OrderByDescending(g => g.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<HousingGroup?> GetByIdWithMembersAndDecisionsAsync(int id)
    {
        return await _dbSet
            .Include(g => g.Members).ThenInclude(m => m.AdmissionDecision)
            .Include(g => g.Allocation)
            .FirstOrDefaultAsync(g => g.Id == id);
    }
}

public class GroupInvitationRepository : Repository<GroupInvitation>, IGroupInvitationRepository
{
    public GroupInvitationRepository(HousingDbContext context) : base(context) { }

    public async Task<IEnumerable<GroupInvitation>> GetByInvitedStudentIdAsync(string studentId)
    {
        return await _dbSet
            .Where(i => i.InvitedStudentId == studentId)
            .Include(i => i.HousingGroup)
            .ToListAsync();
    }

    public async Task<IEnumerable<GroupInvitation>> GetByGroupIdAsync(int groupId)
    {
        return await _dbSet
            .Where(i => i.HousingGroupId == groupId)
            .ToListAsync();
    }

    public async Task<IEnumerable<GroupInvitation>> GetPendingInvitationsAsync(string studentId)
    {
        return await _dbSet
            .Where(i => i.InvitedStudentId == studentId && i.Status == InvitationStatus.Pending)
            .Include(i => i.HousingGroup)
            .ToListAsync();
    }

    public async Task<GroupInvitation?> GetPendingByGroupAndStudentAsync(int groupId, string studentId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(i => i.HousingGroupId == groupId && i.InvitedStudentId == studentId && i.Status == InvitationStatus.Pending);
    }
}

public class AdmissionDecisionRepository : Repository<AdmissionDecision>, IAdmissionDecisionRepository
{
    public AdmissionDecisionRepository(HousingDbContext context) : base(context) { }

    public async Task<AdmissionDecision?> GetByHousingRequestIdAsync(int requestId)
    {
        return await _dbSet
            .Include(a => a.HousingRequest)
            .FirstOrDefaultAsync(a => a.HousingRequestId == requestId);
    }

    public async Task<IEnumerable<AdmissionDecision>> GetByStatusAsync(AdmissionDecisionStatus status)
    {
        return await _dbSet
            .Where(a => a.Status == status)
            .Include(a => a.HousingRequest)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdmissionDecision>> GetAcceptedDecisionsAsync()
    {
        return await _dbSet
            .Where(a => a.Status == AdmissionDecisionStatus.Accepted)
            .Include(a => a.HousingRequest)
            .ToListAsync();
    }
}

public class AllocationRepository : Repository<Allocation>, IAllocationRepository
{
    public AllocationRepository(HousingDbContext context) : base(context) { }

    public async Task<Allocation?> GetByHousingRequestIdAsync(int requestId)
    {
        return await _dbSet
            .Include(a => a.Room).ThenInclude(r => r.Building)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.HousingRequestId == requestId && a.VacatedAt == null);
    }

    public async Task<Allocation?> GetByGroupIdAsync(int groupId)
    {
        return await _dbSet
            .Include(a => a.Room).ThenInclude(r => r.Building)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.HousingGroupId == groupId && a.VacatedAt == null);
    }

    public async Task<IEnumerable<Allocation>> GetByRoomIdAsync(int roomId)
    {
        return await _dbSet
            .Where(a => a.RoomId == roomId)
            .ToListAsync();
    }

    public async Task<Allocation?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(a => a.Room).ThenInclude(r => r.Building)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<(IEnumerable<Allocation> Items, int TotalCount)> GetAllWithDetailsAsync(int? buildingId, int? roomId, PaginationParams pagination)
    {
        var query = _dbSet
            .Include(a => a.Room).ThenInclude(r => r.Building)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .AsQueryable();

        if (buildingId.HasValue)
        {
            query = query.Where(a => a.Room.BuildingId == buildingId.Value);
        }

        if (roomId.HasValue)
        {
            query = query.Where(a => a.RoomId == roomId.Value);
        }

        query = query.OrderByDescending(a => a.AllocatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Allocation>> GetActiveByBuildingIdAsync(int buildingId)
    {
        return await _dbSet
            .Include(a => a.Room)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .Where(a => a.Room.BuildingId == buildingId && a.VacatedAt == null)
            .ToListAsync();
    }

    public async Task<IEnumerable<Allocation>> GetHistoryByStudentIdAsync(string studentId)
    {
        return await _dbSet
            .Include(a => a.Room).ThenInclude(r => r.Building)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .Where(a =>
                (a.HousingRequest != null && a.HousingRequest.StudentId == studentId) ||
                (a.HousingGroup != null && a.HousingGroup.Members.Any(m => m.StudentId == studentId)))
            .OrderByDescending(a => a.AllocatedAt)
            .ToListAsync();
    }

    public async Task<Allocation?> GetActiveByStudentIdAsync(string studentId)
    {
        return await _dbSet
            .Include(a => a.Room).ThenInclude(r => r.Building)
            .Include(a => a.HousingRequest)
            .Include(a => a.HousingGroup).ThenInclude(g => g!.Members)
            .Where(a => a.VacatedAt == null &&
                ((a.HousingRequest != null && a.HousingRequest.StudentId == studentId) ||
                 (a.HousingGroup != null && a.HousingGroup.Members.Any(m => m.StudentId == studentId))))
            .OrderByDescending(a => a.AllocatedAt)
            .FirstOrDefaultAsync();
    }
}
