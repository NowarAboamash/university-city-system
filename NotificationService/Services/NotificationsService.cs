using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Enums;
using NotificationService.Interfaces;
using NotificationService.Models;

namespace NotificationService.Services
{
    public class NotificationsService : INotificationService
    {
        private readonly NotificationDbContext _context;
        private readonly IDeviceTokenService _deviceTokenService;
        private readonly IPushNotificationSender _pushSender;
        private readonly ILogger<NotificationsService> _logger;

        public NotificationsService(
            NotificationDbContext context,
            IDeviceTokenService deviceTokenService,
            IPushNotificationSender pushSender,
            ILogger<NotificationsService> logger)
        {
            _context = context;
            _deviceTokenService = deviceTokenService;
            _pushSender = pushSender;
            _logger = logger;
        }

        public async Task<PagedResult<NotificationReadDto>> GetAllAsync(PaginationParams parameters)
        {
            var query = _context.Notifications.AsNoTracking().OrderByDescending(n => n.CreatedAt);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(n => new NotificationReadDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Data = n.Data,
                    TargetType = n.TargetType,
                    TargetRole = n.TargetRole,
                    CreatedBy = n.CreatedBy,
                    CreatedAt = n.CreatedAt,
                    SentAt = n.SentAt,
                    RecipientCount = n.Recipients.Count,
                    DeliveredCount = n.Recipients.Count(r => r.DeliveredSuccessfully)
                })
                .ToListAsync();

            return new PagedResult<NotificationReadDto>
            {
                Items = items,
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<NotificationReadDto?> GetByIdAsync(int id)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.Id == id)
                .Select(n => new NotificationReadDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Data = n.Data,
                    TargetType = n.TargetType,
                    TargetRole = n.TargetRole,
                    CreatedBy = n.CreatedBy,
                    CreatedAt = n.CreatedAt,
                    SentAt = n.SentAt,
                    RecipientCount = n.Recipients.Count,
                    DeliveredCount = n.Recipients.Count(r => r.DeliveredSuccessfully)
                })
                .FirstOrDefaultAsync();
        }

        public async Task<(NotificationReadDto? Notification, string? ErrorMessage)> CreateAndSendAsync(NotificationCreateDto dto, string createdBy)
        {
            var validationError = Validate(dto.TargetType, dto.TargetStudentIds, dto.TargetRole);
            if (validationError is not null)
            {
                return (null, validationError);
            }

            var entity = new Notification
            {
                Title = dto.Title,
                Body = dto.Body,
                Data = dto.Data,
                TargetType = dto.TargetType,
                TargetRole = dto.TargetRole,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(entity);
            await _context.SaveChangesAsync();

            await ResolveAndDispatchAsync(entity, dto.TargetType, dto.TargetStudentIds, dto.TargetRole);

            return (await GetByIdAsync(entity.Id), null);
        }

        public async Task<bool> UpdateAsync(int id, NotificationUpdateDto dto)
        {
            var entity = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
            if (entity is null)
            {
                return false;
            }

            entity.Title = dto.Title;
            entity.Body = dto.Body;
            entity.Data = dto.Data;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
            if (entity is null)
            {
                return false;
            }

            _context.Notifications.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResult<NotificationInboxItemDto>> GetInboxAsync(string studentId, PaginationParams parameters)
        {
            var query = _context.NotificationRecipients
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.Notification!.CreatedAt);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(r => new NotificationInboxItemDto
                {
                    RecipientId = r.Id,
                    NotificationId = r.NotificationId,
                    Title = r.Notification!.Title,
                    Body = r.Notification.Body,
                    Data = r.Notification.Data,
                    IsRead = r.IsRead,
                    ReadAt = r.ReadAt,
                    CreatedAt = r.Notification.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<NotificationInboxItemDto>
            {
                Items = items,
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string studentId)
        {
            var recipient = await _context.NotificationRecipients
                .FirstOrDefaultAsync(r => r.NotificationId == notificationId && r.StudentId == studentId);
            if (recipient is null)
            {
                return false;
            }

            if (!recipient.IsRead)
            {
                recipient.IsRead = true;
                recipient.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        private async Task ResolveAndDispatchAsync(
            Notification entity,
            NotificationTargetType targetType,
            List<string>? targetStudentIds,
            string? targetRole)
        {
            List<string> recipientStudentIds = targetType switch
            {
                NotificationTargetType.User or NotificationTargetType.Users =>
                    targetStudentIds!.Distinct().ToList(),
                NotificationTargetType.Role =>
                    await _context.DeviceTokens.AsNoTracking()
                        .Where(t => t.Role == targetRole)
                        .Select(t => t.StudentId)
                        .Distinct()
                        .ToListAsync(),
                NotificationTargetType.Broadcast =>
                    await _context.DeviceTokens.AsNoTracking()
                        .Select(t => t.StudentId)
                        .Distinct()
                        .ToListAsync(),
                _ => []
            };

            var tokens = targetType switch
            {
                NotificationTargetType.User or NotificationTargetType.Users =>
                    await _deviceTokenService.GetTokensForStudentsAsync(recipientStudentIds),
                NotificationTargetType.Role =>
                    await _deviceTokenService.GetTokensForRoleAsync(targetRole!),
                NotificationTargetType.Broadcast =>
                    await _deviceTokenService.GetAllTokensAsync(),
                _ => []
            };

            var deliveredTokens = new HashSet<string>();
            if (tokens.Count > 0)
            {
                try
                {
                    deliveredTokens = (await _pushSender.SendAsync(tokens, entity.Title, entity.Body, entity.Data)).ToHashSet();
                }
                catch (Exception ex)
                {
                    // Push delivery failing must not stop the notification from existing
                    // in recipients' in-app inbox — deliveredTokens stays empty.
                    _logger.LogWarning(ex, "Push delivery failed for notification {NotificationId}", entity.Id);
                }
            }

            var deliveredStudentIds = deliveredTokens.Count == 0
                ? new HashSet<string>()
                : (await _context.DeviceTokens.AsNoTracking()
                    .Where(t => deliveredTokens.Contains(t.FcmToken))
                    .Select(t => t.StudentId)
                    .ToListAsync())
                    .ToHashSet();

            foreach (var studentId in recipientStudentIds)
            {
                _context.NotificationRecipients.Add(new NotificationRecipient
                {
                    NotificationId = entity.Id,
                    StudentId = studentId,
                    IsRead = false,
                    DeliveredSuccessfully = deliveredStudentIds.Contains(studentId)
                });
            }

            entity.SentAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private static string? Validate(NotificationTargetType targetType, List<string>? targetStudentIds, string? targetRole)
        {
            switch (targetType)
            {
                case NotificationTargetType.User:
                    if (targetStudentIds is null || targetStudentIds.Count != 1)
                    {
                        return "TargetType 'User' requires exactly one id in TargetStudentIds.";
                    }
                    break;
                case NotificationTargetType.Users:
                    if (targetStudentIds is null || targetStudentIds.Count == 0)
                    {
                        return "TargetType 'Users' requires at least one id in TargetStudentIds.";
                    }
                    break;
                case NotificationTargetType.Role:
                    if (string.IsNullOrWhiteSpace(targetRole))
                    {
                        return "TargetType 'Role' requires TargetRole.";
                    }
                    break;
                case NotificationTargetType.Broadcast:
                    break;
            }

            return null;
        }
    }
}
