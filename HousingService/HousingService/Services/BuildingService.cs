using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

public class BuildingService : IBuildingService
{
    private readonly IBuildingRepository _buildingRepository;
    private readonly TimeProvider _timeProvider;

    public BuildingService(IBuildingRepository buildingRepository, TimeProvider timeProvider)
    {
        _buildingRepository = buildingRepository;
        _timeProvider = timeProvider;
    }

    public async Task<BuildingDto> CreateAsync(CreateBuildingDto dto)
    {
        await ValidateAsync(dto.Name, dto.StandardRoomCapacity);

        var building = new Building
        {
            Name = dto.Name.Trim(),
            Gender = dto.Gender,
            Status = BuildingStatus.Active,
            FloorsCount = dto.FloorsCount,
            StandardRoomCapacity = dto.StandardRoomCapacity,
            Description = dto.Description?.Trim(),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _buildingRepository.AddAsync(building);
        await _buildingRepository.SaveChangesAsync();

        return MapToDto(building);
    }

    public async Task<IReadOnlyList<BuildingDto>> GetAllAsync()
    {
        var buildings = await _buildingRepository.GetAllAsync();
        return buildings.Select(MapToDto).ToList();
    }

    public async Task<BuildingDto?> GetByIdAsync(int id)
    {
        var building = await _buildingRepository.GetByIdAsync(id);
        return building is null ? null : MapToDto(building);
    }

    public async Task<bool> UpdateAsync(int id, UpdateBuildingDto dto)
    {
        var building = await _buildingRepository.GetByIdAsync(id);
        if (building is null)
        {
            return false;
        }

        await ValidateAsync(dto.Name, dto.StandardRoomCapacity, id);

        building.Name = dto.Name.Trim();
        building.Gender = dto.Gender;
        building.Status = dto.Status;
        building.FloorsCount = dto.FloorsCount;
        building.StandardRoomCapacity = dto.StandardRoomCapacity;
        building.Description = dto.Description?.Trim();
        building.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        _buildingRepository.Update(building);
        await _buildingRepository.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<BuildingLookupDto>> GetLookupAsync()
    {
        var buildings = await _buildingRepository.GetAllAsync();
        return buildings
            .Select(b => new BuildingLookupDto { Id = b.Id, Name = b.Name })
            .ToList();
    }

    private async Task ValidateAsync(string name, int standardRoomCapacity, int? excludingId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.");
        }

        if (standardRoomCapacity <= 0)
        {
            throw new ArgumentException("StandardRoomCapacity must be greater than zero.");
        }

        var existing = await _buildingRepository.GetByNameAsync(name.Trim());
        if (existing is not null && existing.Id != excludingId)
        {
            throw new ArgumentException($"A building named '{name}' already exists.");
        }
    }

    private static BuildingDto MapToDto(Building building)
    {
        return new BuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            Gender = building.Gender,
            Status = building.Status,
            FloorsCount = building.FloorsCount,
            StandardRoomCapacity = building.StandardRoomCapacity,
            Description = building.Description,
            RoomsCount = building.Rooms.Count,
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };
    }
}
