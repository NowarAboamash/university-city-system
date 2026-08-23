using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class HousingRequestDocumentDto
{
    public int Id { get; set; }

    public DocumentType Type { get; set; }

    public string DocumentPath { get; set; } = string.Empty;

    public DocumentReviewStatus ReviewStatus { get; set; }

    public string? ReviewNotes { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
