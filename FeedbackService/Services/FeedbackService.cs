using FeedbackService.Data;
using FeedbackService.DTOs;
using FeedbackService.Interfaces;
using FeedbackService.Models;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Notifications;
using SharedKernel.Users;

namespace FeedbackService.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly FeedbackDbContext _context;
        private readonly IFileHandler _fileHandler;
        private readonly IUserLookupService _userLookupService;
        private readonly INotificationPublisher _notificationPublisher;

        public FeedbackService(
            FeedbackDbContext context,
            IFileHandler fileHandler,
            IUserLookupService userLookupService,
            INotificationPublisher notificationPublisher)
        {
            _context = context;
            _fileHandler = fileHandler;
            _userLookupService = userLookupService;
            _notificationPublisher = notificationPublisher;
        }

        public async Task<PagedResult<FeedbackReadDto>> GetAllAsync(PaginationParams parameters, string? studentId)
        {
            var query = _context.Feedbacks.AsNoTracking().AsQueryable();

            if (studentId is not null)
            {
                query = query.Where(f => f.StudentId == studentId);
            }

            query = query.OrderByDescending(f => f.CreatedAt);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(f => new FeedbackReadDto
                {
                    Id = f.Id,
                    StudentId = f.StudentId,
                    Type = f.Type,
                    Title = f.Title,
                    Description = f.Description,
                    IsAnonymous = f.IsAnonymous,
                    IsRead = f.IsRead,
                    CreatedAt = f.CreatedAt,
                    AdminReply = f.AdminReply,
                    RepliedByAdminId = f.RepliedByAdminId,
                    RepliedAt = f.RepliedAt,
                    Images = f.Images
                        .Select(i => new FeedbackImageDto
                        {
                            Id = i.Id,
                            ImagePath = i.ImagePath,
                            FeedbackId = i.FeedbackId
                        })
                        .ToList()
                })
                .ToListAsync();

            await EnrichWithNamesAsync(items);

            return new PagedResult<FeedbackReadDto>
            {
                Items = items,
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount
            };
        }

        private async Task EnrichWithNamesAsync(List<FeedbackReadDto> items)
        {
            var idsToLookup = new HashSet<string>();
            foreach (var item in items)
            {
                if (!item.IsAnonymous)
                {
                    idsToLookup.Add(item.StudentId);
                }

                if (!string.IsNullOrWhiteSpace(item.RepliedByAdminId))
                {
                    idsToLookup.Add(item.RepliedByAdminId);
                }
            }

            if (idsToLookup.Count == 0)
            {
                return;
            }

            var users = await _userLookupService.LookupUsersAsync(idsToLookup);
            if (users.Count == 0)
            {
                return;
            }

            foreach (var item in items)
            {
                // Never populate StudentName for anonymous feedback, even though StudentId
                // is technically known - anonymity is a display rule, not a data-access one.
                if (!item.IsAnonymous && users.TryGetValue(item.StudentId, out var student))
                {
                    item.StudentName = student.FullName;
                }

                if (!string.IsNullOrWhiteSpace(item.RepliedByAdminId) &&
                    users.TryGetValue(item.RepliedByAdminId, out var admin))
                {
                    item.RepliedByAdminName = admin.FullName;
                }
            }
        }

        public async Task<FeedbackReadDto?> GetByIdAsync(int id, bool markAsRead = false)
        {
            if (!markAsRead)
            {
                return await _context.Feedbacks
                    .AsNoTracking()
                    .Where(f => f.Id == id)
                    .Select(f => new FeedbackReadDto
                    {
                        Id = f.Id,
                        StudentId = f.StudentId,
                        Type = f.Type,
                        Title = f.Title,
                        Description = f.Description,
                        IsAnonymous = f.IsAnonymous,
                        IsRead = f.IsRead,
                        CreatedAt = f.CreatedAt,
                        AdminReply = f.AdminReply,
                        RepliedByAdminId = f.RepliedByAdminId,
                        RepliedAt = f.RepliedAt,
                        Images = f.Images
                            .Select(i => new FeedbackImageDto
                            {
                                Id = i.Id,
                                ImagePath = i.ImagePath,
                                FeedbackId = i.FeedbackId
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();
            }

            var entity = await _context.Feedbacks
                .Include(f => f.Images)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (entity is null)
            {
                return null;
            }

            if (!entity.IsRead)
            {
                entity.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return MapToDto(entity);
        }

        public async Task<FeedbackReadDto> CreateAsync(FeedbackCreateDto dto, string studentId)
        {
            var entity = new Feedback
            {
                StudentId = studentId,
                Type = dto.Type,
                Title = dto.Title,
                Description = dto.Description,
                IsAnonymous = dto.IsAnonymous,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(entity);
            await _context.SaveChangesAsync();

            return MapToDto(entity);
        }

        public async Task<(FeedbackReadDto? Feedback, string? ErrorMessage)> CreateWithImagesAsync(FeedbackCreateWithImagesDto dto, string studentId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var entity = new Feedback
                {
                    StudentId = studentId,
                    Type = dto.Type,
                    Title = dto.Title,
                    Description = dto.Description,
                    IsAnonymous = dto.IsAnonymous,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Feedbacks.Add(entity);
                await _context.SaveChangesAsync();

                var savedImagePaths = new List<string>();

                if (dto.Images is not null && dto.Images.Count > 0)
                {
                    foreach (var image in dto.Images)
                    {
                        if (!_fileHandler.IsValidImage(image, out var errorMessage))
                        {
                            await transaction.RollbackAsync();
                            await CleanupSavedImagesAsync(savedImagePaths);
                            return ((FeedbackReadDto?)null, errorMessage);
                        }

                        var imagePath = await _fileHandler.SaveImageAsync(image);
                        if (string.IsNullOrWhiteSpace(imagePath))
                        {
                            await transaction.RollbackAsync();
                            await CleanupSavedImagesAsync(savedImagePaths);
                            return ((FeedbackReadDto?)null, "Failed to save one or more image files.");
                        }

                        savedImagePaths.Add(imagePath);

                        _context.FeedbackImages.Add(new FeedbackImage
                        {
                            FeedbackId = entity.Id,
                            ImagePath = imagePath
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return (MapToDto(entity), (string?)null);
            });
        }

        public async Task<bool> UpdateAsync(int id, FeedbackUpdateDto dto)
        {
            var entity = await _context.Feedbacks.FirstOrDefaultAsync(f => f.Id == id);
            if (entity is null)
            {
                return false;
            }

            entity.Type = dto.Type;
            entity.Title = dto.Title;
            entity.Description = dto.Description;
            entity.IsAnonymous = dto.IsAnonymous;
            entity.IsRead = dto.IsRead;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FeedbackReadDto?> ReplyAsync(int id, FeedbackReplyDto dto, string adminId)
        {
            var entity = await _context.Feedbacks
                .Include(f => f.Images)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (entity is null)
            {
                return null;
            }

            entity.AdminReply = dto.Reply;
            entity.RepliedByAdminId = adminId;
            entity.RepliedAt = DateTime.UtcNow;
            entity.IsRead = true;

            await _context.SaveChangesAsync();

            await _notificationPublisher.NotifyUserAsync(
                entity.StudentId,
                "تم الرد على شكواك",
                "قامت الإدارة بالرد على الشكوى/الملاحظة التي أرسلتها.");

            return MapToDto(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Feedbacks
                .Include(f => f.Images)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (entity is null)
            {
                return false;
            }

            var imagePaths = entity.Images?.Select(i => i.ImagePath).ToList() ?? new List<string>();

            _context.Feedbacks.Remove(entity);
            await _context.SaveChangesAsync();

            foreach (var imagePath in imagePaths)
            {
                await _fileHandler.DeleteImageAsync(imagePath);
            }

            return true;
        }

        private async Task CleanupSavedImagesAsync(IEnumerable<string> imagePaths)
        {
            foreach (var imagePath in imagePaths)
            {
                await _fileHandler.DeleteImageAsync(imagePath);
            }
        }

        private static FeedbackReadDto MapToDto(Feedback entity) => new()
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            Type = entity.Type,
            Title = entity.Title,
            Description = entity.Description,
            IsAnonymous = entity.IsAnonymous,
            IsRead = entity.IsRead,
            CreatedAt = entity.CreatedAt,
            AdminReply = entity.AdminReply,
            RepliedByAdminId = entity.RepliedByAdminId,
            RepliedAt = entity.RepliedAt,
            Images = entity.Images?
                .Select(i => new FeedbackImageDto
                {
                    Id = i.Id,
                    ImagePath = i.ImagePath,
                    FeedbackId = i.FeedbackId
                })
                .ToList() ?? []
        };
    }
}
