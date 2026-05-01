using System.ComponentModel.DataAnnotations;
using StudentService.Enums;

namespace StudentService.DTOs;

public class UpdateStudentDto
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? NationalId { get; set; }

    public string? Nationality { get; set; }

    [Range(1, int.MaxValue)]
    public int CollegeId { get; set; }

    [Range(1, int.MaxValue)]
    public int GovernorateId { get; set; }

    public string? Village { get; set; }

    [Required]
    public StudentStatus Status { get; set; }
}
