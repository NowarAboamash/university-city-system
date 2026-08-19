using FeedbackService.Data;
using FeedbackService.Interfaces;
using FeedbackService.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Auth;
using SharedKernel.Media;
using SharedKernel.Users;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<FeedbackDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(60)));
builder.Services.AddScoped<IFeedbackService, FeedbackService.Services.FeedbackService>();
builder.Services.AddScoped<IFeedbackImageService, FeedbackImageService>();
builder.Services.AddScoped<IFileHandler, FileHandler>();
builder.Services.AddCloudinaryImageUploader();
builder.Services.AddAuthServiceUserLookup(builder.Configuration);
builder.Services.AddSharedJwtAuthentication(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.AddJwtBearerSecurity());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();
// Apply migrations (if using EF Core) � recommended before seeding
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FeedbackDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
