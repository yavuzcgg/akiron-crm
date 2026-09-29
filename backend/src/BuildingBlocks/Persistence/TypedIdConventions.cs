using System.Reflection;
using Akiron.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Akiron.BuildingBlocks.Persistence;

internal static class TypedIdConventions
{
    /// <summary>Registers a <c>uuid</c> conversion for every <see cref="ITypedId{TSelf}"/> in the given assemblies.</summary>
    public static void Apply(ModelConfigurationBuilder builder, params Assembly[] assemblies)
    {
        var typedIds = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsValueType: true, IsGenericTypeDefinition: false } && IsTypedId(type));

        foreach (var idType in typedIds)
        {
            builder.Properties(idType).HaveConversion(typeof(TypedIdValueConverter<>).MakeGenericType(idType));
        }
    }

    private static bool IsTypedId(Type type) =>
        type.GetInterfaces().Any(contract =>
            contract.IsGenericType &&
            contract.GetGenericTypeDefinition() == typeof(ITypedId<>) &&
            contract.GenericTypeArguments[0] == type);
}

public sealed class TypedIdValueConverter<TId>() : ValueConverter<TId, Guid>(
    id => id.Value,
    value => TypedIdFactory<TId>.From(value))
    where TId : struct, ITypedId<TId>;

/// <summary>
/// Expression trees cannot call static abstract interface members directly, so the converter
/// goes through this ordinary static method instead.
/// </summary>
public static class TypedIdFactory<TId>
    where TId : struct, ITypedId<TId>
{
#pragma warning disable CA1000 // Generic static member: the whole point is to be callable from an expression tree.
    public static TId From(Guid value) => TId.From(value);
#pragma warning restore CA1000
}
