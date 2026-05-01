using System.ComponentModel.DataAnnotations;

namespace StudentService.Models;

public class College
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public ICollection<Student> Students { get; set; } = new List<Student>();
}
