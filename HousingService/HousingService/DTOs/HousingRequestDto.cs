using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class HousingRequestDto
{
    public int Id { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    public int GovernorateId { get; set; }

    public AcademicLevel AcademicLevel { get; set; }

    public int HousingCycleId { get; set; }

    public string DetailedAddress { get; set; } = string.Empty;

    public bool HasSpecialNeeds { get; set; }

    public bool IsPreviousResident { get; set; }

    public int? PreviousBuildingId { get; set; }

    public int? PreviousFloor { get; set; }

    public string? PreviousRoomNumber { get; set; }

    public int? HousingGroupId { get; set; }

    public HousingRequestStatus Status { get; set; }

    public string? SpecialNotes { get; set; }

    public List<HousingRequestDocumentDto> Documents { get; set; } = [];

    public AdmissionDecisionDto? Decision { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? LockedAt { get; set; }

    public DateTime? PaymentDueDate { get; set; }

    /// <summary>Fee frozen at acceptance; null before the request is accepted.</summary>
    public decimal? FeeAmount { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>Amount actually charged on payment; null until paid.</summary>
    public decimal? AmountPaid { get; set; }
}
