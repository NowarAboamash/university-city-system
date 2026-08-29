using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IHousingSettingsService
{
    Task<HousingSettingsDto> GetAsync();

    /// <exception cref="ArgumentException">If the values are out of range
    /// (days &gt; 0, ReminderDaysBefore &lt; PaymentDeadlineDays, fee &gt;= 0).</exception>
    Task<HousingSettingsDto> UpdateAsync(UpdateHousingSettingsDto dto);
}
