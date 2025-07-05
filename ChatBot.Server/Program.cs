using ChatBot.Server.Services;
using ChatBot.Server.Settings;
using ChatBot.Server.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

// Add database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:62963")  // Frontend URL
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure settings
builder.Services.Configure<OpenRouterSettings>(
    builder.Configuration.GetSection("OpenRouter"));

// Add services
builder.Services.AddHttpClient<IPythonNlpService, PythonNlpService>();
builder.Services.AddHttpClient<ILLMService, LLMService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config.GetSection("OpenRouter:ApiKey").Value;
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:62963");
    client.DefaultRequestHeaders.Add("X-Title", "ERP Assistant");
});
builder.Services.AddScoped<ApplicationDbContext>();

// Modularized services registration
builder.Services.AddScoped<IChatModelService, ChatOrchestratorService>();
builder.Services.AddScoped<ISemanticMemoryService, SemanticMemoryService>();
builder.Services.AddScoped<IIntentService, IntentService>();
builder.Services.AddScoped<IChatHistoryRepository, ChatHistoryRepository>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddUrlGroup(new Uri("http://localhost:8000/health"), name: "PythonNlpService", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, tags: new[] { "external" });

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var error = context.Features.Get<IExceptionHandlerFeature>();
            if (error != null)
            {
                var ex = error.Error;
                await context.Response.WriteAsJsonAsync(new
                {
                    StatusCode = 500,
                    Message = "An error occurred while processing your request.",
                    DetailedMessage = app.Environment.IsDevelopment() ? ex.Message : null
                });
            }
        });
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

// Add health check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Attempting to create/connect to database...");
        var canConnect = await context.Database.CanConnectAsync();
        logger.LogInformation("Can connect to database: {CanConnect}", canConnect);
        
        if (!canConnect)
        {
            logger.LogError("Cannot connect to database. Please check your SQL Server connection and permissions.");
        }
        else
        {
            logger.LogInformation("Creating database if it doesn't exist...");
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Database creation completed.");
            
            // Verify ChatHistories table
            var tableExists = await context.Database.ExecuteSqlRawAsync(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ChatHistories'") > 0;
            logger.LogInformation("ChatHistories table exists: {TableExists}", tableExists);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating/connecting to the database: {ErrorMessage}", ex.Message);
    }
}

app.Run();
