using HousingService.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace HousingService.DTOs;

public class UpdateHousingRequestDto
{
    public Gender Gender { get; set; }

    public int GovernorateId { get; set; }

    public AcademicLevel AcademicLevel { get; set; }

    public string DetailedAddress { get; set; } = string.Empty;

    public bool HasSpecialNeeds { get; set; }

    public bool IsPreviousResident { get; set; }

    public int? PreviousBuildingId { get; set; }

    public int? PreviousFloor { get; set; }

    public string? PreviousRoomNumber { get; set; }

    public string? Notes { get; set; }

    // Only re-upload a document if it's being replaced (e.g. fixing one an admin rejected).
    public IFormFile? PersonalPhoto { get; set; }

    public IFormFile? NationalIdFront { get; set; }

    public IFormFile? NationalIdBack { get; set; }

    public IFormFile? UniversityIdFront { get; set; }

    public IFormFile? UniversityIdBack { get; set; }

    public IFormFile? DepartureReceipt { get; set; }

    public IFormFile? ResidencyProof { get; set; }
}
