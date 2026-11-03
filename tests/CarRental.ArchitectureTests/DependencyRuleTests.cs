using System.Reflection;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Domain.Reservations;
using CarRental.Infrastructure.Persistence;
using MediatR;

namespace CarRental.ArchitectureTests;

/// <summary>
/// Clean Architecture's DEPENDENCY RULE as executable tests: source code dependencies point inwards.
///   Api -> Infrastructure -> Application -> Domain
/// If someone adds "using Microsoft.EntityFrameworkCore" to the Domain, the build stays green but
/// this test goes red, and the pull request is blocked.
/// </summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Reservation).Assembly;
    private static readonly Assembly Application = typeof(ICommand<>).Assembly;
    private static readonly Assembly Infrastructure = typeof(CarRentalDbContext).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_class_library()
    {
        var offenders = References(Domain)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal) && name != "netstandard" && name != "mscorlib")
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("CarRental.Infrastructure")]
    [InlineData("CarRental.Api")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("MongoDB.Driver")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_does_not_depend_on_outer_layers_or_frameworks(string forbidden)
    {
        Assert.DoesNotContain(References(Application), name => name.StartsWith(forbidden, StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api()
    {
        Assert.DoesNotContain(References(Infrastructure), name => name == "CarRental.Api");
    }

    [Fact]
    public void Every_command_and_query_has_exactly_one_handler()
    {
        var requests = Application.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(ICommand<>) || i.GetGenericTypeDefinition() == typeof(IQuery<>))))
            .ToList();

        Assert.NotEmpty(requests);
        foreach (var request in requests)
        {
            var handlers = Application.GetTypes().Count(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) && i.GetGenericArguments()[0] == request));
            Assert.True(handlers == 1, $"{request.Name} has {handlers} handlers (SOLID S: one handler per use case).");
        }
    }

    [Fact]
    public void Aggregates_expose_no_public_setters()
    {
        var aggregates = Domain.GetTypes().Where(t => t.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition().Name.StartsWith("AggregateRoot", StringComparison.Ordinal));

        var publicSetters = aggregates
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.SetMethod is { IsPublic: true } && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.Empty(publicSetters);
    }
}
