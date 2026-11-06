using System.Reflection;
using Clean.Domain.Common;
using FluentValidation;
using MediatR;
using NetArchTest.Rules;
using static Clean.ArchitectureTests.LayerDependencyTests;

namespace Clean.ArchitectureTests;

// İsimlendirme ve tasarım konvansiyonları: ekip büyüdükçe yapının "erimesini" engeller.
public class ConventionTests
{
    private static void AssertSuccess(TestResult result) =>
        Assert.True(result.IsSuccessful,
            "Kuralı ihlal eden tipler: " + string.Join(", ", result.FailingTypeNames ?? []));

    private static bool Implements(Type type, Type openGeneric) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

    [Fact]
    public void RequestHandlers_ShouldEndWithHandler_AndBeSealed()
    {
        var handlers = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => Implements(t, typeof(IRequestHandler<,>)) || Implements(t, typeof(IRequestHandler<>)))
            .ToList();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, h =>
        {
            Assert.EndsWith("Handler", h.Name);
            Assert.True(h.IsSealed, $"{h.Name} sealed olmalı");
        });
    }

    [Fact]
    public void NotificationHandlers_ShouldEndWithHandler_AndBeSealed_AndLiveInEventHandlers()
    {
        var handlers = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && Implements(t, typeof(INotificationHandler<>)))
            .ToList();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, h =>
        {
            Assert.EndsWith("Handler", h.Name);
            Assert.True(h.IsSealed, $"{h.Name} sealed olmalı");
            Assert.EndsWith(".EventHandlers", h.Namespace);
        });
    }

    [Fact]
    public void Validators_ShouldEndWithValidator_AndBeSealed()
    {
        var validators = Types.InAssembly(ApplicationAssembly).That().Inherit(typeof(AbstractValidator<>));
        Assert.NotEmpty(validators.GetTypes()); // kural boş kümeye uygulanıp "yanlışlıkla geçmesin"

        AssertSuccess(validators.Should().HaveNameEndingWith("Validator").And().BeSealed().GetResult());
    }

    [Fact]
    public void Behaviours_ShouldLiveInCommonBehaviours() =>
        AssertSuccess(Types.InAssembly(ApplicationAssembly).That().HaveNameEndingWith("Behaviour")
            .Should().ResideInNamespace("Clean.Application.Common.Behaviours")
            .GetResult());

    [Fact]
    public void DomainEvents_ShouldBeSealed_AndLiveInEventsNamespace()
    {
        var events = Types.InAssembly(DomainAssembly).That().ImplementInterface(typeof(IDomainEvent));
        Assert.NotEmpty(events.GetTypes());

        AssertSuccess(events.Should().BeSealed().And().ResideInNamespaceEndingWith(".Events").GetResult());
    }

    [Fact]
    public void DomainEntities_ShouldNotHavePublicSetters()
    {
        // Durum yalnızca niyet bildiren metotlarla (Ship, Cancel...) değişebilir.
        var offenders = DomainAssembly.GetTypes()
            .Where(t => t.BaseType is { IsGenericType: true } && IsEntity(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void StronglyTypedIds_ShouldBeReadonlyRecordStructs()
    {
        var ids = DomainAssembly.GetTypes().Where(t => t.Name.EndsWith("Id") && t.IsValueType).ToList();

        Assert.NotEmpty(ids);
        Assert.All(ids, t => Assert.True(t.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute)),
            $"{t.Name} readonly olmalı"));
    }

    private static bool IsEntity(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Entity<>))
                return true;
        return false;
    }
}
