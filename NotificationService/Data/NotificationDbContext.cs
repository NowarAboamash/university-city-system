using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NotificationService.Models;

namespace NotificationService.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
        }

        public virtual DbSet<Notification> Notifications { get; set; }

        public virtual DbSet<NotificationRecipient> NotificationRecipients { get; set; }

        public virtual DbSet<DeviceToken> DeviceTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server's datetime2 doesn't store DateTimeKind, so EF returns
            // DateTime columns as Kind=Unspecified even though the values are always UTC.
            // That makes System.Text.Json omit the "Z" suffix, so clients treat
            // the timestamp as local time instead of UTC. Force Kind=Utc on read
            // so the serialized value is unambiguous.
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(utcConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(utcNullableConverter);
                    }
                }
            }

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(n => n.Id);

                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Body).IsRequired().HasMaxLength(2000);
                entity.Property(n => n.CreatedBy).IsRequired().HasMaxLength(64);
                entity.Property(n => n.TargetType).HasConversion<int>().IsRequired();
                entity.Property(n => n.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<NotificationRecipient>(entity =>
            {
                entity.ToTable("NotificationRecipients");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.StudentId).IsRequired().HasMaxLength(64);

                entity.HasOne(r => r.Notification)
                    .WithMany(n => n.Recipients)
                    .HasForeignKey(r => r.NotificationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(r => new { r.NotificationId, r.StudentId }).IsUnique();
            });

            modelBuilder.Entity<DeviceToken>(entity =>
            {
                entity.ToTable("DeviceTokens");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.StudentId).IsRequired().HasMaxLength(64);
                entity.Property(t => t.Role).IsRequired().HasMaxLength(50);
                entity.Property(t => t.FcmToken).IsRequired().HasMaxLength(500);

                entity.HasIndex(t => t.FcmToken).IsUnique();
                entity.HasIndex(t => t.StudentId);
            });
        }
    }
}
