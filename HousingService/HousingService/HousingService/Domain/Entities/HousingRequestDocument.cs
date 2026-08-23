using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents a document submitted as part of a housing request.
/// </summary>
public class HousingRequestDocument
{
    public int Id { get; set; }
    public int HousingRequestId { get; set; }
    public DocumentType Type { get; set; }
    public required string DocumentPath { get; set; } // Path or URL to the document
    public DocumentReviewStatus ReviewStatus { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual HousingRequest HousingRequest { get; set; } = null!;
}
