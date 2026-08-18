using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Interfaces;
using NotificationService.Models;

namespace NotificationService.Services
{
    public class DeviceTokenService : IDeviceTokenService
    {
        private readonly NotificationDbContext _context;

        public DeviceTokenService(NotificationDbContext context)
        {
            _context = context;
        }

        public async Task RegisterAsync(string studentId, string role, string fcmToken, string? platform)
        {
            var existing = await _context.DeviceTokens.FirstOrDefaultAsync(t => t.FcmToken == fcmToken);
            if (existing is not null)
            {
                existing.StudentId = studentId;
                existing.Role = role;
                existing.Platform = platform;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.DeviceTokens.Add(new DeviceToken
                {
                    StudentId = studentId,
                    Role = role,
                    FcmToken = fcmToken,
                    Platform = platform,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> UnregisterAsync(string studentId, string fcmToken)
        {
            var token = await _context.DeviceTokens
                .FirstOrDefaultAsync(t => t.StudentId == studentId && t.FcmToken == fcmToken);
            if (token is null)
            {
                return false;
            }

            _context.DeviceTokens.Remove(token);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<string>> GetTokensForStudentAsync(string studentId)
        {
            return await _context.DeviceTokens
                .AsNoTracking()
                .Where(t => t.StudentId == studentId)
                .Select(t => t.FcmToken)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<string>> GetTokensForStudentsAsync(IEnumerable<string> studentIds)
        {
            var ids = studentIds.ToList();
            return await _context.DeviceTokens
                .AsNoTracking()
                .Where(t => ids.Contains(t.StudentId))
                .Select(t => t.FcmToken)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<string>> GetTokensForRoleAsync(string role)
        {
            return await _context.DeviceTokens
                .AsNoTracking()
                .Where(t => t.Role == role)
                .Select(t => t.FcmToken)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<string>> GetAllTokensAsync()
        {
            return await _context.DeviceTokens
                .AsNoTracking()
                .Select(t => t.FcmToken)
                .ToListAsync();
        }
    }
}
