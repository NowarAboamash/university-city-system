using AdvertisingService.Data;
using AdvertisingService.DTOs;
using AdvertisingService.Enums;
using AdvertisingService.Interfaces;
using AdvertisingService.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvertisingService.Services;

public class AdvertisementService : IAdvertisementService
{
    private readonly AdvertisingDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AdvertisementService(AdvertisingDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<AdvertisementDto> CreateAsync(CreateAdvertisementDto dto, Guid createdBy)
    {
        ValidateAdvertisement(dto.Title, dto.ImageUrl, dto.Type, dto.StartDate, dto.EndDate);

        var advertisement = new Advertisement
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            ImageUrl = dto.ImageUrl?.Trim(),
            Type = dto.Type,
            TargetGender = dto.TargetGender,
            IsActive = true,
            Priority = dto.Priority,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedBy = createdBy,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        AddTargeting(advertisement, dto.CollegeIds, dto.GovernorateIds);

        _dbContext.Advertisements.Add(advertisement);
        await _dbContext.SaveChangesAsync();

        return MapToDto(advertisement);
    }

    public async Task<IReadOnlyList<AdvertisementDto>> GetAllAsync()
    {
        var ads = await _dbContext.Advertisements
            .AsNoTracking()
            .OrderByDescending(ad => ad.Priority)
            .ThenBy(ad => ad.StartDate)
            .ToListAsync();

        return ads.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(TargetGender targetGender, int? collegeId, int? governorateId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var query = _dbContext.Advertisements
            .AsNoTracking()
            .Where(ad => ad.IsActive && ad.StartDate <= now && ad.EndDate >= now)
            .Where(ad => ad.TargetGender == TargetGender.Both || ad.TargetGender == targetGender)
            .Where(ad => !ad.AdvertisementColleges.Any() || (collegeId.HasValue && ad.AdvertisementColleges.Any(c => c.CollegeId == collegeId)))
            .Where(ad => !ad.AdvertisementGovernorates.Any() || (governorateId.HasValue && ad.AdvertisementGovernorates.Any(g => g.GovernorateId == governorateId)))
            .OrderByDescending(ad => ad.Priority)
            .ThenBy(ad => ad.StartDate);

        var ads = await query.ToListAsync();
        return ads.Select(MapToDto).ToList();
    }

    public async Task<AdvertisementDto?> GetByIdAsync(Guid id)
    {
        var ad = await _dbContext.Advertisements
            .AsNoTracking()
            .FirstOrDefaultAsync(ad => ad.Id == id);

        return ad is null ? null : MapToDto(ad);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAdvertisementDto dto)
    {
        ValidateAdvertisement(dto.Title, dto.ImageUrl, dto.Type, dto.StartDate, dto.EndDate);

        var ad = await _dbContext.Advertisements
            .Include(a => a.AdvertisementColleges)
            .Include(a => a.AdvertisementGovernorates)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (ad is null)
        {
            return false;
        }

        ad.Title = dto.Title.Trim();
        ad.Description = dto.Description?.Trim();
        ad.ImageUrl = dto.ImageUrl?.Trim();
        ad.Type = dto.Type;
        ad.TargetGender = dto.TargetGender;
        ad.Priority = dto.Priority;
        ad.StartDate = dto.StartDate;
        ad.EndDate = dto.EndDate;

        ad.AdvertisementColleges.Clear();
        ad.AdvertisementGovernorates.Clear();
        AddTargeting(ad, dto.CollegeIds, dto.GovernorateIds);

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var ad = await _dbContext.Advertisements.FindAsync(id);
        if (ad is null)
        {
            return false;
        }

        _dbContext.Advertisements.Remove(ad);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static void ValidateAdvertisement(string title, string? imageUrl, AdType type, DateTime startDate, DateTime endDate)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.");
        }

        if (startDate > endDate)
        {
            throw new ArgumentException("StartDate must be less than or equal to EndDate.");
        }

        if (type == AdType.Banner && string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("ImageUrl is required for banner advertisements.");
        }
    }

    private static void AddTargeting(Advertisement advertisement, IEnumerable<int> collegeIds, IEnumerable<int> governorateIds)
    {
        foreach (var collegeId in collegeIds.Distinct())
        {
            advertisement.AdvertisementColleges.Add(new AdvertisementCollege
            {
                AdvertisementId = advertisement.Id,
                CollegeId = collegeId
            });
        }

        foreach (var governorateId in governorateIds.Distinct())
        {
            advertisement.AdvertisementGovernorates.Add(new AdvertisementGovernorate
            {
                AdvertisementId = advertisement.Id,
                GovernorateId = governorateId
            });
        }
    }

    private static AdvertisementDto MapToDto(Advertisement advertisement)
    {
        return new AdvertisementDto
        {
            Id = advertisement.Id,
            Title = advertisement.Title,
            Description = advertisement.Description,
            ImageUrl = advertisement.ImageUrl,
            Type = advertisement.Type,
            TargetGender = advertisement.TargetGender,
            Priority = advertisement.Priority,
            StartDate = advertisement.StartDate,
            EndDate = advertisement.EndDate
        };
    }
}
