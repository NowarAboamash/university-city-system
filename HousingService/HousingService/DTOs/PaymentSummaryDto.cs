namespace HousingService.DTOs;

/// <summary>
/// Financial roll-up for the admin payments dashboard, computed straight from the housing
/// requests table. The <c>*InRange</c> fields exist for reconciliation against AuthService's
/// wallet ledger over a period; everything else is a "right now" snapshot.
/// </summary>
public class PaymentSummaryDto
{
    /// <summary>Current configured fee (for display); not necessarily what past requests were charged.</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>Sum of the frozen fee across every Accepted request (paid + unpaid).</summary>
    public decimal TotalRequired { get; set; }

    /// <summary>Sum actually collected across all time (uses the real per-request AmountPaid).</summary>
    public decimal TotalPaid { get; set; }

    /// <summary><c>TotalRequired - TotalPaid</c>.</summary>
    public decimal TotalOutstanding { get; set; }

    public int CountAccepted { get; set; }

    public int CountPaid { get; set; }

    /// <summary>Accepted requests not yet paid = <c>CountAccepted - CountPaid</c>.</summary>
    public int CountUnpaid { get; set; }

    /// <summary>Collected within [paidFrom, paidTo] if given; otherwise equal to <see cref="TotalPaid"/>.</summary>
    public decimal PaidInRange { get; set; }

    /// <summary>Payment count within [paidFrom, paidTo] if given; otherwise equal to <see cref="CountPaid"/>.</summary>
    public int CountPaidInRange { get; set; }

    public DateTime AsOf { get; set; }
}
