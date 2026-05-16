using AdvertisingService.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvertisingService.Data;

public class AdvertisingDbContext : DbContext
{
    public AdvertisingDbContext(DbContextOptions<AdvertisingDbContext> options) : base(options)
    {
    }

    public DbSet<Advertisement> Advertisements => Set<Advertisement>();

    public DbSet<AdvertisementCollege> AdvertisementColleges => Set<AdvertisementCollege>();

    public DbSet<AdvertisementGovernorate> AdvertisementGovernorates => Set<AdvertisementGovernorate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Advertisement>(entity =>
        {
            entity.HasKey(ad => ad.Id);
            entity.Property(ad => ad.Title).IsRequired().HasMaxLength(200);
            entity.HasIndex(ad => ad.IsActive);
            entity.HasIndex(ad => ad.StartDate);
            entity.HasIndex(ad => ad.EndDate);
        });

        modelBuilder.Entity<AdvertisementCollege>(entity =>
        {
            entity.HasKey(ac => new { ac.AdvertisementId, ac.CollegeId });
            entity.HasOne(ac => ac.Advertisement)
                .WithMany(ad => ad.AdvertisementColleges)
                .HasForeignKey(ac => ac.AdvertisementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdvertisementGovernorate>(entity =>
        {
            entity.HasKey(ag => new { ag.AdvertisementId, ag.GovernorateId });
            entity.HasOne(ag => ag.Advertisement)
                .WithMany(ad => ad.AdvertisementGovernorates)
                .HasForeignKey(ag => ag.AdvertisementId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
