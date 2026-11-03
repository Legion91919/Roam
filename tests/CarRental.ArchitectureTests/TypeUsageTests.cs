using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Domain.Reservations;
using CarRental.Infrastructure.Persistence;

namespace CarRental.ArchitectureTests;

/// <summary>
/// Finer-grained than assembly references: which TYPES each layer's compiled code uses, read from the
/// assembly metadata. Catches a stray HttpContext in a handler or a File.WriteAllBytes in a use case,
/// which an assembly-level check misses (System.IO lives in the base class library everyone references).
/// </summary>
public class TypeUsageTests
{
    private static readonly Assembly Domain = typeof(Reservation).Assembly;
    private static readonly Assembly Application = typeof(ICommand<>).Assembly;
    private static readonly Assembly Infrastructure = typeof(CarRentalDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    /// <summary>Full names ("System.IO.File") of every type the assembly's IL references.</summary>
    private static HashSet<string> TypesUsedBy(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(type => $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}")
            .ToHashSet();
    }

    private static readonly string[] FileSystemTypes =
        ["System.IO.File", "System.IO.Directory", "System.IO.FileStream", "System.IO.FileInfo", "System.IO.DirectoryInfo", "System.IO.DriveInfo"];

    public static TheoryData<string> LayersOutsideInfrastructure() => new() { "Domain", "Application", "Api" };
    public static TheoryData<string> LayersOutsideApi() => new() { "Domain", "Application", "Infrastructure" };

    private static Assembly Layer(string name) => name switch
    {
        "Domain" => Domain,
        "Application" => Application,
        "Infrastructure" => Infrastructure,
        _ => Api,
    };

    [Theory]
    [MemberData(nameof(LayersOutsideInfrastructure))]
    public void Only_infrastructure_touches_the_file_system(string layer)
    {
        var used = TypesUsedBy(Layer(layer));

        Assert.Empty(FileSystemTypes.Where(used.Contains));
    }

    [Theory]
    [MemberData(nameof(LayersOutsideInfrastructure))]
    public void Only_infrastructure_uses_cloud_sdks(string layer)
    {
        var used = TypesUsedBy(Layer(layer));

        Assert.Empty(used.Where(t => t.StartsWith("Azure.", StringComparison.Ordinal) || t.StartsWith("Amazon.", StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(LayersOutsideApi))]
    public void Only_the_api_reads_the_http_context(string layer)
    {
        var used = TypesUsedBy(Layer(layer));

        Assert.Empty(used.Where(t => t is "Microsoft.AspNetCore.Http.HttpContext" or "Microsoft.AspNetCore.Http.IHttpContextAccessor"
            or "Microsoft.AspNetCore.Http.HttpRequest" or "Microsoft.AspNetCore.Http.IFormFile"));
    }

    [Fact]
    public void Use_cases_never_read_the_system_clock_directly()
    {
        // TimeProvider (injected) is the only clock. DateTime.Now/UtcNow in a handler makes rules untestable.
        var offenders = new[] { Domain, Application }.SelectMany(assembly =>
        {
            using var stream = File.OpenRead(assembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            return metadata.MemberReferences
                .Select(h => metadata.GetMemberReference(h))
                .Where(m => m.Parent.Kind == HandleKind.TypeReference)
                .Select(m => (Type: metadata.GetTypeReference((TypeReferenceHandle)m.Parent), Name: metadata.GetString(m.Name)))
                .Where(m => metadata.GetString(m.Type.Namespace) == "System"
                            && metadata.GetString(m.Type.Name) is "DateTime" or "DateTimeOffset"
                            && m.Name is "get_Now" or "get_UtcNow" or "get_Today")
                .Select(m => $"{assembly.GetName().Name}: {metadata.GetString(m.Type.Name)}.{m.Name}")
                .ToList();
        }).ToList();

        Assert.Empty(offenders);
    }
}
