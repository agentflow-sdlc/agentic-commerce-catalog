using System.Reflection;
using System.Reflection.Emit;

namespace Catalog.Architecture.Tests;

public sealed class DependencyRulesTests
{
    private static readonly Assembly ContractsAssembly = typeof(Catalog.Contracts.HealthResponse).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;
    private static readonly Assembly ManagersAssembly = typeof(Catalog.Managers.AssemblyMarker).Assembly;
    private static readonly Assembly EnginesAssembly = typeof(Catalog.Engines.AssemblyMarker).Assembly;
    private static readonly Assembly AccessorsAssembly = typeof(Catalog.Accessors.AssemblyMarker).Assembly;
    private static readonly Assembly MigratorAssembly = typeof(Catalog.DatabaseMigrator.AssemblyMarker).Assembly;

    private static readonly Assembly[] ProductionAssemblies =
    [
        ContractsAssembly,
        ApiAssembly,
        ManagersAssembly,
        EnginesAssembly,
        AccessorsAssembly,
        MigratorAssembly,
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
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogEndpointsDelegateWithoutCallingEnginesAccessorsOrEfCore()
    {
        var endpointTypes = new[]
            {
                ApiAssembly.GetType("Catalog.Api.ProductEndpoints", throwOnError: true)!,
                ApiAssembly.GetType("Catalog.Api.CategoryEndpoints", throwOnError: true)!,
            }
            .SelectMany(GetTypeAndNestedTypes)
            .ToArray();
        var forbiddenAssemblies = new[]
        {
            "Catalog.Engines",
            "Catalog.Accessors",
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
    public void NeutralLayersRemainIndependentFromAzureSdkAssemblies()
    {
        foreach (var assembly in new[] { ContractsAssembly, EnginesAssembly, ManagersAssembly })
        {
            Assert.DoesNotContain(
                GetReferenceNames(assembly),
                reference => reference.StartsWith("Azure", StringComparison.Ordinal)
                    || reference.StartsWith("Microsoft.Azure", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ApiCannotExecuteEntityFrameworkMigrations()
    {
        var references = GetReferenceNames(ApiAssembly);

        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain("Catalog.DatabaseMigrator", references);
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
