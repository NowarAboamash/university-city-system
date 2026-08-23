using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class GroupInvitationDto
{
    public int Id { get; set; }

    public string InvitedStudentId { get; set; } = string.Empty;

    public InvitationStatus Status { get; set; }

    public DateTime SentAt { get; set; }
}
