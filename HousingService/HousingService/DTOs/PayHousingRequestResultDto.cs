namespace HousingService.DTOs;

public enum PaymentOutcome
{
    Success,
    RequestNotFound,
    NotOwned,
    NotAccepted,
    AlreadyPaid,
    FeeNotConfigured,
    InsufficientBalance,
    GatewayError
}

/// <summary>Result of attempting to pay a housing request's fee from the student's wallet.</summary>
public class PayHousingRequestResultDto
{
    public PaymentOutcome Outcome { get; set; }

    /// <summary>The housing fee owed on this request (what was / would be charged). Null only for
    /// outcomes decided before the fee is resolved (RequestNotFound, NotOwned, NotAccepted).
    /// For AlreadyPaid it's the amount previously paid.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Wallet balance after the charge — only set when <see cref="Outcome"/> is Success.</summary>
    public decimal? NewBalance { get; set; }

    public string Message { get; set; } = string.Empty;
}
