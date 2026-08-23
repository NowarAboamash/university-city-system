using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;

namespace HousingService.Services;

public class HousingGroupService : IHousingGroupService
{
    private readonly IHousingGroupRepository _groupRepository;
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IHousingRequestRepository _requestRepository;
    private readonly IHousingCycleRepository _cycleRepository;
    private readonly TimeProvider _timeProvider;

    public HousingGroupService(
        IHousingGroupRepository groupRepository,
        IGroupInvitationRepository invitationRepository,
        IHousingRequestRepository requestRepository,
        IHousingCycleRepository cycleRepository,
        TimeProvider timeProvider)
    {
        _groupRepository = groupRepository;
        _invitationRepository = invitationRepository;
        _requestRepository = requestRepository;
        _cycleRepository = cycleRepository;
        _timeProvider = timeProvider;
    }

    public async Task<HousingGroupDto> CreateAsync(string leaderId, CreateHousingGroupDto dto)
    {
        var cycle = await _cycleRepository.GetOpenAsync();
        if (cycle is null)
        {
            throw new ArgumentException("No housing cycle is currently open.");
        }

        var leaderRequest = await _requestRepository.GetByStudentAndCycleAsync(leaderId, cycle.Id);
        if (leaderRequest is null)
        {
            throw new ArgumentException("You must submit a housing request for the current cycle before creating a group.");
        }

        if (leaderRequest.HousingGroupId is not null)
        {
            throw new ArgumentException("You are already part of a housing group.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var code = await GenerateUniqueCodeAsync(now.Year);

        var group = new HousingGroup
        {
            LeaderId = leaderId,
            Code = code,
            HousingCycleId = cycle.Id,
            Status = HousingGroupStatus.Open,
            MaxMembers = 4,
            Description = dto.Description?.Trim(),
            CreatedAt = now
        };

        // Adding to the tracked navigation collection lets EF assign HousingGroupId
        // via relationship fixup once the group's own Id is generated on save.
        group.Members.Add(leaderRequest);

        await _groupRepository.AddAsync(group);
        await _groupRepository.SaveChangesAsync();

        return MapToDto(group, includeInvitations: true);
    }

    public async Task<HousingGroupDto?> GetMineAsync(string studentId)
    {
        var cycle = await _cycleRepository.GetOpenAsync();
        if (cycle is null)
        {
            return null;
        }

        var request = await _requestRepository.GetByStudentAndCycleAsync(studentId, cycle.Id);
        if (request?.HousingGroupId is null)
        {
            return null;
        }

        var group = await _groupRepository.GetByIdWithDetailsAsync(request.HousingGroupId.Value);
        return group is null ? null : MapToDto(group, includeInvitations: group.LeaderId == studentId);
    }

    public async Task<HousingGroupDto?> GetByIdAsync(int id)
    {
        var group = await _groupRepository.GetByIdWithDetailsAsync(id);
        return group is null ? null : MapToDto(group, includeInvitations: true);
    }

    public async Task<IReadOnlyList<HousingGroupDto>> GetAllAsync(int? housingCycleId)
    {
        var groups = await _groupRepository.GetAllWithDetailsAsync(housingCycleId);
        return groups.Select(g => MapToDto(g, includeInvitations: true)).ToList();
    }

    public async Task<GroupInvitationDto?> JoinByCodeAsync(string studentId, JoinHousingGroupDto dto)
    {
        var group = await _groupRepository.GetByCodeAsync(dto.Code.Trim());
        if (group is null)
        {
            return null;
        }

        if (group.Status != HousingGroupStatus.Open)
        {
            throw new ArgumentException("This group is not accepting new members.");
        }

        var request = await _requestRepository.GetByStudentAndCycleAsync(studentId, group.HousingCycleId);
        if (request is null)
        {
            throw new ArgumentException("You must submit a housing request for this cycle before joining a group.");
        }

        if (request.HousingGroupId is not null)
        {
            throw new ArgumentException("You are already part of a housing group.");
        }

        if (request.Gender != group.Members.First(m => m.StudentId == group.LeaderId).Gender)
        {
            throw new ArgumentException("You must be the same gender as the group's leader to join.");
        }

        var existingInvitation = await _invitationRepository.GetByGroupAndStudentAsync(group.Id, studentId);
        if (existingInvitation is { Status: InvitationStatus.Pending })
        {
            throw new ArgumentException("You already have a pending join request for this group.");
        }

        var pendingCount = group.Invitations.Count(i => i.Status == InvitationStatus.Pending);
        if (group.Members.Count + pendingCount >= group.MaxMembers)
        {
            throw new ArgumentException("This group is full.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var invitation = new GroupInvitation
        {
            HousingGroupId = group.Id,
            InvitedStudentId = studentId,
            InvitedByStudentId = studentId,
            Status = InvitationStatus.Pending,
            SentAt = now,
            CreatedAt = now
        };

        await _invitationRepository.AddAsync(invitation);
        await _invitationRepository.SaveChangesAsync();

        return new GroupInvitationDto
        {
            Id = invitation.Id,
            InvitedStudentId = invitation.InvitedStudentId,
            Status = invitation.Status,
            SentAt = invitation.SentAt
        };
    }

    public async Task<bool?> RespondToInvitationAsync(string leaderId, int invitationId, RespondToInvitationDto dto)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation is null)
        {
            return null;
        }

        var group = await _groupRepository.GetByIdWithDetailsAsync(invitation.HousingGroupId);
        if (group is null || group.LeaderId != leaderId)
        {
            return null;
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new ArgumentException("This join request has already been responded to.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (!dto.Approve)
        {
            invitation.Status = InvitationStatus.Rejected;
            invitation.RespondedAt = now;
            _invitationRepository.Update(invitation);
            await _invitationRepository.SaveChangesAsync();
            return true;
        }

        if (group.Members.Count >= group.MaxMembers)
        {
            throw new ArgumentException("This group is already full.");
        }

        var memberRequest = await _requestRepository.GetByStudentAndCycleAsync(invitation.InvitedStudentId, group.HousingCycleId);
        if (memberRequest is null || memberRequest.HousingGroupId is not null)
        {
            throw new ArgumentException("This student's housing request is no longer eligible to join.");
        }

        memberRequest.HousingGroupId = group.Id;
        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = now;

        _requestRepository.Update(memberRequest);
        _invitationRepository.Update(invitation);
        await _invitationRepository.SaveChangesAsync();

        // Reload the member count post-save to decide whether the group is now full.
        var refreshedGroup = await _groupRepository.GetByIdWithDetailsAsync(group.Id);
        if (refreshedGroup is not null && refreshedGroup.Members.Count >= refreshedGroup.MaxMembers)
        {
            refreshedGroup.Status = HousingGroupStatus.Locked;
            refreshedGroup.LockedAt = now;

            foreach (var pending in refreshedGroup.Invitations.Where(i => i.Status == InvitationStatus.Pending))
            {
                pending.Status = InvitationStatus.Cancelled;
                pending.RespondedAt = now;
                _invitationRepository.Update(pending);
            }

            _groupRepository.Update(refreshedGroup);
            await _groupRepository.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> LeaveAsync(string studentId)
    {
        var cycle = await _cycleRepository.GetOpenAsync();
        if (cycle is null)
        {
            return false;
        }

        var request = await _requestRepository.GetByStudentAndCycleAsync(studentId, cycle.Id);
        if (request?.HousingGroupId is null)
        {
            return false;
        }

        var group = await _groupRepository.GetByIdWithDetailsAsync(request.HousingGroupId.Value);
        if (group is null)
        {
            return false;
        }

        await RemoveMemberFromGroupAsync(group, request);
        return true;
    }

    public async Task<bool?> RemoveMemberAsync(int groupId, string studentId)
    {
        var group = await _groupRepository.GetByIdWithDetailsAsync(groupId);
        if (group is null)
        {
            return null;
        }

        var memberRequest = group.Members.FirstOrDefault(m => m.StudentId == studentId);
        if (memberRequest is null)
        {
            return null;
        }

        await RemoveMemberFromGroupAsync(group, memberRequest);
        return true;
    }

    private async Task RemoveMemberFromGroupAsync(HousingGroup group, HousingRequest memberRequest)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var wasLeader = memberRequest.StudentId == group.LeaderId;
        var remainingMembers = group.Members.Where(m => m.Id != memberRequest.Id).ToList();

        memberRequest.HousingGroupId = null;
        memberRequest.UpdatedAt = now;
        _requestRepository.Update(memberRequest);

        if (wasLeader)
        {
            if (remainingMembers.Count == 0)
            {
                await _requestRepository.SaveChangesAsync();
                _groupRepository.Remove(group);
                await _groupRepository.SaveChangesAsync();
                return;
            }

            var acceptedInvitations = group.Invitations
                .Where(i => i.Status == InvitationStatus.Accepted)
                .OrderBy(i => i.RespondedAt)
                .ToList();

            var newLeaderId = acceptedInvitations
                .Select(i => i.InvitedStudentId)
                .FirstOrDefault(id => remainingMembers.Any(m => m.StudentId == id))
                ?? remainingMembers.First().StudentId;

            group.LeaderId = newLeaderId;
        }
        else if (group.Status == HousingGroupStatus.Locked)
        {
            // A spot freed up in a previously-full group — reopen it to new join requests.
            group.Status = HousingGroupStatus.Open;
            group.LockedAt = null;
        }

        group.UpdatedAt = now;
        _groupRepository.Update(group);
        await _groupRepository.SaveChangesAsync();
    }

    private async Task<string> GenerateUniqueCodeAsync(int year)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = new string(Enumerable.Range(0, 4).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
            var code = $"GRP-{year}-{suffix}";

            var existing = await _groupRepository.GetByCodeAsync(code);
            if (existing is null)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique group code. Please try again.");
    }

    private static HousingGroupDto MapToDto(HousingGroup group, bool includeInvitations)
    {
        return new HousingGroupDto
        {
            Id = group.Id,
            Code = group.Code,
            LeaderId = group.LeaderId,
            HousingCycleId = group.HousingCycleId,
            Status = group.Status,
            MaxMembers = group.MaxMembers,
            MemberStudentIds = group.Members.Select(m => m.StudentId).ToList(),
            Description = group.Description,
            CreatedAt = group.CreatedAt,
            PendingInvitations = includeInvitations
                ? group.Invitations
                    .Where(i => i.Status == InvitationStatus.Pending)
                    .Select(i => new GroupInvitationDto
                    {
                        Id = i.Id,
                        InvitedStudentId = i.InvitedStudentId,
                        Status = i.Status,
                        SentAt = i.SentAt
                    }).ToList()
                : null
        };
    }
}
