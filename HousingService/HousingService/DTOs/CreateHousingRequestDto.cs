using HousingService.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace HousingService.DTOs;

public class CreateHousingRequestDto
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

    // Mandatory documents — the request cannot be submitted without all five.
    public IFormFile PersonalPhoto { get; set; } = null!;

    public IFormFile NationalIdFront { get; set; } = null!;

    public IFormFile NationalIdBack { get; set; } = null!;

    public IFormFile UniversityIdFront { get; set; } = null!;

    public IFormFile UniversityIdBack { get; set; } = null!;

    // Optional documents — may be added now or requested later by an admin.
    public IFormFile? DepartureReceipt { get; set; }

    public IFormFile? ResidencyProof { get; set; }
}
