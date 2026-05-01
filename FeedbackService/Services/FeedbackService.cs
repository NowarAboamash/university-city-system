using FeedbackService.Data;
using FeedbackService.DTOs;
using FeedbackService.Interfaces;
using FeedbackService.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedbackService.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly FeedbackDbContext _context;
        private readonly IFileHandler _fileHandler;

        public FeedbackService(FeedbackDbContext context, IFileHandler fileHandler)
        {
            _context = context;
            _fileHandler = fileHandler;
        }

        public async Task<IReadOnlyList<FeedbackReadDto>> GetAllAsync()
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .OrderByDescending(f => f.CreatedAt)
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
        }

        public async Task<FeedbackReadDto?> GetByIdAsync(int id)
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

        public async Task<FeedbackReadDto> CreateAsync(FeedbackCreateDto dto)
        {
            var entity = new Feedback
            {
                StudentId = dto.StudentId,
                Type = dto.Type,
                Title = dto.Title,
                Description = dto.Description,
                IsAnonymous = dto.IsAnonymous,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(entity);
            await _context.SaveChangesAsync();

            return new FeedbackReadDto
            {
                Id = entity.Id,
                StudentId = entity.StudentId,
                Type = entity.Type,
                Title = entity.Title,
                Description = entity.Description,
                IsAnonymous = entity.IsAnonymous,
                IsRead = entity.IsRead,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<(FeedbackReadDto? Feedback, string? ErrorMessage)> CreateWithImagesAsync(FeedbackCreateWithImagesDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var entity = new Feedback
            {
                StudentId = dto.StudentId,
                Type = dto.Type,
                Title = dto.Title,
                Description = dto.Description,
                IsAnonymous = dto.IsAnonymous,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(entity);
            await _context.SaveChangesAsync();

            if (dto.Images is not null && dto.Images.Count > 0)
            {
                foreach (var image in dto.Images)
                {
                    if (!_fileHandler.IsValidImage(image, out var errorMessage))
                    {
                        await transaction.RollbackAsync();
                        return (null, errorMessage);
                    }

                    var imagePath = await _fileHandler.SaveImageAsync(image);
                    if (string.IsNullOrWhiteSpace(imagePath))
                    {
                        await transaction.RollbackAsync();
                        return (null, "Failed to save one or more image files.");
                    }

                    _context.FeedbackImages.Add(new FeedbackImage
                    {
                        FeedbackId = entity.Id,
                        ImagePath = imagePath
                    });
                }

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return (new FeedbackReadDto
            {
                Id = entity.Id,
                StudentId = entity.StudentId,
                Type = entity.Type,
                Title = entity.Title,
                Description = entity.Description,
                IsAnonymous = entity.IsAnonymous,
                IsRead = entity.IsRead,
                CreatedAt = entity.CreatedAt
            }, null);
        }

        public async Task<bool> UpdateAsync(int id, FeedbackUpdateDto dto)
        {
            var entity = await _context.Feedbacks.FirstOrDefaultAsync(f => f.Id == id);
            if (entity is null)
            {
                return false;
            }

            entity.StudentId = dto.StudentId;
            entity.Type = dto.Type;
            entity.Title = dto.Title;
            entity.Description = dto.Description;
            entity.IsAnonymous = dto.IsAnonymous;
            entity.IsRead = dto.IsRead;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Feedbacks.FirstOrDefaultAsync(f => f.Id == id);
            if (entity is null)
            {
                return false;
            }

            _context.Feedbacks.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
