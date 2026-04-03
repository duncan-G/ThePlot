using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ThePlot.ArchitectureTests;

public class DataAccessArchitectureTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Database.Abstractions.IQuery<>).Assembly,
            typeof(Database.Repository<,>).Assembly,
            typeof(Core.Screenplays.Screenplay).Assembly,
            typeof(Infrastructure.DatabaseExtensions).Assembly,
            typeof(Workers.ContentGeneration.VoiceDeterminationService).Assembly,
            typeof(Grpc.Server.Program).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly("ThePlot.Infrastructure").As("Infrastructure Layer");

    private static readonly IObjectProvider<IType> DatabaseLayer =
        Types().That().ResideInAssembly("Database").As("Database Layer");

    private static readonly IObjectProvider<IType> WorkersLayer =
        Types().That().ResideInAssembly("ThePlot.Workers.ContentGeneration").As("Workers Layer");

    private static readonly IObjectProvider<IType> GrpcLayer =
        Types().That().ResideInAssembly("ThePlot.Grpc.Server").As("gRPC Server Layer");

    [Fact]
    public void AsQueryable_ShouldOnlyBeCalledFromDatabaseAndInfrastructure()
    {
        IArchRule rule = Types().That()
            .Are(WorkersLayer).Or().Are(GrpcLayer)
            .Should().NotDependOnAny(
                Types().That().HaveFullNameContaining("IExecutableQuery"));

        rule.Check(Architecture);
    }

    [Fact]
    public void WorkersShouldNotDependOnThePlotContext()
    {
        IArchRule rule = Types().That().Are(WorkersLayer)
            .Should().NotDependOnAny(
                Types().That().HaveFullNameContaining("ThePlotContext"));

        rule.Check(Architecture);
    }

    [Fact]
    public void GrpcServerShouldNotDependOnThePlotContext()
    {
        IArchRule rule = Types().That().Are(GrpcLayer)
            .Should().NotDependOnAny(
                Types().That().HaveFullNameContaining("ThePlotContext"));

        rule.Check(Architecture);
    }

    [Fact]
    public void WorkersShouldNotDependOnEntityFrameworkDirectly()
    {
        IArchRule rule = Types().That().Are(WorkersLayer)
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore"));

        rule.Check(Architecture);
    }

    [Fact]
    public void GrpcServerShouldNotDependOnEntityFrameworkDirectly()
    {
        IArchRule rule = Types().That().Are(GrpcLayer)
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore"));

        rule.Check(Architecture);
    }
}
