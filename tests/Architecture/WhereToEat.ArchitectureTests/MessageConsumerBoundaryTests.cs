using System.Reflection;
using FluentAssertions;
using Xunit;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Consumer-to-contract guard (Step 2, rule (e)). The design calls this out explicitly: in the
/// in-process modular monolith all MassTransit consumers share one process and one IoC
/// container, so MassTransit decouples the *call*, not the *reference* — nothing physically stops
/// a consumer in one module from taking a hard code reference into another module's internals
/// (a leak a network bus would have made impossible). So the fitness tests must assert
/// **consumer-to-contract / cross-module references**, not merely the API layer: every message
/// consumer may reference, across module lines, only <c>WhereToEat.Contracts</c> types — never
/// another module's Domain/Application internals.
///
/// This rule **deliberately stays in <see cref="System.Reflection"/>** (unlike rules (a)–(d),
/// which use NetArchTest): it is a structural member-signature scan that NetArchTest's
/// namespace/dependency predicate model cannot express. MassTransit is also not a dependency yet,
/// so consumers are detected **structurally** by the <c>IConsumer&lt;TMessage&gt;</c> shape (an
/// interface named <c>IConsumer</c> with one generic argument), independent of which package
/// defines it. For each consumer we inspect the types it structurally touches (the message type,
/// constructor parameters, fields, properties — instance **and static** — and method signatures)
/// and fail if any belongs to a **different** module's assembly.
///
/// The scan iterates <see cref="AssemblyLoader.SolutionAssemblies"/> (ALL <c>WhereToEat.*</c>
/// production assemblies), **not** just <see cref="AssemblyLoader.ModuleAssemblies"/>: the Step 13
/// consumers live in <c>WhereToEat.BuildingBlocks.Messaging</c> — a shared family that
/// <see cref="ModuleGrouping"/> resolves to <c>null</c>, so it is absent from
/// <c>ModuleAssemblies</c>. An <c>IConsumer&lt;&gt;</c> can be declared in any assembly (a module's
/// Infrastructure or the shared Messaging family), so every solution assembly must be scanned or the
/// rule would pass vacuously for consumers it never sees. A consumer whose own assembly resolves to
/// no module (a shared-family host like Messaging) may reference any module's Contracts; it is only
/// flagged when it touches a different module's **internal** (non-Contracts) type.
/// </summary>
public sealed class MessageConsumerBoundaryTests
{
    private const string ConsumerInterfaceName = "IConsumer`1";

    [Fact]
    public void MessageConsumers_ReferenceOnlyContractsAcrossModuleLines()
    {
        var violations = new List<string>();

        // Scan EVERY WhereToEat.* production assembly, not just module assemblies: the Step 13
        // consumers live in the shared BuildingBlocks.Messaging family (which resolves to no module),
        // so restricting to ModuleAssemblies would skip them and pass vacuously.
        foreach (var assembly in AssemblyLoader.SolutionAssemblies)
        {
            var consumerAssemblyName = assembly.GetName().Name;
            var consumerModule = ModuleGrouping.ResolveModule(consumerAssemblyName);

            foreach (var consumer in ConsumerTypesIn(assembly))
            {
                foreach (var touched in StructurallyReferencedTypes(consumer))
                {
                    var touchedAssemblyName = touched.Assembly.GetName().Name;

                    if (IsForbiddenCrossModuleReference(consumerModule, touchedAssemblyName))
                    {
                        violations.Add($"{consumer.FullName} ({consumerAssemblyName}) -> {touched.FullName}");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            "MassTransit consumers may reference only WhereToEat.Contracts types across module " +
            "lines, never another module's Domain/Application internals (in-process MassTransit " +
            "decouples the call, not the reference); found: {0}",
            string.Join("; ", violations));
    }

    // A consumer's reference is forbidden when the touched type belongs to a real module that is
    // NOT the consumer's own module. Touching a shared family (Contracts, SharedKernel,
    // BuildingBlocks.*) resolves to no module and is always allowed — that is exactly the seam a
    // consumer is meant to cross. A consumer that itself lives in a shared family (consumerModule ==
    // null, e.g. BuildingBlocks.Messaging) may therefore reference any module's Contracts, but is
    // flagged the instant it touches a different module's internal (non-shared) type.
    private static bool IsForbiddenCrossModuleReference(string? consumerModule, string? touchedAssemblyName)
    {
        var touchedModule = ModuleGrouping.ResolveModule(touchedAssemblyName);

        // Shared family / host / non-WhereToEat type — always allowed to be referenced.
        if (touchedModule is null)
        {
            return false;
        }

        // Touched type belongs to a real module: allowed only if it is the consumer's own module.
        return !string.Equals(touchedModule, consumerModule, StringComparison.Ordinal);
    }

    // A MassTransit consumer is any concrete type implementing an interface shaped like
    // IConsumer<TMessage> — matched by name + single generic arg, so it works before the
    // MassTransit package is referenced and regardless of which assembly declares the interface.
    private static IEnumerable<Type> ConsumerTypesIn(Assembly assembly)
        => SafeGetTypes(assembly)
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces().Any(IsConsumerInterface));

    private static bool IsConsumerInterface(Type @interface)
        => @interface.IsGenericType
            && string.Equals(@interface.Name, ConsumerInterfaceName, StringComparison.Ordinal)
            && @interface.GetGenericArguments().Length == 1;

    // The set of types a consumer structurally touches: its message type(s), constructor
    // parameters (its injected collaborators), fields, properties, and method param/return types.
    private static HashSet<Type> StructurallyReferencedTypes(Type consumer)
    {
        var touched = new HashSet<Type>();

        foreach (var consumerInterface in consumer.GetInterfaces().Where(IsConsumerInterface))
        {
            AddType(touched, consumerInterface.GetGenericArguments()[0]);
        }

        // Include Static so a consumer's static field/property/method that references another
        // module's internals cannot evade the rule (Instance alone would miss them).
        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var ctor in consumer.GetConstructors(members))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                AddType(touched, parameter.ParameterType);
            }
        }

        foreach (var field in consumer.GetFields(members))
        {
            AddType(touched, field.FieldType);
        }

        foreach (var property in consumer.GetProperties(members))
        {
            AddType(touched, property.PropertyType);
        }

        foreach (var method in consumer.GetMethods(members))
        {
            AddType(touched, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AddType(touched, parameter.ParameterType);
            }
        }

        return touched;
    }

    // Unwrap generic arguments (e.g. Task<RestaurantDto>, IReadOnlyList<Dish>) so a forbidden
    // type hidden inside a generic is still inspected.
    private static void AddType(HashSet<Type> sink, Type type)
    {
        var resolved = type.IsByRef || type.IsArray ? type.GetElementType() : type;
        if (resolved is null || !sink.Add(resolved))
        {
            return;
        }

        if (resolved.IsGenericType)
        {
            foreach (var argument in resolved.GetGenericArguments())
            {
                AddType(sink, argument);
            }
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Tolerate a partially-loadable assembly: inspect the types that did load.
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
