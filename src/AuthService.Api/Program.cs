using Microsoft.EntityFrameworkCore;
using AuthService.Api.Startup;
using AuthService.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/authService.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting AuthService API...");
    
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();

    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

    // Add CORS policy
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:5173") // Vite dev server
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
    
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddApiAuthenticationSettings(builder.Configuration);
    builder.Services.AddEndpointPolicies(builder.Configuration);
    
    builder.Services.AddInfrastructureServices();
    builder.Services.AddApplicationServices();
    builder.Services.AddApplicationHandlers();
    
    // Swagger Stuff
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddSwaggerSettings(builder.Configuration);
    }
    
    // Build the Wapp
    WebApplication app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseCors("AllowFrontend");
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization(); 
    app.UseRateLimiter();
    
    // Map our endpoints here
    app.MapApiEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AuthService API failed to start");
}
finally
{
    Log.CloseAndFlush();
}
