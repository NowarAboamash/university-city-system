using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Interfaces;
using NotificationService.Services;
using SharedKernel.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(60)));
builder.Services.AddScoped<INotificationService, NotificationsService>();
builder.Services.AddSingleton<IPushNotificationSender, FirebasePushNotificationSender>();

var authServiceBaseUrl = Environment.GetEnvironmentVariable("AUTH_SERVICE_BASE_URL")
    ?? builder.Configuration["AuthService:BaseUrl"];
if (string.IsNullOrWhiteSpace(authServiceBaseUrl))
{
    throw new InvalidOperationException(
        "AuthService base URL is not configured. Set the 'AUTH_SERVICE_BASE_URL' " +
        "environment variable or 'AuthService:BaseUrl' in configuration.");
}

var authServiceApiKey = Environment.GetEnvironmentVariable("AUTH_SERVICE_INTERNAL_API_KEY")
    ?? builder.Configuration["AuthService:InternalApiKey"];
if (string.IsNullOrWhiteSpace(authServiceApiKey))
{
    throw new InvalidOperationException(
        "AuthService internal API key is not configured. Set the 'AUTH_SERVICE_INTERNAL_API_KEY' " +
        "environment variable or 'AuthService:InternalApiKey' in configuration.");
}

builder.Services.AddHttpClient<IAuthServiceClient, AuthServiceClient>(client =>
{
    client.BaseAddress = new Uri(authServiceBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", authServiceApiKey);
    client.Timeout = TimeSpan.FromSeconds(10);
});
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
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
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
