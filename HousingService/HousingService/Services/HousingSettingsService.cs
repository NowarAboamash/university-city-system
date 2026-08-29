using HousingService.Data.Repositories;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

public class HousingSettingsService : IHousingSettingsService
{
    private readonly IHousingSettingsRepository _settingsRepository;
    private readonly TimeProvider _timeProvider;

    public HousingSettingsService(IHousingSettingsRepository settingsRepository, TimeProvider timeProvider)
    {
        _settingsRepository = settingsRepository;
        _timeProvider = timeProvider;
    }

    public async Task<HousingSettingsDto> GetAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        return MapToDto(settings);
    }

    public async Task<HousingSettingsDto> UpdateAsync(UpdateHousingSettingsDto dto)
    {
        if (dto.PaymentDeadlineDays <= 0)
        {
            throw new ArgumentException("PaymentDeadlineDays must be greater than 0.");
        }

        if (dto.ReminderDaysBefore <= 0)
        {
            throw new ArgumentException("ReminderDaysBefore must be greater than 0.");
        }

        if (dto.ReminderDaysBefore >= dto.PaymentDeadlineDays)
        {
            throw new ArgumentException("ReminderDaysBefore must be less than PaymentDeadlineDays.");
        }

        if (dto.HousingFeeAmount < 0)
        {
            throw new ArgumentException("HousingFeeAmount cannot be negative.");
        }

        var settings = await _settingsRepository.GetAsync();
        settings.PaymentDeadlineDays = dto.PaymentDeadlineDays;
        settings.ReminderDaysBefore = dto.ReminderDaysBefore;
        settings.HousingFeeAmount = dto.HousingFeeAmount;
        settings.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        _settingsRepository.Update(settings);
        await _settingsRepository.SaveChangesAsync();

        return MapToDto(settings);
    }

    private static HousingSettingsDto MapToDto(Domain.Entities.HousingSettings settings) => new()
    {
        PaymentDeadlineDays = settings.PaymentDeadlineDays,
        ReminderDaysBefore = settings.ReminderDaysBefore,
        HousingFeeAmount = settings.HousingFeeAmount,
        UpdatedAt = settings.UpdatedAt
    };
}
