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

    /// <summary>Wallet balance after the charge — only set when <see cref="Outcome"/> is Success.</summary>
    public decimal? NewBalance { get; set; }

    public string Message { get; set; } = string.Empty;
}
