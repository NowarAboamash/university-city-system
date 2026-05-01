using StudentService.DTOs;

namespace StudentService.Interfaces;

public interface IStudentService
{
    Task<StudentDto> CreateAsync(CreateStudentDto dto);
    Task<IReadOnlyList<StudentDto>> GetAllAsync();
    Task<StudentDto?> GetByIdAsync(Guid id);
    Task<StudentDto?> UpdateAsync(Guid id, UpdateStudentDto dto);
    Task<bool> DeleteAsync(Guid id);
}
