using HousingService.Data.Repositories;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

/// <summary>
/// The enforcement half of the housing-fee workflow (the reminder half is
/// <see cref="IPaymentReminderService"/>). Runs on the same daily job.
/// </summary>
public class UnpaidEvictionService : IUnpaidEvictionService
{
    private const string PerformedBy = "system:payment-enforcement";

    private readonly IHousingRequestRepository _requestRepository;
    private readonly IHousingRequestService _requestService;
    private readonly TimeProvider _timeProvider;

    public UnpaidEvictionService(
        IHousingRequestRepository requestRepository,
        IHousingRequestService requestService,
        TimeProvider timeProvider)
    {
        _requestRepository = requestRepository;
        _requestService = requestService;
        _timeProvider = timeProvider;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Compare due dates against the start of today: a request is overdue only once its
        // deadline *day* has fully passed (the 15th day itself still belongs to the student).
        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;

        var overdue = (await _requestRepository.GetOverdueUnpaidAcceptedAsync(today)).ToList();
        if (overdue.Count == 0)
        {
            return 0;
        }

        var evicted = 0;
        foreach (var request in overdue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // MakeDecisionAsync does the rest: for a grouped member it delegates to the group
            // removal routine (which also frees/re-syncs the room); for an individual it vacates
            // any active allocation; either way it notifies the student with the NonPayment wording.
            await _requestService.MakeDecisionAsync(
                request.Id,
                new MakeAdmissionDecisionDto
                {
                    Status = AdmissionDecisionStatus.Rejected,
                    RejectionReason = RejectionReason.NonPayment,
                    DecisionReason = "لم يتم دفع رسوم السكن خلال المهلة المحددة."
                },
                PerformedBy);

            evicted++;
        }

        return evicted;
    }
}
