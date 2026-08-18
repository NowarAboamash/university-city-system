using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using FeedbackService.Models;
namespace FeedbackService.Data
{
    public partial class FeedbackDbContext :DbContext
    {
        public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options) : base(options)
        {
        }

        public virtual DbSet<Feedback> Feedbacks { get; set; }

        public virtual DbSet<FeedbackImage>  FeedbackImages {  get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server's datetime2 doesn't store DateTimeKind, so EF returns
            // CreatedAt as Kind=Unspecified even though the value is always UTC.
            // That makes System.Text.Json omit the "Z" suffix, so clients treat
            // the timestamp as local time instead of UTC. Force Kind=Utc on read
            // so the serialized value is unambiguous.
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(utcConverter);
                    }
                }
            }

            modelBuilder.Entity<Feedback>().Property(f => f.Type).HasConversion<int>();

            modelBuilder.Entity<Feedback>(entity =>
            {
                entity.ToTable("Feedback");

                entity.HasKey(f => f.Id);

                entity.Property(f => f.StudentId)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(f => f.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(f => f.Description)
                    .IsRequired();

                entity.Property(f => f.Type)
                    .HasConversion<int>()
                    .IsRequired();

                entity.Property(f => f.IsAnonymous)
                    .HasDefaultValue(false);

                entity.Property(f => f.IsRead)
                    .HasDefaultValue(false);

                entity.Property(f => f.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(f => f.RepliedByAdminId)
                    .HasMaxLength(64);
            });

            modelBuilder.Entity<FeedbackImage>(entity =>
            {
                entity.ToTable("FeedbackImages");
                entity.HasKey(fi => fi.Id);
                entity.Property(fi => fi.ImagePath)
                    .IsRequired();
                
                entity.HasOne(fi => fi.Feedback)
                    .WithMany(f => f.Images)
                    .HasForeignKey(fi => fi.FeedbackId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }



    }
}
