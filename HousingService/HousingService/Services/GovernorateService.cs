using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

public class GovernorateService : IGovernorateService
{
    private readonly IGovernorateRepository _governorateRepository;
    private readonly TimeProvider _timeProvider;

    public GovernorateService(IGovernorateRepository governorateRepository, TimeProvider timeProvider)
    {
        _governorateRepository = governorateRepository;
        _timeProvider = timeProvider;
    }

    public async Task<GovernorateDto> CreateAsync(CreateGovernorateDto dto)
    {
        await ValidateAsync(dto.Name);

        var governorate = new Governorate
        {
            Name = dto.Name.Trim(),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _governorateRepository.AddAsync(governorate);
        await _governorateRepository.SaveChangesAsync();

        return MapToDto(governorate);
    }

    public async Task<IReadOnlyList<GovernorateDto>> GetAllAsync()
    {
        var governorates = await _governorateRepository.GetAllAsync();
        return governorates.Select(MapToDto).ToList();
    }

    public async Task<GovernorateDto?> GetByIdAsync(int id)
    {
        var governorate = await _governorateRepository.GetByIdAsync(id);
        return governorate is null ? null : MapToDto(governorate);
    }

    public async Task<bool> UpdateAsync(int id, CreateGovernorateDto dto)
    {
        var governorate = await _governorateRepository.GetByIdAsync(id);
        if (governorate is null)
        {
            return false;
        }

        await ValidateAsync(dto.Name, id);

        governorate.Name = dto.Name.Trim();
        governorate.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        _governorateRepository.Update(governorate);
        await _governorateRepository.SaveChangesAsync();
        return true;
    }

    private async Task ValidateAsync(string name, int? excludingId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.");
        }

        var existing = await _governorateRepository.GetByNameAsync(name.Trim());
        if (existing is not null && existing.Id != excludingId)
        {
            throw new ArgumentException($"A governorate named '{name}' already exists.");
        }
    }

    private static GovernorateDto MapToDto(Governorate governorate)
    {
        return new GovernorateDto
        {
            Id = governorate.Id,
            Name = governorate.Name,
            CreatedAt = governorate.CreatedAt,
            UpdatedAt = governorate.UpdatedAt
        };
    }
}
