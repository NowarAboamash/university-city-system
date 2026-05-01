using System.ComponentModel.DataAnnotations;
using StudentService.Enums;

namespace StudentService.Models;

public class Student
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string UniversityId { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? NationalId { get; set; }

    public string? Nationality { get; set; }

    public int CollegeId { get; set; }

    public College? College { get; set; }

    public int GovernorateId { get; set; }

    public Governorate? Governorate { get; set; }

    public string? Village { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
