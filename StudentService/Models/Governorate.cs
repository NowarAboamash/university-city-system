using System.ComponentModel.DataAnnotations;

namespace StudentService.Models;

public class Governorate
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;
}
