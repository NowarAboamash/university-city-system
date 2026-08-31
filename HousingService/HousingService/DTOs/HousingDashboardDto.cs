using HousingService.Domain.Enums;

namespace HousingService.DTOs;

/// <summary>Everything the admin housing dashboard's overview needs, in one payload.</summary>
public class HousingDashboardDto
{
    /// <summary>Housing requests still awaiting an admission decision.</summary>
    public int PendingRequests { get; set; }

    /// <summary>Occupied beds ÷ beds in in-service rooms, as a percentage (1 decimal).</summary>
    public double OccupancyRate { get; set; }

    public int OccupiedBeds { get; set; }

    /// <summary>Total beds across rooms not in Maintenance/Closed.</summary>
    public int TotalBeds { get; set; }

    /// <summary>Distinct students currently housed (individually or via a group).</summary>
    public int TotalHousedStudents { get; set; }

    public DashboardRoomStatusDto Rooms { get; set; } = new();

    /// <summary>Newest housing requests first (up to 6).</summary>
    public List<DashboardRequestDto> RecentRequests { get; set; } = [];

    /// <summary>Occupied beds at the end of each of the last 7 days, oldest first.</summary>
    public List<DashboardOccupancyPointDto> WeeklyOccupancy { get; set; } = [];
}

public class DashboardRoomStatusDto
{
    public int Available { get; set; }

    /// <summary>Rooms with status Occupied or Full.</summary>
    public int Occupied { get; set; }

    /// <summary>Rooms with status Maintenance or Closed.</summary>
    public int OutOfService { get; set; }

    public int Total { get; set; }
}

public class DashboardRequestDto
{
    public int Id { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public string? StudentName { get; set; }

    public int GovernorateId { get; set; }

    public AcademicLevel AcademicLevel { get; set; }

    /// <summary>True when the request is linked to a housing group ("مشتركة"), false for an individual request ("فردية").</summary>
    public bool IsGroup { get; set; }

    public HousingRequestStatus Status { get; set; }

    /// <summary><see cref="AdmissionDecisionStatus.Pending"/> when no decision has been recorded yet.</summary>
    public AdmissionDecisionStatus AdmissionStatus { get; set; }

    public DateTime SubmittedAt { get; set; }
}

public class DashboardOccupancyPointDto
{
    public DateOnly Date { get; set; }

    public int OccupiedBeds { get; set; }
}
