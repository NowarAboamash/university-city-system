namespace HousingService.DTOs;

/// <summary>Admin-editable housing payment configuration (single global row).</summary>
public class HousingSettingsDto
{
    public int PaymentDeadlineDays { get; set; }

    public int ReminderDaysBefore { get; set; }

    public decimal HousingFeeAmount { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Payload for <c>PUT /api/housing-requests/settings</c>.</summary>
public class UpdateHousingSettingsDto
{
    public int PaymentDeadlineDays { get; set; }

    public int ReminderDaysBefore { get; set; }

    public decimal HousingFeeAmount { get; set; }
}
