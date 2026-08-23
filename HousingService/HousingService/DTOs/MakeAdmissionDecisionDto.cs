using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class MakeAdmissionDecisionDto
{
    public AdmissionDecisionStatus Status { get; set; }

    public string? DecisionReason { get; set; }
}
