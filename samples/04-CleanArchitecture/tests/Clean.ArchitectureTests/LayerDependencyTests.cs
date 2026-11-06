using System.Reflection;
using NetArchTest.Rules;

namespace Clean.ArchitectureTests;

// "The Dependency Rule"un otomatik bekçisi: kaynak kod bağımlılıkları sadece İÇERİ doğru olabilir.
// Biri Domain'e EF Core ekler ya da Application'dan Infrastructure'a referans verirse bu testler kırılır.
public class LayerDependencyTests
{
    internal static readonly Assembly DomainAssembly = typeof(Clean.Domain.Orders.Order).Assembly;
    internal static readonly Assembly ApplicationAssembly = typeof(Clean.Application.DependencyInjection).Assembly;
    internal static readonly Assembly InfrastructureAssembly = typeof(Clean.Infrastructure.DependencyInjection).Assembly;
    internal static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    private const string DomainNs = "Clean.Domain";
    private const string ApplicationNs = "Clean.Application";
    private const string InfrastructureNs = "Clean.Infrastructure";
    private const string ApiNs = "Clean.Api";

    private static void AssertSuccess(TestResult result) =>
        Assert.True(result.IsSuccessful,
            "Kuralı ihlal eden tipler: " + string.Join(", ", result.FailingTypeNames ?? []));

    [Fact]
    public void Domain_ShouldNotDependOn_AnyOtherLayerOrFramework() =>
        AssertSuccess(Types.InAssembly(DomainAssembly).ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs,
                "MediatR", "FluentValidation", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult());

    [Fact]
    public void Domain_ShouldNotReference_AnyNuGetPackage()
    {
        var references = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System") && n != "netstandard" && n != "mscorlib")
            .ToList();

        Assert.Empty(references);
    }

    [Fact]
    public void Application_ShouldNotDependOn_InfrastructureOrApi() =>
        AssertSuccess(Types.InAssembly(ApplicationAssembly).ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs, "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore.Sqlite")
            .GetResult());

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api() =>
        AssertSuccess(Types.InAssembly(InfrastructureAssembly).ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult());

    [Fact]
    public void ApiEndpoints_ShouldNotDependOn_InfrastructureOrEfCore() =>
        // Endpoint'ler sadece ISender ve Application tiplerini bilir; Infrastructure sadece Program.cs'te (composition).
        AssertSuccess(Types.InAssembly(ApiAssembly).That().ResideInNamespace("Clean.Api.Endpoints")
            .ShouldNot().HaveDependencyOnAny(InfrastructureNs, "Microsoft.EntityFrameworkCore")
            .GetResult());
}
