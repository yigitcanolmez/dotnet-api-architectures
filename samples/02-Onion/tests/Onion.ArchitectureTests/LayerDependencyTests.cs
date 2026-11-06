using NetArchTest.Rules;
using Onion.Application.Orders;
using Onion.Domain.Entities;
using Onion.Infrastructure.Persistence;

namespace Onion.ArchitectureTests;

// Onion'un temel kuralı (bağımlılıklar merkeze doğru) derleme zamanında değil, bu testlerle de korunur.
// Biri yanlışlıkla Domain'e EF Core paketi eklerse veya Application'dan Infrastructure'a referans verirse test kırılır.
public class LayerDependencyTests
{
    private const string DomainNs = "Onion.Domain";
    private const string ApplicationNs = "Onion.Application";
    private const string InfrastructureNs = "Onion.Infrastructure";
    private const string ApiNs = "Onion.Api";

    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Order).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(OrderAppService).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer_or_EF_Core()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_should_not_reference_any_NuGet_or_project_assembly()
    {
        // Domain sadece .NET temel kütüphanelerini (System.*) referans almalı.
        var references = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System") && n != "netstandard" && n != "mscorlib")
            .ToList();

        Assert.Empty(references);
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_Api_or_EF_Core()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Repository_interfaces_should_live_in_Domain_and_implementations_in_Infrastructure()
    {
        var interfaces = Types.InAssembly(DomainAssembly)
            .That().HaveNameEndingWith("Repository").And().AreInterfaces()
            .GetTypes();
        Assert.NotEmpty(interfaces);

        var result = Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Repository")
            .Should().ResideInNamespace($"{InfrastructureNs}.Persistence.Repositories")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Kural ihlal eden tipler: " + string.Join(", ", result.FailingTypeNames ?? []);
}
