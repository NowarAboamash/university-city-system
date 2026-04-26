using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FeedbackService.Data
{
    public class FeedbackDbContextFactory : IDesignTimeDbContextFactory<FeedbackDbContext>
    {
        public FeedbackDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=FeedbackServiceDb;Trusted_Connection=True;TrustServerCertificate=True;";

            var optionsBuilder = new DbContextOptionsBuilder<FeedbackDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new FeedbackDbContext(optionsBuilder.Options);
        }
    }
}
