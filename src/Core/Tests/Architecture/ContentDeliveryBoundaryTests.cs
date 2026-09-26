using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Cms.ContentDelivery;
using Xunit;

namespace Core.Tests.Architecture;

// The Content Delivery SDK is the public boundary for CMS websites: it must not leak Core
// persistence, Domain entities, write/translation machinery or raw translation data, and its
// reads must be tenant-bound at startup rather than per call.
public class ContentDeliveryBoundaryTests
{
    private static readonly Assembly Sdk = typeof(IContentDeliveryClient).Assembly;

    private static readonly string[] AllowedReferencePrefixes =
    {
        "System",
        "netstandard",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.DependencyInjection.Abstractions"
    };

    private static IEnumerable<PropertyInfo> PublicProperties =>
        Sdk.GetExportedTypes().SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

    private static IEnumerable<MethodInfo> PublicMethods =>
        Sdk.GetExportedTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

    [Fact]
    public void Sdk_ReferencesNoCoreProjectOrPersistenceAssembly()
    {
        var violations = Sdk.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => !AllowedReferencePrefixes.Any(prefix => name == prefix || name.StartsWith(prefix + ".")))
            .ToList();

        Assert.True(violations.Count == 0, $"Cms.ContentDelivery must not reference {string.Join(", ", violations)}.");
    }

    [Fact]
    public void PublicSurface_UsesOnlySdkAndFrameworkTypes()
    {
        var surface = PublicProperties.Select(p => p.PropertyType)
            .Concat(PublicMethods.Select(m => m.ReturnType))
            .Concat(PublicMethods.SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
            .SelectMany(Flatten)
            .Distinct();

        var violations = surface
            .Where(t => t.Assembly != Sdk && t.Namespace?.StartsWith("System") != true && t.Namespace?.StartsWith("Microsoft.Extensions") != true)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0, $"Public SDK surface exposes non-SDK types: {string.Join(", ", violations)}.");
    }

    [Fact]
    public void NoPublicOperation_AcceptsAnApplicationId()
    {
        var violations = PublicMethods
            .SelectMany(m => m.GetParameters().Select(p => (m, p)))
            .Where(x => x.p.Name.Contains("applicationId", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.m.DeclaringType.Name}.{x.m.Name}")
            .ToList();

        Assert.True(violations.Count == 0, $"Tenant must be bound at startup, not passed per call: {string.Join(", ", violations)}.");
    }

    [Fact]
    public void OnlyTheStartupOptions_CarryAnApplicationId()
    {
        var carriers = PublicProperties
            .Where(p => p.Name.Contains("ApplicationId", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.DeclaringType)
            .ToList();

        Assert.Equal(new[] { typeof(ContentDeliveryOptions) }, carriers);
    }

    [Fact]
    public void TenantContext_IsNotPublic()
    {
        Assert.False(typeof(ContentDeliveryTenant).IsPublic);
    }

    [Theory]
    [InlineData("FarsiContent")]
    [InlineData("Translations")]
    [InlineData("Fingerprint")]
    [InlineData("Provider")]
    [InlineData("Model")]
    [InlineData("Prompt")]
    [InlineData("Error")]
    [InlineData("Job")]
    public void PublicDtos_ExposeNoTranslationInternals(string forbidden)
    {
        var violations = PublicProperties
            .Where(p => p.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.DeclaringType.Name}.{p.Name}")
            .ToList();

        Assert.True(violations.Count == 0, $"Public SDK DTOs expose {forbidden}: {string.Join(", ", violations)}.");
    }

    [Fact]
    public void PublicDtos_AreSealedAndImmutable()
    {
        var types = Sdk.GetExportedTypes().Where(t => t.IsClass && !(t.IsAbstract && t.IsSealed)).ToList();
        Assert.NotEmpty(types);

        var violations = new List<string>();
        foreach (var type in types)
        {
            if (!type.IsSealed)
                violations.Add($"{type.Name} is not sealed");

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var setter = property.SetMethod;
                if (setter is { IsPublic: true } && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                    violations.Add($"{type.Name}.{property.Name} has a public setter");

                var propertyType = property.PropertyType;
                if (propertyType.IsArray || (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>)))
                    violations.Add($"{type.Name}.{property.Name} is a mutable collection");
            }
        }

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Fact]
    public void PublicInterfaces_AreOnlyTheDeliveryClient()
    {
        Assert.Equal(new[] { typeof(IContentDeliveryClient) }, Sdk.GetExportedTypes().Where(t => t.IsInterface));
    }

    // net9 websites must be able to consume the SDK (net10 ones still can).
    [Fact]
    public void Sdk_TargetsNet9()
    {
        Assert.Equal(".NETCoreApp,Version=v9.0", Sdk.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
    }

    [Fact]
    public void Sdk_ReferencesNoAssemblyNewerThanNet9()
    {
        var violations = Sdk.GetReferencedAssemblies()
            .Where(a => a.Name != "netstandard" && a.Version.Major > 9)
            .Select(a => $"{a.Name} {a.Version}")
            .ToList();

        Assert.True(violations.Count == 0, $"A net9 consumer cannot satisfy: {string.Join(", ", violations)}.");
    }

    // Nullable analysis is on for the whole public surface: every reference-typed property is
    // either annotated optional, or non-null and then required or given a non-null default.
    [Fact]
    public void PublicDtos_DeclareNullability_AndRequireNonNullValues()
    {
        var context = new NullabilityInfoContext();
        var violations = new List<string>();

        foreach (var property in PublicProperties.Where(p => !p.PropertyType.IsValueType && !p.DeclaringType.IsGenericTypeDefinition))
        {
            var state = context.Create(property).ReadState;
            if (state == NullabilityState.Unknown)
                violations.Add($"{property.DeclaringType.Name}.{property.Name} is nullable-oblivious");
            else if (state == NullabilityState.NotNull && property.SetMethod != null
                     && !property.IsDefined(typeof(RequiredMemberAttribute))
                     && !IsReadOnlyCollection(property.PropertyType))
                violations.Add($"{property.DeclaringType.Name}.{property.Name} is non-null but neither required nor defaulted");
        }

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Fact]
    public void Result_ValueIsOptional_AndAvailableOnlyWhenFound()
    {
        var type = typeof(ContentDeliveryResult<ContentSummary>);
        var context = new NullabilityInfoContext();

        Assert.Equal(NullabilityState.Nullable, context.Create(type.GetProperty(nameof(ContentDeliveryResult<ContentSummary>.Value))).ReadState);

        var isFound = type.GetProperty(nameof(ContentDeliveryResult<ContentSummary>.IsFound)).GetCustomAttribute<MemberNotNullWhenAttribute>();
        Assert.NotNull(isFound);
        Assert.True(isFound.ReturnValue);
        Assert.Equal(new[] { nameof(ContentDeliveryResult<ContentSummary>.Value) }, isFound.Members);

        var outValue = type.GetMethod(nameof(ContentDeliveryResult<ContentSummary>.TryGetValue)).GetParameters().Single();
        Assert.True(outValue.GetCustomAttribute<NotNullWhenAttribute>()?.ReturnValue);
        Assert.Equal(NullabilityState.Nullable, context.Create(outValue).WriteState);
    }

    [Theory]
    [InlineData(typeof(Domains.Entities.BaseEntity))]
    [InlineData(typeof(Application.Repository.IRepository<>))]
    [InlineData(typeof(Infrastructure.Data.ApplicationDbContext))]
    public void CoreLayers_DoNotDependOnTheSdk(Type typeFromAssembly)
    {
        Assert.DoesNotContain(typeFromAssembly.Assembly.GetReferencedAssemblies(), a => a.Name == Sdk.GetName().Name);
    }

    private static bool IsReadOnlyCollection(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.IsByRef || type.IsArray)
            return Flatten(type.GetElementType());
        if (type.IsGenericParameter)
            return Enumerable.Empty<Type>();
        if (type.IsGenericType)
            return new[] { type.GetGenericTypeDefinition() }.Concat(type.GetGenericArguments().SelectMany(Flatten));
        return new[] { type };
    }
}
