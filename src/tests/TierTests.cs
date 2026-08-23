using Xunit;

namespace TrafficSimulation.Tests;

/// <summary>
/// The suite's own gate: every test class names a tier, and names one that exists.
/// </summary>
/// <remarks>
/// An untiered class is the failure that reads as a pass. It is in no tier's filter, so no run takes
/// it again and nothing says so — the tests are still there, still green in the report they were last
/// in, and no longer being asked. Presence and spelling are all a machine can check here; that the
/// tier named is the <em>right</em> one is what the definitions on <see cref="Tier"/> are for.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class TierTests
{
    static readonly string[] Known = [Tier.Unit, Tier.Town, Tier.Perf, Tier.E2E];

    [Fact]
    public void EveryTestClassNamesItsTier()
    {
        var untiered = new List<string>();
        foreach (var type in typeof(Tier).Assembly.GetTypes())
            if (HoldsTests(type) && TierOf(type) is null)
                untiered.Add(type.FullName!);

        Assert.True(untiered.Count == 0,
            $"no [Trait(Tier.Key, …)] on: {string.Join(", ", untiered)}");
    }

    [Fact]
    public void NoTestClassNamesATierThatDoesNotExist()
    {
        var wrong = new List<string>();
        foreach (var type in typeof(Tier).Assembly.GetTypes())
            if (HoldsTests(type) && TierOf(type) is { } tier && Array.IndexOf(Known, tier) < 0)
                wrong.Add($"{type.FullName} says '{tier}'");

        Assert.True(wrong.Count == 0, $"unknown tier: {string.Join(", ", wrong)}");
    }

    /// <summary>Theories included: <c>TheoryAttribute</c> is a <c>FactAttribute</c>.</summary>
    static bool HoldsTests(Type type)
    {
        if (!type.IsClass || type.IsAbstract) return false;
        foreach (var method in type.GetMethods())
            if (method.IsDefined(typeof(FactAttribute), inherit: true)) return true;
        return false;
    }

    static string? TierOf(Type type)
    {
        foreach (var attribute in type.GetCustomAttributesData())
        {
            if (attribute.AttributeType != typeof(TraitAttribute)) continue;
            if (attribute.ConstructorArguments.Count == 2
                && (string?)attribute.ConstructorArguments[0].Value == Tier.Key)
                return (string?)attribute.ConstructorArguments[1].Value;
        }

        return null;
    }
}
