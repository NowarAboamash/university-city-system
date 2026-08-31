using HousingService.Data;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Users;

namespace HousingService.Services;

public class DashboardService : IDashboardService
{
    private const int RecentRequestsCount = 6;
    private const int TrendDays = 7;

    private readonly HousingDbContext _db;
    private readonly IUserLookupService _userLookup;
    private readonly TimeProvider _timeProvider;

    public DashboardService(HousingDbContext db, IUserLookupService userLookup, TimeProvider timeProvider)
    {
        _db = db;
        _userLookup = userLookup;
        _timeProvider = timeProvider;
    }

    private sealed record AllocationSnapshot(DateTime AllocatedAt, DateTime? VacatedAt, string? IndividualStudentId, List<string> GroupMemberIds)
    {
        public int Seats => IndividualStudentId is not null ? 1 : GroupMemberIds.Count;
        public IEnumerable<string> StudentIds => IndividualStudentId is not null ? [IndividualStudentId] : GroupMemberIds;
        public bool IsActiveOn(DateTime endOfDayUtc) => AllocatedAt <= endOfDayUtc && (VacatedAt is null || VacatedAt > endOfDayUtc);
    }

    public async Task<HousingDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var windowStart = today.AddDays(-(TrendDays - 1));
        var windowStartUtc = windowStart.ToDateTime(TimeOnly.MinValue);

        var pendingRequests = await _db.HousingRequests
            .CountAsync(r => r.AdmissionDecision == null, cancellationToken);

        // Room status breakdown (one grouped query, no row materialization).
        var roomCounts = await _db.Rooms
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountWhere(params RoomStatus[] statuses) => roomCounts.Where(x => statuses.Contains(x.Status)).Sum(x => x.Count);

        var rooms = new DashboardRoomStatusDto
        {
            Available = CountWhere(RoomStatus.Available),
            Occupied = CountWhere(RoomStatus.Occupied, RoomStatus.Full),
            OutOfService = CountWhere(RoomStatus.Maintenance, RoomStatus.Closed),
            Total = roomCounts.Sum(x => x.Count)
        };

        var totalBeds = await _db.Rooms
            .Where(r => r.Status != RoomStatus.Maintenance && r.Status != RoomStatus.Closed)
            .SumAsync(r => r.Building.StandardRoomCapacity, cancellationToken);

        // Allocations that are active now, or were vacated recently enough to still count on an
        // earlier day of the 7-day trend window. Split into two translation-safe queries
        // (individual vs group) and stitched together in memory.
        var individualAllocations = await _db.Allocations
            .Where(a => (a.VacatedAt == null || a.VacatedAt >= windowStartUtc) && a.HousingRequestId != null)
            .Select(a => new { a.AllocatedAt, a.VacatedAt, StudentId = a.HousingRequest!.StudentId })
            .ToListAsync(cancellationToken);

        var groupAllocations = await _db.Allocations
            .Where(a => (a.VacatedAt == null || a.VacatedAt >= windowStartUtc) && a.HousingGroupId != null)
            .Select(a => new { a.AllocatedAt, a.VacatedAt, MemberIds = a.HousingGroup!.Members.Select(m => m.StudentId).ToList() })
            .ToListAsync(cancellationToken);

        var allocations = new List<AllocationSnapshot>(individualAllocations.Count + groupAllocations.Count);
        allocations.AddRange(individualAllocations.Select(a => new AllocationSnapshot(a.AllocatedAt, a.VacatedAt, a.StudentId, [])));
        allocations.AddRange(groupAllocations.Select(a => new AllocationSnapshot(a.AllocatedAt, a.VacatedAt, null, a.MemberIds)));

        var activeNow = allocations.Where(a => a.VacatedAt is null).ToList();
        var occupiedBeds = activeNow.Sum(a => a.Seats);
        var totalHousedStudents = activeNow.SelectMany(a => a.StudentIds).Distinct().Count();

        var weeklyOccupancy = new List<DashboardOccupancyPointDto>(TrendDays);
        for (var i = 0; i < TrendDays; i++)
        {
            var day = windowStart.AddDays(i);
            var endOfDay = day.ToDateTime(TimeOnly.MaxValue);
            weeklyOccupancy.Add(new DashboardOccupancyPointDto
            {
                Date = day,
                OccupiedBeds = allocations.Where(a => a.IsActiveOn(endOfDay)).Sum(a => a.Seats)
            });
        }

        var recent = await _db.HousingRequests
            .Include(r => r.AdmissionDecision)
            .OrderByDescending(r => r.SubmittedAt)
            .Take(RecentRequestsCount)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                r.GovernorateId,
                r.AcademicLevel,
                r.Status,
                r.SubmittedAt,
                IsGroup = r.HousingGroupId != null,
                AdmissionStatus = r.AdmissionDecision != null ? r.AdmissionDecision.Status : AdmissionDecisionStatus.Pending
            })
            .ToListAsync(cancellationToken);

        var names = await _userLookup.LookupUsersAsync(
            recent.Select(r => r.StudentId).Distinct().ToList(), cancellationToken);

        var recentRequests = recent.Select(r => new DashboardRequestDto
        {
            Id = r.Id,
            StudentId = r.StudentId,
            StudentName = names.TryGetValue(r.StudentId, out var info) ? info.FullName : null,
            GovernorateId = r.GovernorateId,
            AcademicLevel = r.AcademicLevel,
            IsGroup = r.IsGroup,
            Status = r.Status,
            AdmissionStatus = r.AdmissionStatus,
            SubmittedAt = r.SubmittedAt
        }).ToList();

        return new HousingDashboardDto
        {
            PendingRequests = pendingRequests,
            OccupancyRate = totalBeds == 0 ? 0 : Math.Round((double)occupiedBeds / totalBeds * 100, 1),
            OccupiedBeds = occupiedBeds,
            TotalBeds = totalBeds,
            TotalHousedStudents = totalHousedStudents,
            Rooms = rooms,
            RecentRequests = recentRequests,
            WeeklyOccupancy = weeklyOccupancy
        };
    }
}
