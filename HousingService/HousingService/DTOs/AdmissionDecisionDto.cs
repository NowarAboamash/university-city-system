using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class AdmissionDecisionDto
{
    public int Id { get; set; }

    public AdmissionDecisionStatus Status { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime DecisionDate { get; set; }

    public string? ReviewedBy { get; set; }
}
