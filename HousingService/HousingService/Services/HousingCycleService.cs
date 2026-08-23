using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

public class HousingCycleService : IHousingCycleService
{
    private readonly IHousingCycleRepository _cycleRepository;
    private readonly TimeProvider _timeProvider;

    public HousingCycleService(IHousingCycleRepository cycleRepository, TimeProvider timeProvider)
    {
        _cycleRepository = cycleRepository;
        _timeProvider = timeProvider;
    }

    public async Task<HousingCycleDto> CreateAsync(CreateHousingCycleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException("Name is required.");
        }

        var existing = await _cycleRepository.GetByNameAsync(dto.Name.Trim());
        if (existing is not null)
        {
            throw new ArgumentException($"A housing cycle named '{dto.Name}' already exists.");
        }

        var cycle = new HousingCycle
        {
            Name = dto.Name.Trim(),
            Status = HousingCycleStatus.Closed,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _cycleRepository.AddAsync(cycle);
        await _cycleRepository.SaveChangesAsync();

        return MapToDto(cycle);
    }

    public async Task<IReadOnlyList<HousingCycleDto>> GetAllAsync()
    {
        var cycles = await _cycleRepository.GetAllAsync();
        return cycles.Select(MapToDto).ToList();
    }

    public async Task<HousingCycleDto?> GetByIdAsync(int id)
    {
        var cycle = await _cycleRepository.GetByIdAsync(id);
        return cycle is null ? null : MapToDto(cycle);
    }

    public async Task<HousingCycleDto?> GetCurrentOpenAsync()
    {
        var cycle = await _cycleRepository.GetOpenAsync();
        return cycle is null ? null : MapToDto(cycle);
    }

    public async Task<bool> OpenAsync(int id)
    {
        var cycle = await _cycleRepository.GetByIdAsync(id);
        if (cycle is null)
        {
            return false;
        }

        var currentlyOpen = await _cycleRepository.GetOpenAsync();
        if (currentlyOpen is not null && currentlyOpen.Id != id)
        {
            throw new ArgumentException($"Housing cycle '{currentlyOpen.Name}' is already open. Close it before opening another.");
        }

        cycle.Status = HousingCycleStatus.Open;
        cycle.OpenedAt = _timeProvider.GetUtcNow().UtcDateTime;
        cycle.UpdatedAt = cycle.OpenedAt;

        _cycleRepository.Update(cycle);
        await _cycleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CloseAsync(int id)
    {
        var cycle = await _cycleRepository.GetByIdAsync(id);
        if (cycle is null)
        {
            return false;
        }

        cycle.Status = HousingCycleStatus.Closed;
        cycle.ClosedAt = _timeProvider.GetUtcNow().UtcDateTime;
        cycle.UpdatedAt = cycle.ClosedAt;

        _cycleRepository.Update(cycle);
        await _cycleRepository.SaveChangesAsync();
        return true;
    }

    private static HousingCycleDto MapToDto(HousingCycle cycle)
    {
        return new HousingCycleDto
        {
            Id = cycle.Id,
            Name = cycle.Name,
            Status = cycle.Status,
            OpenedAt = cycle.OpenedAt,
            ClosedAt = cycle.ClosedAt,
            CreatedAt = cycle.CreatedAt,
            UpdatedAt = cycle.UpdatedAt
        };
    }
}
