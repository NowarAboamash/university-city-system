using HousingService.External;

namespace HousingService.Tests.Fakes;

public sealed record WalletChargeCall(string UserId, decimal Amount, string Reference, string Description);

/// <summary>
/// In-memory stand-in for AuthService's wallet-charge endpoint. Configure the outcome per test;
/// records every call so assertions can check the student, amount and reference.
/// </summary>
public sealed class FakeWalletClient : IWalletClient
{
    public List<WalletChargeCall> Calls { get; } = [];

    /// <summary>When set, ChargeAsync throws it (simulates AuthService unreachable / 5xx).</summary>
    public Exception? ThrowOnCharge { get; set; }

    public bool InsufficientBalance { get; set; }

    public decimal NextBalance { get; set; } = 100m;

    public Task<WalletChargeResult> ChargeAsync(string userId, decimal amount, string reference, string description, CancellationToken cancellationToken = default)
    {
        Calls.Add(new WalletChargeCall(userId, amount, reference, description));

        if (ThrowOnCharge is not null)
        {
            throw ThrowOnCharge;
        }

        if (InsufficientBalance)
        {
            return Task.FromResult(new WalletChargeResult { Success = false, InsufficientBalance = true });
        }

        return Task.FromResult(new WalletChargeResult { Success = true, NewBalance = NextBalance });
    }
}
