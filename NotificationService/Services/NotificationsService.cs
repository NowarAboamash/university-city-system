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
        private readonly IAuthServiceClient _authServiceClient;
        private readonly IPushNotificationSender _pushSender;
        private readonly ILogger<NotificationsService> _logger;

        public NotificationsService(
            NotificationDbContext context,
            IAuthServiceClient authServiceClient,
            IPushNotificationSender pushSender,
            ILogger<NotificationsService> logger)
        {
            _context = context;
            _authServiceClient = authServiceClient;
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
            List<string> recipientStudentIds;
            Dictionary<string, string> tokenToStudentId;

            if (targetType is NotificationTargetType.User or NotificationTargetType.Users)
            {
                recipientStudentIds = targetStudentIds!.Distinct().ToList();

                var lookup = await _authServiceClient.LookupUsersAsync(recipientStudentIds);
                tokenToStudentId = recipientStudentIds
                    .Where(id => lookup.TryGetValue(id, out var u) && !string.IsNullOrEmpty(u.FcmToken))
                    .ToDictionary(id => lookup[id].FcmToken!, id => id);
            }
            else
            {
                // Role and Broadcast: AuthService only returns active users that already
                // have a registered token, so the returned set doubles as both the
                // recipient list and the token source.
                var role = targetType == NotificationTargetType.Role ? targetRole : null;
                var users = await _authServiceClient.GetFcmTokensAsync(role);

                recipientStudentIds = users.Select(u => u.Id).Distinct().ToList();
                tokenToStudentId = users
                    .Where(u => !string.IsNullOrEmpty(u.FcmToken))
                    .GroupBy(u => u.FcmToken!)
                    .ToDictionary(g => g.Key, g => g.First().Id);
            }

            var tokens = tokenToStudentId.Keys.ToList();

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

            var deliveredStudentIds = deliveredTokens
                .Where(t => tokenToStudentId.ContainsKey(t))
                .Select(t => tokenToStudentId[t])
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
