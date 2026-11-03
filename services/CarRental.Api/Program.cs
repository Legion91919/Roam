using System.Text.Json.Serialization;
using CarRental.Api.Infrastructure;
using CarRental.Application;
using CarRental.Application.Abstractions.Identity;
using CarRental.Infrastructure;
using CarRental.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;

// =============================================================================================
// COMPOSITION ROOT
// Every dependency in the system is chosen here (and in each layer's AddXxx extension method).
// Swapping SQL for Mongo read models, the system clock for a fake one, or ASP.NET Core Identity
// for Entra ID, is a change in this wiring, never in a handler or an aggregate.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Development demo aid only: shift "now" to show late returns without waiting (see OffsetTimeProvider).
if (builder.Environment.IsDevelopment() && TimeSpan.TryParse(builder.Configuration["DemoClock:Offset"], System.Globalization.CultureInfo.InvariantCulture, out var demoOffset))
    builder.Services.AddSingleton<TimeProvider>(new OffsetTimeProvider(demoOffset));

// Who is calling: HTTP claims -> the Application's ICurrentUser port.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Errors -> RFC 7807 ProblemDetails with a stable machine-readable "code" (see ApiExceptionHandler).
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Car Rental API", Version = "v1", Description = "Clean Architecture + CQRS-lite proof of concept" });

    // "Authorize" button in Swagger UI: paste the accessToken from POST /api/auth/login.
    var bearer = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Access token from POST /api/auth/login",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });
});

builder.Services.AddCors(options => options.AddPolicy("client", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>("Database:InitialiseOnStartup"))
    await DatabaseInitializer.InitialiseAsync(app.Services);

app.UseCors("client");
app.UseAuthentication();
app.UseAuthorization();

// POST /api/auth/register, /api/auth/login (returns a bearer token), /api/auth/refresh, ...
app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>().WithTags("Auth");
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed so integration tests can host the API with WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program { }
