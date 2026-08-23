using HousingService.Data.Repositories;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;
using SharedKernel.Notifications;

namespace HousingService.Services;

public class BuildingEvacuationService : IBuildingEvacuationService
{
    private readonly IBuildingRepository _buildingRepository;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;

    public BuildingEvacuationService(
        IBuildingRepository buildingRepository,
        IAllocationRepository allocationRepository,
        IRoomRepository roomRepository,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        _buildingRepository = buildingRepository;
        _allocationRepository = allocationRepository;
        _roomRepository = roomRepository;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<int?> AnnounceAsync(int buildingId, AnnounceEvacuationDto dto)
    {
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building is null)
        {
            return null;
        }

        var allocations = await _allocationRepository.GetActiveByBuildingIdAsync(buildingId);
        var residentIds = GetResidentStudentIds(allocations);

        if (residentIds.Count == 0)
        {
            return 0;
        }

        var message = string.IsNullOrWhiteSpace(dto.Message)
            ? $"سيتم إخلاء مبنى {building.Name} قريباً. يرجى الاستعداد لمغادرة السكن."
            : dto.Message.Trim();

        await _notificationPublisher.NotifyUsersAsync(residentIds, "إشعار إخلاء سكن", message);

        return residentIds.Count;
    }

    public async Task<EvacuationResultDto?> ExecuteAsync(int buildingId, ExecuteEvacuationDto dto)
    {
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building is null)
        {
            return null;
        }

        var allocations = (await _allocationRepository.GetActiveByBuildingIdAsync(buildingId)).ToList();
        var residentIds = GetResidentStudentIds(allocations);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var allocation in allocations)
        {
            allocation.VacatedAt = now;
            allocation.UpdatedAt = now;
            _allocationRepository.Update(allocation);
        }

        var rooms = await _roomRepository.GetByBuildingIdAsync(buildingId);
        foreach (var room in rooms.Where(r => r.Status is RoomStatus.Occupied or RoomStatus.Full))
        {
            room.Status = RoomStatus.Available;
            room.UpdatedAt = now;
            _roomRepository.Update(room);
        }

        await _allocationRepository.SaveChangesAsync();

        if (residentIds.Count > 0)
        {
            var message = string.IsNullOrWhiteSpace(dto.Message)
                ? $"تم إخلاء مبنى {building.Name}. نشكركم على التزامكم."
                : dto.Message.Trim();

            await _notificationPublisher.NotifyUsersAsync(residentIds, "تم إخلاء السكن", message);
        }

        return new EvacuationResultDto
        {
            VacatedAllocationsCount = allocations.Count,
            NotifiedStudentIds = residentIds
        };
    }

    private static List<string> GetResidentStudentIds(IEnumerable<Domain.Entities.Allocation> allocations)
    {
        var ids = new List<string>();

        foreach (var allocation in allocations)
        {
            if (allocation.HousingRequest is not null)
            {
                ids.Add(allocation.HousingRequest.StudentId);
            }
            else if (allocation.HousingGroup is not null)
            {
                ids.AddRange(allocation.HousingGroup.Members.Select(m => m.StudentId));
            }
        }

        return ids.Distinct().ToList();
    }
}
