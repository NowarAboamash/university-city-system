using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Http;
using SharedKernel.Media;
using SharedKernel.Notifications;
using System.Text.Json;

namespace HousingService.Services;

public class HousingRequestService : IHousingRequestService
{
    private readonly IHousingRequestRepository _requestRepository;
    private readonly IGovernorateRepository _governorateRepository;
    private readonly IHousingCycleRepository _cycleRepository;
    private readonly IBuildingRepository _buildingRepository;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IImageUploader _imageUploader;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IHousingGroupService _groupService;
    private readonly IAllocationService _allocationService;
    private readonly TimeProvider _timeProvider;

    public HousingRequestService(
        IHousingRequestRepository requestRepository,
        IGovernorateRepository governorateRepository,
        IHousingCycleRepository cycleRepository,
        IBuildingRepository buildingRepository,
        IAllocationRepository allocationRepository,
        IGroupInvitationRepository invitationRepository,
        IImageUploader imageUploader,
        INotificationPublisher notificationPublisher,
        IHousingGroupService groupService,
        IAllocationService allocationService,
        TimeProvider timeProvider)
    {
        _requestRepository = requestRepository;
        _governorateRepository = governorateRepository;
        _cycleRepository = cycleRepository;
        _buildingRepository = buildingRepository;
        _allocationRepository = allocationRepository;
        _invitationRepository = invitationRepository;
        _imageUploader = imageUploader;
        _notificationPublisher = notificationPublisher;
        _groupService = groupService;
        _allocationService = allocationService;
        _timeProvider = timeProvider;
    }

    public async Task<HousingRequestDto> CreateAsync(string studentId, CreateHousingRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DetailedAddress))
        {
            throw new ArgumentException("DetailedAddress is required.");
        }

        var governorate = await _governorateRepository.GetByIdAsync(dto.GovernorateId);
        if (governorate is null)
        {
            throw new ArgumentException("Governorate was not found.");
        }

        var cycle = await _cycleRepository.GetOpenAsync();
        if (cycle is null)
        {
            throw new ArgumentException("No housing cycle is currently open for applications.");
        }

        var existing = await _requestRepository.GetByStudentAndCycleAsync(studentId, cycle.Id);
        if (existing is not null)
        {
            throw new ArgumentException("You already have a housing request for the current cycle.");
        }

        if (dto.IsPreviousResident)
        {
            if (dto.PreviousBuildingId is null)
            {
                throw new ArgumentException("PreviousBuildingId is required when IsPreviousResident is true.");
            }

            var previousBuilding = await _buildingRepository.GetByIdAsync(dto.PreviousBuildingId.Value);
            if (previousBuilding is null)
            {
                throw new ArgumentException("PreviousBuildingId was not found.");
            }
        }

        ValidateRequiredFile(dto.PersonalPhoto, nameof(dto.PersonalPhoto));
        ValidateRequiredFile(dto.NationalIdFront, nameof(dto.NationalIdFront));
        ValidateRequiredFile(dto.NationalIdBack, nameof(dto.NationalIdBack));
        ValidateRequiredFile(dto.UniversityIdFront, nameof(dto.UniversityIdFront));
        ValidateRequiredFile(dto.UniversityIdBack, nameof(dto.UniversityIdBack));

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var request = new HousingRequest
        {
            StudentId = studentId,
            Gender = dto.Gender,
            GovernorateId = dto.GovernorateId,
            AcademicLevel = dto.AcademicLevel,
            HousingCycleId = cycle.Id,
            DetailedAddress = dto.DetailedAddress.Trim(),
            HasSpecialNeeds = dto.HasSpecialNeeds,
            IsPreviousResident = dto.IsPreviousResident,
            PreviousBuildingId = dto.IsPreviousResident ? dto.PreviousBuildingId : null,
            PreviousFloor = dto.IsPreviousResident ? dto.PreviousFloor : null,
            PreviousRoomNumber = dto.IsPreviousResident ? dto.PreviousRoomNumber?.Trim() : null,
            SpecialNotes = dto.Notes?.Trim(),
            Status = HousingRequestStatus.Submitted,
            SubmittedAt = now,
            CreatedAt = now
        };

        await AddDocumentAsync(request, DocumentType.PersonalPhoto, dto.PersonalPhoto, now);
        await AddDocumentAsync(request, DocumentType.NationalIdFront, dto.NationalIdFront, now);
        await AddDocumentAsync(request, DocumentType.NationalIdBack, dto.NationalIdBack, now);
        await AddDocumentAsync(request, DocumentType.UniversityIdFront, dto.UniversityIdFront, now);
        await AddDocumentAsync(request, DocumentType.UniversityIdBack, dto.UniversityIdBack, now);

        if (dto.DepartureReceipt is { Length: > 0 })
        {
            await AddDocumentAsync(request, DocumentType.DepartureReceipt, dto.DepartureReceipt, now);
        }

        if (dto.ResidencyProof is { Length: > 0 })
        {
            await AddDocumentAsync(request, DocumentType.ResidencyProof, dto.ResidencyProof, now);
        }

        await _requestRepository.AddAsync(request);
        await _requestRepository.SaveChangesAsync();

        return MapToDto(request);
    }

    public async Task<IReadOnlyList<HousingRequestDto>> GetMineAsync(string studentId)
    {
        var requests = await _requestRepository.GetByStudentIdAsync(studentId);
        return requests.Select(MapToDto).ToList();
    }

    public async Task<HousingRequestDto?> GetMineByIdAsync(string studentId, int id)
    {
        var request = await _requestRepository.GetByIdWithDocumentsAsync(id);
        if (request is null || request.StudentId != studentId)
        {
            return null;
        }

        return MapToDto(request);
    }

    public async Task<PagedResult<HousingRequestDto>> GetAllAsync(int? housingCycleId, int? governorateId, HousingRequestStatus? status, AdmissionDecisionStatus? admissionStatus, PaginationParams pagination)
    {
        var (requests, totalCount) = await _requestRepository.GetAllWithFiltersAsync(housingCycleId, governorateId, status, admissionStatus, pagination);
        return new PagedResult<HousingRequestDto>
        {
            Items = requests.Select(MapToDto).ToList(),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<HousingRequestDto?> GetByIdAsync(int id)
    {
        var request = await _requestRepository.GetByIdWithDocumentsAsync(id);
        return request is null ? null : MapToDto(request);
    }

    public async Task<bool?> UpdateMineAsync(string studentId, int id, UpdateHousingRequestDto dto)
    {
        var request = await _requestRepository.GetByIdWithDocumentsAsync(id);
        if (request is null || request.StudentId != studentId)
        {
            return null;
        }

        if (request.Status == HousingRequestStatus.Locked)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(dto.DetailedAddress))
        {
            throw new ArgumentException("DetailedAddress is required.");
        }

        var governorate = await _governorateRepository.GetByIdAsync(dto.GovernorateId);
        if (governorate is null)
        {
            throw new ArgumentException("Governorate was not found.");
        }

        if (dto.IsPreviousResident)
        {
            if (dto.PreviousBuildingId is null)
            {
                throw new ArgumentException("PreviousBuildingId is required when IsPreviousResident is true.");
            }

            var previousBuilding = await _buildingRepository.GetByIdAsync(dto.PreviousBuildingId.Value);
            if (previousBuilding is null)
            {
                throw new ArgumentException("PreviousBuildingId was not found.");
            }
        }

        if (request.HousingGroupId is not null && dto.Gender != request.Gender)
        {
            throw new ArgumentException("Cannot change Gender while part of a housing group. Leave the group first.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        request.Gender = dto.Gender;
        request.GovernorateId = dto.GovernorateId;
        request.AcademicLevel = dto.AcademicLevel;
        request.DetailedAddress = dto.DetailedAddress.Trim();
        request.HasSpecialNeeds = dto.HasSpecialNeeds;
        request.IsPreviousResident = dto.IsPreviousResident;
        request.PreviousBuildingId = dto.IsPreviousResident ? dto.PreviousBuildingId : null;
        request.PreviousFloor = dto.IsPreviousResident ? dto.PreviousFloor : null;
        request.PreviousRoomNumber = dto.IsPreviousResident ? dto.PreviousRoomNumber?.Trim() : null;
        request.SpecialNotes = dto.Notes?.Trim();
        request.UpdatedAt = now;

        await ReplaceDocumentIfProvidedAsync(request, DocumentType.PersonalPhoto, dto.PersonalPhoto, now);
        await ReplaceDocumentIfProvidedAsync(request, DocumentType.NationalIdFront, dto.NationalIdFront, now);
        await ReplaceDocumentIfProvidedAsync(request, DocumentType.NationalIdBack, dto.NationalIdBack, now);
        await ReplaceDocumentIfProvidedAsync(request, DocumentType.UniversityIdFront, dto.UniversityIdFront, now);
        await ReplaceDocumentIfProvidedAsync(request, DocumentType.UniversityIdBack, dto.UniversityIdBack, now);
        await ReplaceDocumentIfProvidedAsync(request, DocumentType.DepartureReceipt, dto.DepartureReceipt, now);
        await ReplaceDocumentIfProvidedAsync(request, DocumentType.ResidencyProof, dto.ResidencyProof, now);

        // Editing is how the student addresses a NeedsRevision flag, so it always
        // sends the request back to the admin's queue.
        request.Status = HousingRequestStatus.Submitted;

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool?> ReviewDocumentAsync(int requestId, int documentId, ReviewDocumentDto dto, string reviewedBy)
    {
        var request = await _requestRepository.GetByIdWithDocumentsAsync(requestId);
        if (request is null)
        {
            return null;
        }

        var document = request.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
        {
            return null;
        }

        if (request.Status == HousingRequestStatus.Locked)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        document.ReviewStatus = dto.Approve ? DocumentReviewStatus.Approved : DocumentReviewStatus.Rejected;
        document.ReviewNotes = dto.ReviewNotes?.Trim();
        document.ReviewedAt = now;

        if (!dto.Approve)
        {
            request.Status = HousingRequestStatus.NeedsRevision;
            request.UpdatedAt = now;

            var data = JsonSerializer.Serialize(new { requestId = request.Id, documentId = document.Id });
            await _notificationPublisher.NotifyUserAsync(
                request.StudentId,
                "طلب السكن يحتاج تعديل",
                "هناك مستند بحاجة إلى تصحيح ضمن طلب التسكين الخاص بك. يرجى مراجعة طلبك وتحديثه.",
                data);
        }

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync();
        return true;
    }

    public async Task<HousingRequestDto?> MakeDecisionAsync(int requestId, MakeAdmissionDecisionDto dto, string reviewedBy)
    {
        if (dto.Status == AdmissionDecisionStatus.Pending)
        {
            throw new ArgumentException("Status must be Accepted, Rejected, or WaitingList.");
        }

        var request = await _requestRepository.GetByIdWithDocumentsAsync(requestId);
        if (request is null)
        {
            return null;
        }

        if (request.Status == HousingRequestStatus.NeedsRevision)
        {
            throw new ArgumentException("Cannot record a decision while the request needs revision. Resolve outstanding document issues first.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var isFirstDecision = request.AdmissionDecision is null;

        if (request.AdmissionDecision is null)
        {
            request.AdmissionDecision = new AdmissionDecision
            {
                HousingRequestId = request.Id,
                CreatedAt = now
            };
        }
        else
        {
            request.AdmissionDecision.UpdatedAt = now;
        }

        request.AdmissionDecision.Status = dto.Status;
        request.AdmissionDecision.DecisionReason = dto.DecisionReason?.Trim();
        request.AdmissionDecision.DecisionDate = now;
        request.AdmissionDecision.ReviewedBy = reviewedBy;

        if (isFirstDecision)
        {
            request.Status = HousingRequestStatus.Locked;
            request.LockedAt = now;
        }

        request.UpdatedAt = now;

        // Accepting/rejecting the whole request implicitly finalizes any document still
        // awaiting individual review — an admin doesn't decide the request without having
        // judged its documents. Already-reviewed documents (from ReviewDocumentAsync) are left as-is.
        if (dto.Status is AdmissionDecisionStatus.Accepted or AdmissionDecisionStatus.Rejected)
        {
            var finalStatus = dto.Status == AdmissionDecisionStatus.Accepted
                ? DocumentReviewStatus.Approved
                : DocumentReviewStatus.Rejected;

            foreach (var document in request.Documents.Where(d => d.ReviewStatus == DocumentReviewStatus.Pending))
            {
                document.ReviewStatus = finalStatus;
                document.ReviewedAt = now;
            }
        }

        // Payment integration hook: a future PaymentService charges the housing fee once a
        // request is Accepted. Not wired up yet — PaymentService doesn't exist (see project
        // memory) — but this is the single point where that call/event would be triggered.

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync();

        // A member who's no longer Accepted (rejected outright, or bumped back to the waiting
        // list) is implicitly excluded from any group they were part of — no grace period
        // needed since this is a deliberate admin decision, not a stuck-pending situation.
        if (dto.Status is AdmissionDecisionStatus.Rejected or AdmissionDecisionStatus.WaitingList)
        {
            if (request.HousingGroupId is not null)
            {
                // Reuses the shared removal routine, which also keeps the room in sync
                // (recomputes occupancy, vacates the allocation if this was the last member).
                await _groupService.RemoveMemberAsync(request.HousingGroupId.Value, request.StudentId);
            }
            else
            {
                // An individual student who's no longer Accepted shouldn't keep occupying a
                // room either — free it up the same way an admin-triggered vacate would.
                var activeAllocation = await _allocationRepository.GetByHousingRequestIdAsync(request.Id);
                if (activeAllocation is not null)
                {
                    await _allocationService.VacateAsync(activeAllocation.Id, new VacateAllocationDto
                    {
                        Message = "تم إلغاء تخصيص سكنك بسبب تغيير حالة طلب التسكين الخاص بك."
                    });
                }
            }
        }

        var (title, body) = dto.Status switch
        {
            AdmissionDecisionStatus.Accepted => ("تم قبول طلب التسكين", "تهانينا! تم قبول طلب التسكين الخاص بك."),
            AdmissionDecisionStatus.Rejected => ("تم رفض طلب التسكين", "نأسف، تم رفض طلب التسكين الخاص بك."),
            AdmissionDecisionStatus.WaitingList => ("طلب التسكين على قائمة الانتظار", "تم وضع طلب التسكين الخاص بك على قائمة الانتظار."),
            _ => ("تحديث على طلب التسكين", "تم تحديث حالة طلب التسكين الخاص بك.")
        };

        await _notificationPublisher.NotifyUserAsync(request.StudentId, title, body);

        return MapToDto(request);
    }

    public async Task<bool?> DeleteAsync(int id, string performedBy, bool performedByAdmin)
    {
        var request = await _requestRepository.GetByIdWithDocumentsAsync(id);
        if (request is null)
        {
            return null;
        }

        // A student who is still actively housed (individually or via their group) can't just
        // delete their application out from under a real, physically occupied room — the
        // building must be evacuated (or the allocation otherwise cleared) first.
        var activeAllocation = await _allocationRepository.GetByHousingRequestIdAsync(id);
        if (activeAllocation is null && request.HousingGroupId is not null)
        {
            activeAllocation = await _allocationRepository.GetByGroupIdAsync(request.HousingGroupId.Value);
        }

        if (activeAllocation is not null)
        {
            throw new ArgumentException("Cannot delete a housing request with an active room allocation. The student must be evacuated first.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Leaving the group first reuses the existing leave/remove flow: leadership transfer
        // (or group deletion if this was the last member) and notifying the remaining members.
        if (request.HousingGroupId is not null)
        {
            await _groupService.RemoveMemberAsync(request.HousingGroupId.Value, request.StudentId);
        }

        // Any join requests this student has pending against OTHER groups no longer make sense
        // once their own housing request is gone — cancel them so leaders aren't left reviewing stale ones.
        var pendingInvitations = (await _invitationRepository.GetPendingInvitationsAsync(request.StudentId)).ToList();
        foreach (var invitation in pendingInvitations)
        {
            invitation.Status = InvitationStatus.Cancelled;
            invitation.RespondedAt = now;
            _invitationRepository.Update(invitation);
        }

        if (pendingInvitations.Count > 0)
        {
            await _invitationRepository.SaveChangesAsync();
        }

        foreach (var document in request.Documents)
        {
            await _imageUploader.DeleteAsync(document.DocumentPath);
        }

        _requestRepository.Remove(request);
        await _requestRepository.SaveChangesAsync();

        if (performedByAdmin && performedBy != request.StudentId)
        {
            await _notificationPublisher.NotifyUserAsync(
                request.StudentId,
                "تم حذف طلب التسكين",
                "قام أحد الإداريين بحذف طلب التسكين الخاص بك.");
        }

        return true;
    }

    private static void ValidateRequiredFile(IFormFile? file, string fieldName)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private async Task AddDocumentAsync(HousingRequest request, DocumentType type, IFormFile file, DateTime now)
    {
        await using var stream = file.OpenReadStream();
        var path = await _imageUploader.UploadAsync(stream, file.FileName, "housing-documents");

        request.Documents.Add(new HousingRequestDocument
        {
            Type = type,
            DocumentPath = path,
            ReviewStatus = DocumentReviewStatus.Pending,
            UploadedAt = now,
            CreatedAt = now
        });
    }

    private async Task ReplaceDocumentIfProvidedAsync(HousingRequest request, DocumentType type, IFormFile? file, DateTime now)
    {
        if (file is null || file.Length == 0)
        {
            return;
        }

        await using var stream = file.OpenReadStream();
        var path = await _imageUploader.UploadAsync(stream, file.FileName, "housing-documents");

        var existing = request.Documents.FirstOrDefault(d => d.Type == type);
        if (existing is null)
        {
            request.Documents.Add(new HousingRequestDocument
            {
                Type = type,
                DocumentPath = path,
                ReviewStatus = DocumentReviewStatus.Pending,
                UploadedAt = now,
                CreatedAt = now
            });
            return;
        }

        await _imageUploader.DeleteAsync(existing.DocumentPath);
        existing.DocumentPath = path;
        existing.ReviewStatus = DocumentReviewStatus.Pending;
        existing.ReviewNotes = null;
        existing.UploadedAt = now;
        existing.ReviewedAt = null;
        existing.UpdatedAt = now;
    }

    private static HousingRequestDto MapToDto(HousingRequest request)
    {
        return new HousingRequestDto
        {
            Id = request.Id,
            StudentId = request.StudentId,
            Gender = request.Gender,
            GovernorateId = request.GovernorateId,
            AcademicLevel = request.AcademicLevel,
            HousingCycleId = request.HousingCycleId,
            DetailedAddress = request.DetailedAddress,
            HasSpecialNeeds = request.HasSpecialNeeds,
            IsPreviousResident = request.IsPreviousResident,
            PreviousBuildingId = request.PreviousBuildingId,
            PreviousFloor = request.PreviousFloor,
            PreviousRoomNumber = request.PreviousRoomNumber,
            HousingGroupId = request.HousingGroupId,
            Status = request.Status,
            SpecialNotes = request.SpecialNotes,
            Documents = request.Documents.Select(d => new HousingRequestDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                DocumentPath = d.DocumentPath,
                ReviewStatus = d.ReviewStatus,
                ReviewNotes = d.ReviewNotes,
                UploadedAt = d.UploadedAt,
                ReviewedAt = d.ReviewedAt
            }).ToList(),
            Decision = request.AdmissionDecision is null ? null : new AdmissionDecisionDto
            {
                Id = request.AdmissionDecision.Id,
                Status = request.AdmissionDecision.Status,
                DecisionReason = request.AdmissionDecision.DecisionReason,
                DecisionDate = request.AdmissionDecision.DecisionDate,
                ReviewedBy = request.AdmissionDecision.ReviewedBy
            },
            SubmittedAt = request.SubmittedAt,
            LockedAt = request.LockedAt
        };
    }
}
