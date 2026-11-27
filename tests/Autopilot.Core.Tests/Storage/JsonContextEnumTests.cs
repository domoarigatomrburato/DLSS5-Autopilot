using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Install;
using Autopilot.Core.Library;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Storage;

/// <summary>
/// Every enum a source-generated JSON context reaches is written by name and refused as a number, so an
/// enum added to a saved shape later cannot slip past the context's converter list.
/// </summary>
[TestClass]
public sealed class JsonContextEnumTests
{
    public static IEnumerable<object[]> EveryContext() =>
    [
        [InstallJsonContext.Default],
        [LibraryJsonContext.Default],
        [NetJsonContext.Default],
    ];

    [TestMethod]
    [DynamicData(nameof(EveryContext))]
    public void Every_enum_a_context_reaches_is_written_by_name(JsonSerializerContext context)
    {
        foreach (Type type in EnumsReachedBy(context))
        {
            object value = Enum.GetValues(type).GetValue(0)!;

            string json = JsonSerializer.Serialize(value, context.GetTypeInfo(type)!);

            Assert.StartsWith("\"", json, $"{type.Name} is written as {json}");
        }
    }

    [TestMethod]
    [DynamicData(nameof(EveryContext))]
    public void Every_enum_a_context_reaches_refuses_a_number(JsonSerializerContext context)
    {
        foreach (Type type in EnumsReachedBy(context))
        {
            JsonTypeInfo info = context.GetTypeInfo(type)!;

            _ = Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize("0", info), $"{type.Name} is read from a number");
        }
    }

    [TestMethod]
    [DynamicData(nameof(EveryContext))]
    public void Every_value_of_every_enum_a_context_reaches_round_trips_by_name(JsonSerializerContext context)
    {
        foreach (Type type in EnumsReachedBy(context))
        {
            JsonTypeInfo info = context.GetTypeInfo(type)!;
            foreach (object value in Enum.GetValues(type))
            {
                string json = JsonSerializer.Serialize(value, info);

                Assert.AreEqual(value, JsonSerializer.Deserialize(json, info), $"{type.Name}.{value} is written as {json}");
            }
        }
    }

    [TestMethod]
    public void The_record_names_quantmotion_so_the_install_can_refuse_it()
    {
        Assert.Contains(typeof(MotionProvider), EnumsReachedBy(InstallJsonContext.Default));
        Assert.AreEqual("\"quant-motion\"", JsonSerializer.Serialize(MotionProvider.QuantMotion, InstallJsonContext.Default.MotionProvider));
    }

    [TestMethod]
    [DynamicData(nameof(EveryContext))]
    public void Every_context_reaches_at_least_one_type(JsonSerializerContext context) =>
        Assert.IsNotEmpty(Reached(context));

    private static List<Type> EnumsReachedBy(JsonSerializerContext context) =>
        [.. Reached(context).Where(type => type.IsEnum)];

    // Every type reachable from the context's JsonTypeInfo properties: their members, elements, keys and nullable underlying types.
    private static HashSet<Type> Reached(JsonSerializerContext context)
    {
        Queue<Type> pending = new(
            context.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType.IsAssignableTo(typeof(JsonTypeInfo)))
                .Select(property => ((JsonTypeInfo)property.GetValue(context)!).Type));
        HashSet<Type> seen = [];
        while (pending.TryDequeue(out Type? type))
        {
            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                pending.Enqueue(underlying);
                continue;
            }

            if (!seen.Add(type) || context.GetTypeInfo(type) is not { } info)
            {
                continue;
            }

            foreach (Type next in info.Properties.Select(property => property.PropertyType).Append(info.ElementType).Append(info.KeyType).OfType<Type>())
            {
                pending.Enqueue(next);
            }
        }

        return seen;
    }
}
