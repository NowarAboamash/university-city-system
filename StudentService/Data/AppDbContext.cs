using Microsoft.EntityFrameworkCore;
using StudentService.Models;

namespace StudentService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<College> Colleges => Set<College>();
    public DbSet<Governorate> Governorates => Set<Governorate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasIndex(s => s.UserId).IsUnique();

            entity.Property(s => s.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(s => s.College)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Governorate)
                .WithMany()
                .HasForeignKey(s => s.GovernorateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<College>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
        });
    }
}
