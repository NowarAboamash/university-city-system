using Gateway;
using MMLib.SwaggerForOcelot;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

var ocelotConfigFile = builder.Environment.EnvironmentName switch
{
    "Docker" => "ocelot.docker.json",
    "Production" => "ocelot.Production.json",
    _ => "ocelot.json"
};
builder.Configuration.AddJsonFile(ocelotConfigFile, optional: false, reloadOnChange: true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOcelot(builder.Configuration)
    .AddSingletonDefinedAggregator<DashboardAggregator>();
builder.Services.AddSwaggerForOcelot(builder.Configuration);

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

app.UseCors("AllowAll");

app.UseSwaggerForOcelotUI(opt =>
{
    opt.PathToSwaggerGenerator = "/swagger/docs";
});

await app.UseOcelot();

app.Run();
