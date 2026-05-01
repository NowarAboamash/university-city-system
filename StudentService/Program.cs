using Microsoft.EntityFrameworkCore;
using StudentService.Data;
using StudentService.Interfaces;
using StudentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IStudentService, StudentService.Services.StudentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!context.Colleges.Any())
    {
        context.Colleges.AddRange(
            new StudentService.Models.College { Name = "Engineering" },
            new StudentService.Models.College { Name = "Medicine" },
            new StudentService.Models.College { Name = "Information Technology" },
            new StudentService.Models.College { Name = "Business" });

        context.SaveChanges();
    }

    if (!context.Governorates.Any())
    {
        context.Governorates.AddRange(
            new StudentService.Models.Governorate { Name = "Damascus" },
            new StudentService.Models.Governorate { Name = "Rif Dimashq" },
            new StudentService.Models.Governorate { Name = "Aleppo" },
            new StudentService.Models.Governorate { Name = "Homs" },
            new StudentService.Models.Governorate { Name = "Hama" },
            new StudentService.Models.Governorate { Name = "Latakia" },
            new StudentService.Models.Governorate { Name = "Tartus" },
            new StudentService.Models.Governorate { Name = "Idlib" },
            new StudentService.Models.Governorate { Name = "Deir ez-Zor" },
            new StudentService.Models.Governorate { Name = "Raqqa" },
            new StudentService.Models.Governorate { Name = "Al-Hasakah" },
            new StudentService.Models.Governorate { Name = "Daraa" },
            new StudentService.Models.Governorate { Name = "As-Suwayda" },
            new StudentService.Models.Governorate { Name = "Quneitra" });

        context.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
