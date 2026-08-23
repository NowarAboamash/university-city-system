namespace HousingService.DTOs;

public class EvacuationResultDto
{
    public int VacatedAllocationsCount { get; set; }

    public List<string> NotifiedStudentIds { get; set; } = [];
}
