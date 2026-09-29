using System.Reflection;
using Akiron.BuildingBlocks.Modules;
using Akiron.Modules.Identity;
using NetArchTest.Rules;

namespace Akiron.Tests.Architecture;

/// <summary>
/// The rules in AGENTS.md and ADR-0001 that a reviewer would otherwise have to remember. Module
/// references between projects are also blocked by the compiler; these tests cover what the
/// compiler cannot see.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly BuildingBlocks = typeof(IModule).Assembly;

    /// <summary>Every module assembly. Add each new module here when it is created.</summary>
    private static readonly Assembly[] Modules = [typeof(IdentityModule).Assembly];

    private static readonly Assembly[] All = [BuildingBlocks, .. Modules, typeof(Program).Assembly];

    public static TheoryData<string> ForbiddenNames => ["Services", "Repositories", "Managers", "Helpers", "Utils", "Dtos"];

    [Theory]
    [MemberData(nameof(ForbiddenNames))]
    public void Namespaces_DoNotUseCatchAllFolderNames(string forbidden)
    {
        var offenders = All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.Split('.').Contains(forbidden, StringComparer.Ordinal) == true)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, $"Name folders after what they do, not '{forbidden}': {string.Join(", ", offenders)}");
    }

    [Fact]
    public void BuildingBlocks_DoesNotDependOnAnyModule()
    {
        var result = Types.InAssembly(BuildingBlocks)
            .ShouldNot()
            .HaveDependencyOnAny("Akiron.Modules", "Akiron.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Modules_DoNotDependOnEachOther()
    {
        foreach (var module in Modules)
        {
            var own = module.GetName().Name!;
            var others = Modules.Select(other => other.GetName().Name!).Where(name => name != own).ToArray();
            if (others.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(module).ShouldNot().HaveDependencyOnAny(others).GetResult();
            Assert.True(result.IsSuccessful, Failing(result));
        }
    }

    [Fact]
    public void ModuleDomain_DoesNotDependOnPersistenceOrWeb()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(module)
                .That().ResideInNamespaceEndingWith(".Domain")
                .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
                .GetResult();

            Assert.True(result.IsSuccessful, Failing(result));
        }
    }

    [Fact]
    public void Handlers_AreSealed()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(module)
                .That().HaveNameEndingWith("Handler").And().AreClasses()
                .Should().BeSealed()
                .GetResult();

            Assert.True(result.IsSuccessful, Failing(result));
        }
    }

    [Fact]
    public void ModuleEntities_HavePrivateSetters()
    {
        var offenders = Modules
            .SelectMany(module => module.GetTypes())
            .Where(type => type.Namespace?.EndsWith(".Domain", StringComparison.Ordinal) == true && type.IsClass)
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, $"Change state through methods, not public setters: {string.Join(", ", offenders)}");
    }

    private static string Failing(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
