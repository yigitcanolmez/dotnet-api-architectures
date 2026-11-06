using System.Reflection;
using System.Runtime.CompilerServices;
using ModularMonolith.Modules.Catalog;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Ordering;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Shipping;
using ModularMonolith.Modules.Shipping.Contracts;
using ModularMonolith.Shared.Abstractions.Events;
using ModularMonolith.Shared.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace ModularMonolith.ArchitectureTests;

// [Architecture Tests] Modül sınırlarını derleyici + test seviyesinde zorlar.
// "internal" anahtar kelimesi iç sınıfları gizler; bu testler de PROJE REFERANSLARININ ve
// public yüzeyin yanlışlıkla genişlemesini yakalar.
public class ModuleBoundaryTests
{
    private static readonly Assembly Catalog = typeof(CatalogModule).Assembly;
    private static readonly Assembly Ordering = typeof(OrderingModule).Assembly;
    private static readonly Assembly Shipping = typeof(ShippingModule).Assembly;

    private static readonly Assembly CatalogContracts = typeof(ICatalogModuleApi).Assembly;
    private static readonly Assembly OrderingContracts = typeof(OrderPlacedIntegrationEvent).Assembly;
    private static readonly Assembly ShippingContracts = typeof(ShipmentCreatedIntegrationEvent).Assembly;

    private static readonly Assembly[] ModuleImplementations = [Catalog, Ordering, Shipping];

    public static TheoryData<string> ModuleNames => new() { "Catalog", "Ordering", "Shipping" };

    private static Assembly ModuleByName(string name) => ModuleImplementations.Single(a => a.GetName().Name!.EndsWith(name));

    // Bir assembly'deki tüm (internal dahil) üst seviye tiplerin tam adları — NetArchTest'e "bunlara bağımlı olma" diye verilir.
    // (Namespace yerine tip adı kullanıyoruz; çünkü "ModularMonolith.Modules.Catalog" öneki Contracts'ı da kapsardı.)
    private static string[] TypeNamesOf(Assembly assembly) => assembly.GetTypes()
        .Where(t => !t.IsNested && t.GetCustomAttribute<CompilerGeneratedAttribute>() is null && t.Namespace is not null)
        .Select(t => t.FullName!)
        .ToArray();

    [Fact]
    public void Ordering_should_not_depend_on_Catalog_or_Shipping_implementations()
    {
        var forbidden = TypeNamesOf(Catalog).Concat(TypeNamesOf(Shipping)).ToArray();

        var result = Types.InAssembly(Ordering)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));

        var references = Ordering.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain(Catalog.GetName().Name, references);
        Assert.DoesNotContain(Shipping.GetName().Name, references);
        Assert.Contains(CatalogContracts.GetName().Name, references); // sadece sözleşmeye bağlı
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_should_not_depend_on_other_module_implementations(string moduleName)
    {
        var module = ModuleByName(moduleName);
        var others = ModuleImplementations.Where(a => a != module).SelectMany(TypeNamesOf).ToArray();

        var result = Types.InAssembly(module)
            .ShouldNot().HaveDependencyOnAny(others)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_public_surface_should_be_only_the_module_entry_point(string moduleName)
    {
        var module = ModuleByName(moduleName);

        // GetExportedTypes: assembly dışından GERÇEKTEN görülebilen tipler (internal sınıf içindeki
        // "public" nested tipler dışarıdan görünmez, bu yüzden sayılmaz).
        var publicTypes = module.GetExportedTypes().Select(t => t.Name).ToList();

        Assert.Equal([$"{moduleName}Module"], publicTypes);
        Assert.True(module.GetType($"ModularMonolith.Modules.{moduleName}.{moduleName}Module")!.IsAbstract
                    && module.GetType($"ModularMonolith.Modules.{moduleName}.{moduleName}Module")!.IsSealed,
            "Modül giriş noktası static (extension) sınıf olmalı.");
    }

    [Fact]
    public void Contracts_should_only_contain_interfaces_records_and_integration_events_and_not_depend_on_implementations()
    {
        var implementationTypes = ModuleImplementations.SelectMany(TypeNamesOf).ToArray();

        foreach (var contracts in new[] { CatalogContracts, OrderingContracts, ShippingContracts })
        {
            var result = Types.InAssembly(contracts)
                .ShouldNot().HaveDependencyOnAny([.. implementationTypes, "Microsoft.EntityFrameworkCore"])
                .GetResult();
            Assert.True(result.IsSuccessful, Describe(result));

            Assert.All(contracts.GetExportedTypes(), t => Assert.True(
                t.IsInterface || typeof(IIntegrationEvent).IsAssignableFrom(t) || t.GetMethod("<Clone>$") is not null,
                $"{t.FullName} bir sözleşme tipi değil (interface / record / integration event bekleniyordu)."));
        }
    }

    [Fact]
    public void Shared_should_not_depend_on_any_module()
    {
        var result = Types.InAssembly(typeof(ModuleDbContext).Assembly)
            .ShouldNot().HaveDependencyOn("ModularMonolith.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Sanity_check_bootstrapper_does_depend_on_modules()
    {
        // Negatif kontrol: kural gerçekten ihlali yakalayabiliyor mu? Bootstrapper modülleri bilmek ZORUNDA.
        var result = Types.InAssembly(typeof(Program).Assembly)
            .ShouldNot().HaveDependencyOnAny(TypeNamesOf(Catalog))
            .GetResult();

        Assert.False(result.IsSuccessful);
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful ? "" : "İhlal eden tipler: " + string.Join(", ", result.FailingTypeNames ?? []);
}
