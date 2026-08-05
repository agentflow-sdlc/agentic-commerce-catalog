using System.Reflection;

namespace Catalog.Architecture.Tests;

public sealed class DependencyRulesTests
{
    private static readonly Assembly ContractsAssembly = typeof(Catalog.Contracts.HealthResponse).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;
    private static readonly Assembly ManagersAssembly = typeof(Catalog.Managers.AssemblyMarker).Assembly;
    private static readonly Assembly EnginesAssembly = typeof(Catalog.Engines.AssemblyMarker).Assembly;
    private static readonly Assembly AccessorsAssembly = typeof(Catalog.Accessors.AssemblyMarker).Assembly;

    private static readonly Assembly[] ProductionAssemblies =
    [
        ContractsAssembly,
        ApiAssembly,
        ManagersAssembly,
        EnginesAssembly,
        AccessorsAssembly,
    ];

    [Fact]
    public void ContractsDoNotDependOnOtherProductionProjects()
    {
        var references = GetReferenceNames(ContractsAssembly);

        Assert.DoesNotContain(references, reference => reference.StartsWith("Catalog.", StringComparison.Ordinal));
    }

    [Fact]
    public void EnginesRemainIndependentFromTechnologyAndAccessors()
    {
        var references = GetReferenceNames(EnginesAssembly);
        var forbiddenPrefixes = new[]
        {
            "Catalog.Api",
            "Catalog.Accessors",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Azure",
            "Microsoft.Azure",
        };

        foreach (var forbiddenPrefix in forbiddenPrefixes)
        {
            Assert.DoesNotContain(
                references,
                reference => reference.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ManagersDependOnDomainPortsButNotTechnicalFrameworks()
    {
        var references = GetReferenceNames(ManagersAssembly);

        Assert.Contains("Catalog.Engines", references);
        Assert.DoesNotContain("Catalog.Accessors", references);
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void ApiContainsNoProductOrCategoryBusinessNamespaces()
    {
        var forbiddenSegments = new[] { ".Domain", ".Products", ".Categories", ".BusinessRules" };
        var violations = ApiAssembly
            .GetTypes()
            .Where(type => forbiddenSegments.Any(segment =>
                type.Namespace?.Contains(segment, StringComparison.Ordinal) == true))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ApiUsesAccessorsOnlyAsACompositionDependency()
    {
        var references = GetReferenceNames(ApiAssembly);

        Assert.Contains("Catalog.Accessors", references);
        Assert.DoesNotContain("Catalog.Engines", references);
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }

    [Fact]
    public void AccessorsImplementDomainPortsWithoutDependingOnApiOrManagers()
    {
        var references = GetReferenceNames(AccessorsAssembly);

        Assert.Contains("Catalog.Engines", references);
        Assert.DoesNotContain("Catalog.Api", references);
        Assert.DoesNotContain("Catalog.Managers", references);
        Assert.DoesNotContain("Catalog.Contracts", references);
    }

    [Fact]
    public void ProductionProjectsDoNotDependOnTests()
    {
        foreach (var assembly in ProductionAssemblies)
        {
            Assert.DoesNotContain(
                GetReferenceNames(assembly),
                reference => reference.EndsWith(".Tests", StringComparison.Ordinal));
        }
    }

    private static string[] GetReferenceNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name ?? string.Empty)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
