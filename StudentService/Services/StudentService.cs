using Microsoft.EntityFrameworkCore;
using StudentService.Data;
using StudentService.DTOs;
using StudentService.Enums;
using StudentService.Interfaces;
using StudentService.Models;

namespace StudentService.Services;

public class StudentService : IStudentService
{
    private readonly AppDbContext _context;

    public StudentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDto> CreateAsync(CreateStudentDto dto)
    {
        var userExists = await _context.Students.AsNoTracking().AnyAsync(s => s.UserId == dto.UserId);
        if (userExists)
        {
            throw new InvalidOperationException("Student with the same user id already exists.");
        }

        var college = await _context.Colleges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CollegeId);
        if (college is null)
        {
            throw new InvalidOperationException("Invalid college id.");
        }

        var governorate = await _context.Governorates.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == dto.GovernorateId);
        if (governorate is null)
        {
            throw new InvalidOperationException("Invalid governorate id.");
        }

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            FullName = dto.FullName,
            UniversityId = dto.UniversityId,
            PhoneNumber = dto.PhoneNumber,
            NationalId = dto.NationalId,
            Nationality = dto.Nationality,
            CollegeId = dto.CollegeId,
            GovernorateId = dto.GovernorateId,
            Village = dto.Village,
            Status = StudentStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        return new StudentDto
        {
            Id = student.Id,
            FullName = student.FullName,
            UniversityId = student.UniversityId,
            PhoneNumber = student.PhoneNumber,
            NationalId = student.NationalId,
            Nationality = student.Nationality,
            CollegeName = college.Name,
            GovernorateName = governorate.Name,
            Village = student.Village,
            Status = student.Status
        };
    }

    public async Task<IReadOnlyList<StudentDto>> GetAllAsync()
    {
        return await _context.Students
            .AsNoTracking()
            .Include(s => s.College)
            .Include(s => s.Governorate)
            .Select(s => new StudentDto
            {
                Id = s.Id,
                FullName = s.FullName,
                UniversityId = s.UniversityId,
                PhoneNumber = s.PhoneNumber,
                NationalId = s.NationalId,
                Nationality = s.Nationality,
                CollegeName = s.College != null ? s.College.Name : string.Empty,
                GovernorateName = s.Governorate != null ? s.Governorate.Name : string.Empty,
                Village = s.Village,
                Status = s.Status
            })
            .ToListAsync();
    }

    public async Task<StudentDto?> GetByIdAsync(Guid id)
    {
        return await _context.Students
            .AsNoTracking()
            .Include(s => s.College)
            .Include(s => s.Governorate)
            .Where(s => s.Id == id)
            .Select(s => new StudentDto
            {
                Id = s.Id,
                FullName = s.FullName,
                UniversityId = s.UniversityId,
                PhoneNumber = s.PhoneNumber,
                NationalId = s.NationalId,
                Nationality = s.Nationality,
                CollegeName = s.College != null ? s.College.Name : string.Empty,
                GovernorateName = s.Governorate != null ? s.Governorate.Name : string.Empty,
                Village = s.Village,
                Status = s.Status
            })
            .FirstOrDefaultAsync();
    }

    public async Task<StudentDto?> UpdateAsync(Guid id, UpdateStudentDto dto)
    {
        var student = await _context.Students
            .Include(s => s.College)
            .Include(s => s.Governorate)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null)
        {
            return null;
        }

        var collegeExists = await _context.Colleges.AsNoTracking()
            .AnyAsync(c => c.Id == dto.CollegeId);
        if (!collegeExists)
        {
            throw new InvalidOperationException("Invalid college id.");
        }

        var governorateExists = await _context.Governorates.AsNoTracking()
            .AnyAsync(g => g.Id == dto.GovernorateId);
        if (!governorateExists)
        {
            throw new InvalidOperationException("Invalid governorate id.");
        }

        student.FullName = dto.FullName;
        student.PhoneNumber = dto.PhoneNumber;
        student.NationalId = dto.NationalId;
        student.Nationality = dto.Nationality;
        student.CollegeId = dto.CollegeId;
        student.GovernorateId = dto.GovernorateId;
        student.Village = dto.Village;
        student.Status = dto.Status;

        await _context.SaveChangesAsync();

        var collegeName = student.College?.Name ?? string.Empty;
        var governorateName = student.Governorate?.Name ?? string.Empty;

        return new StudentDto
        {
            Id = student.Id,
            FullName = student.FullName,
            UniversityId = student.UniversityId,
            PhoneNumber = student.PhoneNumber,
            NationalId = student.NationalId,
            Nationality = student.Nationality,
            CollegeName = collegeName,
            GovernorateName = governorateName,
            Village = student.Village,
            Status = student.Status
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student is null)
        {
            return false;
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
        return true;
    }
}
