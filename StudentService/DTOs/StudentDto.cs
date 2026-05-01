using StudentService.Enums;

namespace StudentService.DTOs;

public class StudentDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string UniversityId { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? NationalId { get; set; }

    public string? Nationality { get; set; }

    public string CollegeName { get; set; } = string.Empty;

    public string GovernorateName { get; set; } = string.Empty;

    public string? Village { get; set; }

    public StudentStatus Status { get; set; }
}
