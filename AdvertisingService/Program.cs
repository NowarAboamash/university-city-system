using AdvertisingService.Data;
using AdvertisingService.Interfaces;
using AdvertisingService.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Auth;
using SharedKernel.Media;
using SharedKernel.Notifications;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AdvertisingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(60)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.AddCloudinaryImageUploader();
builder.Services.AddScoped<IAdvertisementService, AdvertisementService>();
builder.Services.AddSharedNotificationPublisher(builder.Configuration, "AdvertisingService");
builder.Services.AddHostedService<ExpiredAdvertisementCleanupService>();
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AdvertisingDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
