using System.Reflection;
using System.Reflection.Emit;

namespace Catalog.Architecture.Tests;

public sealed class DependencyRulesTests
{
    private static readonly Assembly DomainAssembly = typeof(Catalog.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Catalog.Application.AssemblyMarker).Assembly;
    private static readonly Assembly SqlAssembly = typeof(Catalog.Infrastructure.Sql.AssemblyMarker).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;
    private static readonly Assembly MigratorAssembly = typeof(Catalog.Clients.DatabaseMigrator.AssemblyMarker).Assembly;

    private static readonly Assembly[] ProductionAssemblies =
    [
        DomainAssembly,
        ApplicationAssembly,
        SqlAssembly,
        ApiAssembly,
        MigratorAssembly,
    ];

    // Clean Architecture: references point inward only (Clients -> Infrastructure -> Application -> Domain).
    [Theory]
    [InlineData("Catalog.Domain", new string[0])]
    [InlineData("Catalog.Application", new[] { "Catalog.Domain" })]
    [InlineData("Catalog.Infrastructure.Sql", new[] { "Catalog.Application", "Catalog.Domain" })]
    [InlineData("Catalog.Clients.Api", new[] { "Catalog.Application", "Catalog.Infrastructure.Sql" })]
    [InlineData("Catalog.Clients.DatabaseMigrator", new[] { "Catalog.Infrastructure.Sql" })]
    public void ReferencesPointInwardOnly(string assemblyName, string[] allowed)
    {
        var assembly = ProductionAssemblies.Single(candidate => candidate.GetName().Name == assemblyName);
        var catalogReferences = GetReferenceNames(assembly)
            .Where(reference => reference.StartsWith("Catalog.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(catalogReferences.Except(allowed, StringComparer.Ordinal));
    }

    [Fact]
    public void DomainAndApplicationStayIndependentFromFrameworks()
    {
        var forbiddenPrefixes = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.SqlClient",
            "Azure",
            "Microsoft.Azure",
        };

        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly })
        {
            Assert.DoesNotContain(
                GetReferenceNames(assembly),
                reference => forbiddenPrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void TheDomainDeclaresNoPorts() =>
        // Ports are the application's: the domain holds the model and its rules only.
        Assert.DoesNotContain(
            DomainAssembly.GetTypes(),
            type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal));

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
    public void ApiUsesTheSqlAdapterOnlyAsACompositionDependency()
    {
        var references = GetReferenceNames(ApiAssembly);

        Assert.Contains("Catalog.Infrastructure.Sql", references);
        Assert.DoesNotContain("Catalog.Domain", references);
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSqlAdapterDoesNotDependOnHttp() =>
        Assert.DoesNotContain(
            GetReferenceNames(SqlAssembly),
            reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

    [Fact]
    public void CatalogEndpointsDelegateWithoutCallingTheDomainSqlOrEfCore()
    {
        var endpointTypes = new[]
            {
                ApiAssembly.GetType("Catalog.Clients.Api.ProductEndpoints", throwOnError: true)!,
                ApiAssembly.GetType("Catalog.Clients.Api.CategoryEndpoints", throwOnError: true)!,
            }
            .SelectMany(GetTypeAndNestedTypes)
            .ToArray();
        var forbiddenAssemblies = new[]
        {
            "Catalog.Domain",
            "Catalog.Infrastructure.Sql",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.SqlClient",
        };

        var violations = endpointTypes
            .SelectMany(GetReferencedMembers)
            .Where(member => forbiddenAssemblies.Contains(
                member.Module.Assembly.GetName().Name,
                StringComparer.Ordinal))
            .Select(member => $"{member.Module.Assembly.GetName().Name}:{member.DeclaringType?.FullName}.{member.Name}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
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

    [Fact]
    public void ProductiveProjectsDoNotDependOnPulumiInfrastructure()
    {
        foreach (var assembly in ProductionAssemblies)
        {
            Assert.DoesNotContain("Catalog.Infrastructure", GetReferenceNames(assembly));
            Assert.DoesNotContain(
                GetReferenceNames(assembly),
                reference => reference.StartsWith("Pulumi", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ApiCannotExecuteEntityFrameworkMigrations()
    {
        var references = GetReferenceNames(ApiAssembly);

        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain("Catalog.Clients.DatabaseMigrator", references);
    }

    [Fact]
    public void DatabaseMigratorDoesNotExposeAnHttpApplication()
    {
        var references = GetReferenceNames(MigratorAssembly);

        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || reference.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal));
    }

    private static string[] GetReferenceNames(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name ?? string.Empty)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static IEnumerable<Type> GetTypeAndNestedTypes(Type type)
    {
        yield return type;
        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var descendant in GetTypeAndNestedTypes(nestedType))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<MemberInfo> GetReferencedMembers(Type type)
    {
        var methods = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));

        foreach (var method in methods)
        {
            var body = method.GetMethodBody();
            var il = body?.GetILAsByteArray();
            if (il is null)
            {
                continue;
            }

            var position = 0;
            while (position < il.Length)
            {
                var value = il[position++];
                var opcodeValue = value == 0xfe
                    ? (ushort)(0xfe00 | il[position++])
                    : value;
                var opcode = OpCodesByValue[opcodeValue];

                if (opcode.OperandType is OperandType.InlineField
                    or OperandType.InlineMethod
                    or OperandType.InlineTok
                    or OperandType.InlineType)
                {
                    var token = BitConverter.ToInt32(il, position);
                    position += sizeof(int);

                    MemberInfo? member = null;
                    try
                    {
                        member = method.Module.ResolveMember(
                            token,
                            method.DeclaringType?.GetGenericArguments(),
                            method is MethodInfo methodInfo ? methodInfo.GetGenericArguments() : null);
                    }
                    catch (ArgumentException)
                    {
                        // An unresolved generic token cannot introduce a direct project reference.
                    }

                    if (member is not null)
                    {
                        yield return member;
                    }

                    continue;
                }

                position += GetOperandSize(opcode.OperandType, il, position);
            }
        }
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int position) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget
            or OperandType.InlineI
            or OperandType.InlineSig
            or OperandType.InlineString => 4,
        OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => sizeof(int) + (BitConverter.ToInt32(il, position) * sizeof(int)),
        _ => throw new InvalidOperationException($"Unsupported IL operand type {operandType}."),
    };

    private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
}
