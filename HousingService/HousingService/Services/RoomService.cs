using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IBuildingRepository _buildingRepository;
    private readonly TimeProvider _timeProvider;

    public RoomService(IRoomRepository roomRepository, IBuildingRepository buildingRepository, TimeProvider timeProvider)
    {
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
        _timeProvider = timeProvider;
    }

    public async Task<RoomDto?> CreateAsync(int buildingId, CreateRoomDto dto)
    {
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.RoomNumber))
        {
            throw new ArgumentException("RoomNumber is required.");
        }

        var existing = await _roomRepository.GetByBuildingAndRoomNumberAsync(buildingId, dto.RoomNumber.Trim());
        if (existing is not null)
        {
            throw new ArgumentException($"Room '{dto.RoomNumber}' already exists in this building.");
        }

        var room = new Room
        {
            BuildingId = buildingId,
            RoomNumber = dto.RoomNumber.Trim(),
            Floor = dto.Floor,
            CurrentOccupancy = 0,
            Status = RoomStatus.Available,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _roomRepository.AddAsync(room);
        await _roomRepository.SaveChangesAsync();

        return MapToDto(room, building.StandardRoomCapacity, occupantStudentIds: []);
    }

    public async Task<IReadOnlyList<RoomDto>?> GetByBuildingAsync(int buildingId)
    {
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building is null)
        {
            return null;
        }

        var rooms = await _roomRepository.GetByBuildingIdWithOccupantsAsync(buildingId);
        return rooms.Select(r => MapToDto(r, building.StandardRoomCapacity, GetOccupantStudentIds(r))).ToList();
    }

    public async Task<IReadOnlyList<RoomLookupDto>?> GetLookupByBuildingAsync(int buildingId)
    {
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building is null)
        {
            return null;
        }

        var rooms = await _roomRepository.GetByBuildingIdAsync(buildingId);
        return rooms
            .OrderBy(r => r.Floor)
            .ThenBy(r => r.RoomNumber)
            .Select(r => new RoomLookupDto { Id = r.Id, Floor = r.Floor, RoomNumber = r.RoomNumber })
            .ToList();
    }

    public async Task<RoomDto?> GetByIdAsync(int buildingId, int id)
    {
        var room = await _roomRepository.GetByIdWithOccupantsAsync(id);
        if (room is null || room.BuildingId != buildingId)
        {
            return null;
        }

        return MapToDto(room, room.Building.StandardRoomCapacity, GetOccupantStudentIds(room));
    }

    public async Task<bool?> UpdateAsync(int buildingId, int id, UpdateRoomDto dto)
    {
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building is null)
        {
            return null;
        }

        var room = await _roomRepository.GetByIdWithOccupantsAsync(id);
        if (room is null || room.BuildingId != buildingId)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(dto.RoomNumber))
        {
            throw new ArgumentException("RoomNumber is required.");
        }

        var existing = await _roomRepository.GetByBuildingAndRoomNumberAsync(buildingId, dto.RoomNumber.Trim());
        if (existing is not null && existing.Id != id)
        {
            throw new ArgumentException($"Room '{dto.RoomNumber}' already exists in this building.");
        }

        // A manual status edit can't be allowed to silently contradict who's actually living
        // there — that would desync this field from the Allocation-derived occupancy everywhere
        // else in the system reads it from. Occupants must be moved/vacated first (via
        // allocations/transfer or /vacate), not erased by an admin flipping this dropdown.
        var currentOccupancy = GetOccupantStudentIds(room).Count;
        var requestedStatusMeansOccupied = dto.Status is RoomStatus.Occupied or RoomStatus.Full;

        if (currentOccupancy > 0 && !requestedStatusMeansOccupied)
        {
            throw new ArgumentException($"This room currently has {currentOccupancy} active occupant(s); it must be vacated or transferred out before its status can be changed to {dto.Status}.");
        }

        if (currentOccupancy == 0 && requestedStatusMeansOccupied)
        {
            throw new ArgumentException("This room has no active occupants; it cannot be marked Occupied or Full.");
        }

        room.RoomNumber = dto.RoomNumber.Trim();
        room.Floor = dto.Floor;
        room.Status = dto.Status;
        room.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        _roomRepository.Update(room);
        await _roomRepository.SaveChangesAsync();
        return true;
    }

    private static List<string> GetOccupantStudentIds(Room room)
    {
        var occupantIds = new List<string>();

        foreach (var allocation in room.Allocations.Where(a => a.VacatedAt is null))
        {
            if (allocation.HousingRequest is not null)
            {
                occupantIds.Add(allocation.HousingRequest.StudentId);
            }

            if (allocation.HousingGroup is not null)
            {
                occupantIds.AddRange(allocation.HousingGroup.Members.Select(m => m.StudentId));
            }
        }

        return occupantIds.Distinct().ToList();
    }

    private static RoomDto MapToDto(Room room, int capacity, List<string> occupantStudentIds)
    {
        return new RoomDto
        {
            Id = room.Id,
            BuildingId = room.BuildingId,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Capacity = capacity,
            CurrentOccupancy = occupantStudentIds.Count,
            Status = room.Status,
            OccupantStudentIds = occupantStudentIds,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}
