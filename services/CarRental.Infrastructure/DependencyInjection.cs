using CarRental.Application.Abstractions.Files;
using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Events;
using CarRental.Domain.Reservations;
using CarRental.Application.Abstractions.Terms;
using CarRental.Infrastructure.Files;
using CarRental.Infrastructure.Identity;
using CarRental.Infrastructure.Payments;
using CarRental.Infrastructure.Persistence;
using CarRental.Infrastructure.Persistence.Repositories;
using CarRental.Infrastructure.ReadModels.Mongo;
using CarRental.Infrastructure.ReadModels.Sql;
using CarRental.Infrastructure.Terms;
using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace CarRental.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Binds every Application port to a concrete adapter. This is the ONLY place that knows which
    /// database technology implements which port. Change the config, not the code.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlConnection = configuration.GetConnectionString("CarRental")
            ?? throw new InvalidOperationException("Connection string 'CarRental' is missing.");

        services.AddDbContext<CarRentalDbContext>(options =>
            options.UseSqlServer(sqlConnection, sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        // ---- Sign-in: ASP.NET Core Identity (hashed passwords, bearer tokens), own schema ----
        services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(sqlConnection));
        services.AddIdentityApiEndpoints<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AuthDbContext>();

        services.AddScoped<IUserAccounts, IdentityUserAccounts>();
        services.AddSingleton<ITermsProvider, StaticTermsProvider>();

        // ---- Write side (commands) ----
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IRentalRepository, RentalRepository>();
        services.AddScoped<ICategoryInventoryRepository, CategoryInventoryRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IInspectionRepository, InspectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        AddPaymentGateway(services, configuration);
        AddFileStorage(services, configuration);

        // ---- Read side (queries) ----
        services.AddScoped<IReservationReadModel, SqlReservationReadModel>();
        services.AddScoped<IBranchReadModel, SqlBranchReadModel>();
        services.AddScoped<ICustomerReadModel, SqlCustomerReadModel>();
        services.AddScoped<ICounterReadModel, SqlCounterReadModel>();
        services.AddScoped<IFleetReadModel, SqlFleetReadModel>();
        services.AddScoped<IInspectionReadModel, SqlInspectionReadModel>();

        var provider = configuration["ReadModels:Provider"] ?? "Sql";
        if (string.Equals(provider, "Mongo", StringComparison.OrdinalIgnoreCase))
            AddMongoReadModel(services, configuration);
        else
            services.AddScoped<IAvailabilityReadModel, SqlAvailabilityReadModel>();

        return services;
    }

    /// <summary>
    /// Card payments. Only "Mock" exists. A real provider is one more adapter implementing
    /// IPaymentGateway (with its API keys from Key Vault) and one more case here: no use case changes.
    /// </summary>
    private static void AddPaymentGateway(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Payments:Provider"] ?? "Mock";
        if (!string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unknown Payments:Provider '{provider}'. Supported: Mock.");

        // Singleton: the mock remembers idempotency keys for the life of the process, like a gateway would.
        services.AddSingleton<MockPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());
    }

    /// <summary>
    /// Uploaded files. Local: a folder outside any web root (Files:LocalRoot, default "App_Data/uploads"
    /// under the content root). AzureBlob: documented placeholder for production.
    /// </summary>
    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Files:Provider"] ?? "Local";
        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
            return;
        }
        if (!string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unknown Files:Provider '{provider}'. Supported: Local, AzureBlob.");

        var root = configuration["Files:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
        services.AddSingleton<IFileStorage>(new LocalDiskFileStorage(root));
    }

    private static void AddMongoReadModel(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Mongo")
            ?? throw new InvalidOperationException("Connection string 'Mongo' is missing (ReadModels:Provider is Mongo).");
        var database = configuration["Mongo:Database"] ?? "carrental";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connection));   // thread-safe, one per app
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>()
            .GetDatabase(database)
            .GetCollection<AvailabilityDocument>("availability"));

        services.AddScoped<IAvailabilityReadModel, MongoAvailabilityReadModel>();
        services.AddScoped<AvailabilityProjectionRebuilder>();

        // Subscribe the projection to domain events (registered explicitly: it lives outside the Application assembly).
        services.AddTransient<INotificationHandler<DomainEventNotification<ReservationCreated>>, MongoAvailabilityProjection>();
        services.AddTransient<INotificationHandler<DomainEventNotification<ReservationCancelled>>, MongoAvailabilityProjection>();
    }
}
