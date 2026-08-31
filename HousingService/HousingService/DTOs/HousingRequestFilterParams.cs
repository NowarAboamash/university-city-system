using HousingService.Domain.Enums;

namespace HousingService.DTOs;

/// <summary>
/// Query-string filters for the admin <c>GET /api/housing-requests</c> list.
/// Every field is optional; supplied filters are combined with AND and applied at the
/// database level alongside pagination.
/// </summary>
public class HousingRequestFilterParams
{
    public int? HousingCycleId { get; set; }

    public int? GovernorateId { get; set; }

    public HousingRequestStatus? Status { get; set; }

    public AdmissionDecisionStatus? AdmissionStatus { get; set; }

    /// <summary>Repeat the key: <c>?studentIds=a&amp;studentIds=b</c>. Keeps only requests
    /// whose <c>StudentId</c> is in the list (SQL <c>IN</c>). Ignored when empty.</summary>
    public List<string>? StudentIds { get; set; }

    public AcademicLevel? AcademicLevel { get; set; }

    public Gender? Gender { get; set; }

    public bool? IsPaid { get; set; }

    public bool? HasSpecialNeeds { get; set; }

    public bool? IsPreviousResident { get; set; }

    /// <summary><c>true</c> = only requests linked to a housing group; <c>false</c> = only individual (no group).</summary>
    public bool? IsGrouped { get; set; }

    /// <summary>Inclusive lower bound on <c>SubmittedAt</c> (UTC).</summary>
    public DateTime? SubmittedFrom { get; set; }

    /// <summary>Inclusive upper bound on <c>SubmittedAt</c> (UTC).</summary>
    public DateTime? SubmittedTo { get; set; }
}
