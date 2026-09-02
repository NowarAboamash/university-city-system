using HousingService.Data;
using HousingService.Data.Repositories;
using HousingService.External;
using HousingService.Interfaces;
using HousingService.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Auth;
using SharedKernel.Media;
using SharedKernel.Notifications;
using SharedKernel.Users;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<HousingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(60)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IHousingCycleRepository, HousingCycleRepository>();
builder.Services.AddScoped<IHousingCycleService, HousingCycleService>();
builder.Services.AddScoped<IGovernorateRepository, GovernorateRepository>();
builder.Services.AddScoped<IGovernorateService, GovernorateService>();
builder.Services.AddScoped<IHousingRequestRepository, HousingRequestRepository>();
builder.Services.AddScoped<IHousingRequestDocumentRepository, HousingRequestDocumentRepository>();
builder.Services.AddScoped<IHousingRequestService, HousingRequestService>();
builder.Services.AddScoped<IHousingGroupRepository, HousingGroupRepository>();
builder.Services.AddScoped<IGroupInvitationRepository, GroupInvitationRepository>();
builder.Services.AddScoped<IHousingGroupService, HousingGroupService>();
builder.Services.AddScoped<IAllocationRepository, AllocationRepository>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<IBuildingEvacuationService, BuildingEvacuationService>();
builder.Services.AddScoped<IHousingSettingsRepository, HousingSettingsRepository>();
builder.Services.AddScoped<IHousingSettingsService, HousingSettingsService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPaymentReminderService, PaymentReminderService>();
builder.Services.AddScoped<IUnpaidEvictionService, UnpaidEvictionService>();
builder.Services.AddHostedService<PaymentReminderJob>();
builder.Services.AddCloudinaryImageUploader();
builder.Services.AddHousingWalletClient(builder.Configuration);
builder.Services.AddSharedNotificationPublisher(builder.Configuration, "HousingService");
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
