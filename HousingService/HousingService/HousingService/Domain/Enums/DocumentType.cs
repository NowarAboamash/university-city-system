namespace HousingService.Domain.Enums;

/// <summary>
/// Represents document types in a housing application.
/// The first five are mandatory for every submission; the last two are optional
/// (may be uploaded voluntarily or requested later by an admin).
/// </summary>
public enum DocumentType
{
    PersonalPhoto = 0,
    NationalIdFront = 1,
    NationalIdBack = 2,
    UniversityIdFront = 3,
    UniversityIdBack = 4,

    DepartureReceipt = 5,
    ResidencyProof = 6
}
