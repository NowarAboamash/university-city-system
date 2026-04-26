using Microsoft.EntityFrameworkCore;
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

            modelBuilder.Entity<Feedback>().Property(f => f.Type).HasConversion<int>();

            modelBuilder.Entity<Feedback>(entity =>
            {
                entity.ToTable("Feedback");

                entity.HasKey(f => f.Id);

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
