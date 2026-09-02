namespace HousingService.DTOs;

/// <summary>Request body for <c>POST /api/allocations/auto-assign</c>.</summary>
public class AutoAssignRequestDto
{
    /// <summary>When true, computes and returns the proposed placement plan without writing
    /// anything. When false, commits the plan (each placement is re-validated at write time).</summary>
    public bool DryRun { get; set; }
}

/// <summary>One proposed (or committed) room placement.</summary>
public class AutoAssignmentDto
{
    public int? HousingRequestId { get; set; }

    public int? HousingGroupId { get; set; }

    /// <summary>Seats this placement consumes — 1 for an individual, member count for a group.</summary>
    public int Size { get; set; }

    public int RoomId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int BuildingId { get; set; }

    public string BuildingName { get; set; } = string.Empty;

    public List<string> StudentIds { get; set; } = [];
}

/// <summary>A target that could not be placed (no fitting room, or rejected during commit).</summary>
public class AutoAssignSkippedDto
{
    /// <summary><c>"individual"</c> or <c>"group"</c>.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>The housing request id (individual) or housing group id (group).</summary>
    public int TargetId { get; set; }

    public int Size { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Outcome of an auto-assign run — the same shape for a dry run and a real commit.</summary>
public class AutoAssignResultDto
{
    public bool DryRun { get; set; }

    /// <summary>Number of targets (individuals + groups) placed.</summary>
    public int PlacedTargets { get; set; }

    /// <summary>Number of students housed (sum of placement sizes).</summary>
    public int HousedStudents { get; set; }

    /// <summary>Number of targets that could not be placed.</summary>
    public int SkippedTargets { get; set; }

    public List<AutoAssignmentDto> Assignments { get; set; } = [];

    public List<AutoAssignSkippedDto> Skipped { get; set; } = [];
}
