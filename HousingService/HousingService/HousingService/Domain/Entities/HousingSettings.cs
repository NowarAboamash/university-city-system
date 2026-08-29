namespace HousingService.Domain.Entities;

/// <summary>
/// Single-row, admin-editable configuration for the housing payment workflow.
/// Always exactly one row (Id = 1), seeded via migration — read it with FirstAsync().
/// </summary>
public class HousingSettings
{
    public int Id { get; set; }

    /// <summary>How many days a student has to pay the housing fee, counted from the moment
    /// their request is first Accepted. Captured onto each request at acceptance time, so
    /// later edits to this value never shorten/extend an already-accepted request's deadline.</summary>
    public int PaymentDeadlineDays { get; set; } = 15;

    /// <summary>How many days before the deadline the automatic reminder is sent.
    /// Must be greater than 0 and strictly less than <see cref="PaymentDeadlineDays"/>.</summary>
    public int ReminderDaysBefore { get; set; } = 3;

    /// <summary>The housing fee charged from the student's wallet on payment. A single flat
    /// amount for every request (all rooms are the same tier). Must be set (&gt; 0) before
    /// payments can go through.</summary>
    public decimal HousingFeeAmount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
