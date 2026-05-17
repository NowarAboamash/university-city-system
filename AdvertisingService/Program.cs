using AdvertisingService.Data;
using AdvertisingService.Interfaces;
using AdvertisingService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "advertisements"));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AdvertisingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.AddScoped<IAdvertisementService, AdvertisementService>();
builder.Services.AddHostedService<ExpiredAdvertisementCleanupService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("AllowAll");


app.MapControllers();

app.Run();
