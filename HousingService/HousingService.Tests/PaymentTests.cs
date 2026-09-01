using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class PaymentTests
{
    private static UpdateHousingSettingsDto Settings(int deadline = 15, int reminderBefore = 3, decimal fee = 0m) =>
        new() { PaymentDeadlineDays = deadline, ReminderDaysBefore = reminderBefore, HousingFeeAmount = fee };

    // --- HousingSettings ------------------------------------------------------

    [Fact]
    public async Task GetSettings_ReturnsSeededDefaults()
    {
        using var ctx = new TestContext();

        var settings = await ctx.SettingsService.GetAsync();

        Assert.Equal(15, settings.PaymentDeadlineDays);
        Assert.Equal(3, settings.ReminderDaysBefore);
        Assert.Equal(0m, settings.HousingFeeAmount);
    }

    [Fact]
    public async Task UpdateSettings_ValidValues_Persist()
    {
        using var ctx = new TestContext();

        var updated = await ctx.SettingsService.UpdateAsync(Settings(deadline: 20, reminderBefore: 5, fee: 25m));

        Assert.Equal(20, updated.PaymentDeadlineDays);
        Assert.Equal(5, updated.ReminderDaysBefore);
        Assert.Equal(25m, updated.HousingFeeAmount);
        Assert.NotNull(updated.UpdatedAt);

        var reloaded = await ctx.SettingsService.GetAsync();
        Assert.Equal(20, reloaded.PaymentDeadlineDays);
        Assert.Equal(25m, reloaded.HousingFeeAmount);
    }

    [Theory]
    [InlineData(0, 3, 0)]     // deadline must be > 0
    [InlineData(15, 0, 0)]    // reminder must be > 0
    [InlineData(10, 10, 0)]   // reminder must be < deadline
    [InlineData(10, 12, 0)]   // reminder must be < deadline
    [InlineData(15, 3, -1)]   // fee must be >= 0
    public async Task UpdateSettings_InvalidValues_Throw(int deadline, int reminderBefore, int fee)
    {
        using var ctx = new TestContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.SettingsService.UpdateAsync(Settings(deadline, reminderBefore, fee)));
    }

    [Fact]
    public async Task UpdateSettings_FeeWithMoreThanTwoDecimals_Throws()
    {
        using var ctx = new TestContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ctx.SettingsService.UpdateAsync(new UpdateHousingSettingsDto
            {
                PaymentDeadlineDays = 15,
                ReminderDaysBefore = 3,
                HousingFeeAmount = 25.123m
            }));
    }

    // --- PaymentDueDate stamped on acceptance -------------------------------

    [Fact]
    public async Task MakeDecision_FirstAcceptance_StampsPaymentDueDateFromSettings()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(deadline: 20, reminderBefore: 3, fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);

        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Accepted }, "admin-1");

        var stored = ctx.Db.HousingRequests.Single(r => r.Id == request.Id);
        Assert.Equal(ctx.Clock.GetUtcNow().UtcDateTime.AddDays(20), stored.PaymentDueDate);
        Assert.False(stored.ReminderSent);
        Assert.False(stored.IsPaid);
    }

    [Fact]
    public async Task MakeDecision_Rejected_DoesNotStampPaymentDueDate()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);

        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Rejected }, "admin-1");

        Assert.Null(ctx.Db.HousingRequests.Single(r => r.Id == request.Id).PaymentDueDate);
    }

    [Fact]
    public async Task MakeDecision_ReAcceptedAfterReversal_KeepsOriginalDueDate()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(deadline: 15, reminderBefore: 3, fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);

        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Accepted }, "admin-1");
        var originalDueDate = ctx.Db.HousingRequests.Single(r => r.Id == request.Id).PaymentDueDate;

        ctx.Clock.Advance(TimeSpan.FromDays(5));
        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.WaitingList }, "admin-1");
        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Accepted }, "admin-1");

        Assert.Equal(originalDueDate, ctx.Db.HousingRequests.Single(r => r.Id == request.Id).PaymentDueDate);
    }

    // --- PaymentReminderService -------------------------------------------

    [Fact]
    public async Task Reminder_AcceptedUnpaidWithinWindow_NotifiesOnceAndFlags()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var due = ctx.Clock.GetUtcNow().UtcDateTime.AddDays(2); // within the default 3-day window
        ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: due);

        var notified = await ctx.PaymentReminderService.RunAsync();

        Assert.Equal(1, notified);
        var sent = Assert.Single(ctx.Notifications.Sent);
        Assert.Equal("student-1", sent.StudentId);
        Assert.Contains("خلال 2 يوم", sent.Body);
        Assert.Contains("housing_payment_reminder", sent.Data);
        Assert.True(ctx.Db.HousingRequests.Single(r => r.Id == 1000).ReminderSent);

        // Second run is a no-op — ReminderSent guards it.
        ctx.Notifications.Sent.Clear();
        var again = await ctx.PaymentReminderService.RunAsync();
        Assert.Equal(0, again);
        Assert.Empty(ctx.Notifications.Sent);
    }

    [Fact]
    public async Task Reminder_OverdueRequest_StillNotifiesWithPastDueWording()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var due = ctx.Clock.GetUtcNow().UtcDateTime.AddDays(-1);
        ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: due);

        var notified = await ctx.PaymentReminderService.RunAsync();

        Assert.Equal(1, notified);
        Assert.Contains("انتهت مهلة", Assert.Single(ctx.Notifications.Sent).Body);
    }

    [Fact]
    public async Task Reminder_Skips_Paid_NotAccepted_FarFuture_AndAlreadyReminded()
    {
        using var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var soon = ctx.Clock.GetUtcNow().UtcDateTime.AddDays(1);
        var farFuture = ctx.Clock.GetUtcNow().UtcDateTime.AddDays(30);

        ctx.AddRequest(1000, "paid", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: soon, isPaid: true);
        ctx.AddRequest(1001, "not-accepted", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.WaitingList, paymentDueDate: soon);
        ctx.AddRequest(1002, "no-decision", cycle.Id, gov.Id, Gender.Female,
            paymentDueDate: soon);
        ctx.AddRequest(1003, "far-future", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: farFuture);
        ctx.AddRequest(1004, "already-reminded", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, paymentDueDate: soon, reminderSent: true);

        var notified = await ctx.PaymentReminderService.RunAsync();

        Assert.Equal(0, notified);
        Assert.Empty(ctx.Notifications.Sent);
    }

    // --- PayAsync (wallet charge) -----------------------------------------

    private static (TestContext ctx, int requestId) AcceptedUnpaidRequest(decimal fee)
    {
        var ctx = new TestContext();
        ctx.SettingsService.UpdateAsync(Settings(fee: fee)).GetAwaiter().GetResult();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted);
        return (ctx, request.Id);
    }

    [Fact]
    public async Task Pay_Success_ChargesWalletMarksPaidAndNotifies()
    {
        var (ctx, requestId) = AcceptedUnpaidRequest(fee: 25m);
        using var _ = ctx;
        ctx.WalletClient.NextBalance = 75m;

        var result = await ctx.RequestService.PayAsync("student-1", requestId);

        Assert.Equal(PaymentOutcome.Success, result.Outcome);
        Assert.Equal(75m, result.NewBalance);
        Assert.Equal(25m, result.Amount);

        var call = Assert.Single(ctx.WalletClient.Calls);
        Assert.Equal("student-1", call.UserId);
        Assert.Equal(25m, call.Amount);
        Assert.Equal($"housing-request-{requestId}", call.Reference);

        var stored = ctx.Db.HousingRequests.Single(r => r.Id == requestId);
        Assert.True(stored.IsPaid);
        Assert.NotNull(stored.PaidAt);
        Assert.Contains(ctx.Notifications.Sent, n => n.StudentId == "student-1" && n.Data!.Contains("housing_payment_completed"));
    }

    [Fact]
    public async Task Pay_InsufficientBalance_DoesNotMarkPaid()
    {
        var (ctx, requestId) = AcceptedUnpaidRequest(fee: 25m);
        using var _ = ctx;
        ctx.WalletClient.InsufficientBalance = true;

        var result = await ctx.RequestService.PayAsync("student-1", requestId);

        Assert.Equal(PaymentOutcome.InsufficientBalance, result.Outcome);
        Assert.Equal(25m, result.Amount); // the UI needs "you needed X"
        Assert.False(ctx.Db.HousingRequests.Single(r => r.Id == requestId).IsPaid);
    }

    [Fact]
    public async Task Pay_WalletGatewayThrows_ReturnsGatewayErrorAndDoesNotMarkPaid()
    {
        var (ctx, requestId) = AcceptedUnpaidRequest(fee: 25m);
        using var _ = ctx;
        ctx.WalletClient.ThrowOnCharge = new InvalidOperationException("auth-service down");

        var result = await ctx.RequestService.PayAsync("student-1", requestId);

        Assert.Equal(PaymentOutcome.GatewayError, result.Outcome);
        Assert.False(ctx.Db.HousingRequests.Single(r => r.Id == requestId).IsPaid);
    }

    [Fact]
    public async Task Pay_FeeNotConfigured_RejectedWithoutCallingWallet()
    {
        var (ctx, requestId) = AcceptedUnpaidRequest(fee: 0m);
        using var _ = ctx;

        var result = await ctx.RequestService.PayAsync("student-1", requestId);

        Assert.Equal(PaymentOutcome.FeeNotConfigured, result.Outcome);
        Assert.Empty(ctx.WalletClient.Calls);
    }

    [Fact]
    public async Task Pay_NotAccepted_Rejected()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female);

        var result = await ctx.RequestService.PayAsync("student-1", request.Id);

        Assert.Equal(PaymentOutcome.NotAccepted, result.Outcome);
        Assert.Empty(ctx.WalletClient.Calls);
    }

    [Fact]
    public async Task Pay_AlreadyPaid_Rejected()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "student-1", cycle.Id, gov.Id, Gender.Female,
            decisionStatus: AdmissionDecisionStatus.Accepted, isPaid: true);

        var result = await ctx.RequestService.PayAsync("student-1", request.Id);

        Assert.Equal(PaymentOutcome.AlreadyPaid, result.Outcome);
        Assert.Empty(ctx.WalletClient.Calls);
    }

    [Fact]
    public async Task Pay_NotOwner_Rejected()
    {
        var (ctx, requestId) = AcceptedUnpaidRequest(fee: 25m);
        using var _ = ctx;

        var result = await ctx.RequestService.PayAsync("someone-else", requestId);

        Assert.Equal(PaymentOutcome.NotOwned, result.Outcome);
        Assert.Empty(ctx.WalletClient.Calls);
    }

    // --- Fee frozen at acceptance ---------------------------------------

    [Fact]
    public async Task MakeDecision_FirstAcceptance_FreezesFeeAmount()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Female);

        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Accepted }, "admin-1");

        Assert.Equal(25m, ctx.Db.HousingRequests.Single(r => r.Id == request.Id).FeeAmount);
    }

    [Fact]
    public async Task Pay_ChargesFeeFrozenAtAcceptance_NotCurrentSetting()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var request = ctx.AddRequest(1000, "s1", cycle.Id, gov.Id, Gender.Female);
        await ctx.RequestService.MakeDecisionAsync(request.Id, new MakeAdmissionDecisionDto { Status = AdmissionDecisionStatus.Accepted }, "admin-1");

        // Admin raises the fee AFTER this student was accepted.
        await ctx.SettingsService.UpdateAsync(Settings(fee: 40m));

        var result = await ctx.RequestService.PayAsync("s1", request.Id);

        Assert.Equal(PaymentOutcome.Success, result.Outcome);
        Assert.Equal(25m, result.Amount);
        Assert.Equal(25m, Assert.Single(ctx.WalletClient.Calls).Amount);
        Assert.Equal(25m, ctx.Db.HousingRequests.Single(r => r.Id == request.Id).AmountPaid);
    }

    // --- Payment summary ----------------------------------------------

    [Fact]
    public async Task PaymentSummary_AggregatesAcceptedRequests()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);

        // 3 accepted: 2 paid (25 each), 1 unpaid
        ctx.AddRequest(1000, "p1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m, isPaid: true, amountPaid: 25m);
        ctx.AddRequest(1001, "p2", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m, isPaid: true, amountPaid: 25m);
        ctx.AddRequest(1002, "u1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m);
        // not accepted -> excluded entirely
        ctx.AddRequest(1003, "n1", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.WaitingList);

        var s = await ctx.RequestService.GetPaymentSummaryAsync(null, null, null);

        Assert.Equal(3, s.CountAccepted);
        Assert.Equal(2, s.CountPaid);
        Assert.Equal(1, s.CountUnpaid);
        Assert.Equal(75m, s.TotalRequired);
        Assert.Equal(50m, s.TotalPaid);
        Assert.Equal(25m, s.TotalOutstanding);
        Assert.Equal(50m, s.PaidInRange);
        Assert.Equal(2, s.CountPaidInRange);
    }

    [Fact]
    public async Task PaymentSummary_PaidDateRange_ScopesOnlyInRangeFields()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        var day0 = ctx.Clock.GetUtcNow().UtcDateTime;

        ctx.AddRequest(1000, "early", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m, isPaid: true, amountPaid: 25m, paidAt: day0);
        ctx.AddRequest(1001, "late", cycle.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m, isPaid: true, amountPaid: 25m, paidAt: day0.AddDays(10));

        var s = await ctx.RequestService.GetPaymentSummaryAsync(null, day0.AddDays(5), day0.AddDays(15));

        Assert.Equal(2, s.CountPaid);            // all-time snapshot unchanged
        Assert.Equal(50m, s.TotalPaid);
        Assert.Equal(1, s.CountPaidInRange);     // only "late" falls in [day5, day15]
        Assert.Equal(25m, s.PaidInRange);
    }

    [Fact]
    public async Task PaymentSummary_CycleFilter()
    {
        using var ctx = new TestContext();
        await ctx.SettingsService.UpdateAsync(Settings(fee: 25m));
        var cycleA = ctx.AddOpenCycle(1000);
        var cycleB = ctx.AddOpenCycle(1001);
        var gov = ctx.AddGovernorate(1000);
        ctx.AddRequest(1000, "a1", cycleA.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m);
        ctx.AddRequest(1001, "b1", cycleB.Id, gov.Id, Gender.Female, decisionStatus: AdmissionDecisionStatus.Accepted, feeAmount: 25m);

        var s = await ctx.RequestService.GetPaymentSummaryAsync(cycleA.Id, null, null);

        Assert.Equal(1, s.CountAccepted);
        Assert.Equal(25m, s.TotalRequired);
    }
}
