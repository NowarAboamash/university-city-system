using FeedbackService.Data;
using FeedbackService.DTOs;
using FeedbackService.Interfaces;
using FeedbackService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FeedbackService.Services
{
    public class FeedbackImageService : IFeedbackImageService
    {
        private readonly FeedbackDbContext _context;
        private readonly IFileHandler _fileHandler;

        public FeedbackImageService(FeedbackDbContext context, IFileHandler fileHandler)
        {
            _context = context;
            _fileHandler = fileHandler;
        }

        public async Task<FeedbackImageReadDto?> AddAsync(FeedbackImageCreateDto dto)
        {
            var feedbackExists = await _context.Feedbacks
                .AsNoTracking()
                .AnyAsync(f => f.Id == dto.FeedbackId);

            if (!feedbackExists)
            {
                return null;
            }

            var entity = new FeedbackImage
            {
                ImagePath = dto.ImagePath,
                FeedbackId = dto.FeedbackId
            };

            _context.FeedbackImages.Add(entity);
            await _context.SaveChangesAsync();

            return new FeedbackImageReadDto
            {
                Id = entity.Id,
                ImagePath = entity.ImagePath,
                FeedbackId = entity.FeedbackId
            };
        }

        public async Task<IReadOnlyList<FeedbackImageReadDto>> GetByFeedbackIdAsync(int feedbackId)
        {
            return await _context.FeedbackImages
                .AsNoTracking()
                .Where(fi => fi.FeedbackId == feedbackId)
                .Select(fi => new FeedbackImageReadDto
                {
                    Id = fi.Id,
                    ImagePath = fi.ImagePath,
                    FeedbackId = fi.FeedbackId
                })
                .ToListAsync();
        }

        public async Task<(FeedbackImageReadDto? Image, string? ErrorMessage)> UploadAsync(IFormFile file, int feedbackId)
        {
            if (!_fileHandler.IsValidImage(file, out var errorMessage))
            {
                return (null, errorMessage);
            }

            var feedbackExists = await _context.Feedbacks
                .AsNoTracking()
                .AnyAsync(f => f.Id == feedbackId);

            if (!feedbackExists)
            {
                return (null, $"Feedback with ID {feedbackId} was not found.");
            }

            var imagePath = await _fileHandler.SaveImageAsync(file);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return (null, "Failed to save image file.");
            }

            var entity = new FeedbackImage
            {
                ImagePath = imagePath,
                FeedbackId = feedbackId
            };

            _context.FeedbackImages.Add(entity);
            await _context.SaveChangesAsync();

            return (new FeedbackImageReadDto
            {
                Id = entity.Id,
                ImagePath = entity.ImagePath,
                FeedbackId = entity.FeedbackId
            }, null);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.FeedbackImages.FirstOrDefaultAsync(fi => fi.Id == id);
            if (entity is null)
            {
                return false;
            }

            await _fileHandler.DeleteImageAsync(entity.ImagePath);
            _context.FeedbackImages.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByPathAsync(string fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath))
            {
                return false;
            }

            var normalized = fileNameOrPath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var entity = await _context.FeedbackImages
                .FirstOrDefaultAsync(fi => fi.ImagePath.EndsWith($"/{fileName}"));

            if (entity is null)
            {
                return false;
            }

            await _fileHandler.DeleteImageAsync(entity.ImagePath);
            _context.FeedbackImages.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
