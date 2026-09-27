using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The town's standing geometry as instances: that every building and every prop of every shipped map
/// is drawn exactly once, that the cull is a superset and never a subset, and that the two look rules —
/// the nearest roof turned to its door, the prop's kind and band — are the rules and not something near
/// them.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class StandingSpriteTests
{
    static readonly BuildingCatalog Buildings = BuildingCatalog.Load();
    static readonly PropCatalog Props = PropCatalog.Load();

    public static TheoryData<string> Maps => Towns.EveryTown();

    static StandingSprites Lay(CityPlan plan) => Lay(plan, BuildingUses.Of(plan));

    static StandingSprites Lay(CityPlan plan, BuildingUses uses) =>
        StandingSprites.Lay(plan, Buildings, uses, Props, 0, Buildings.Count, Aspects(), SimConfig.Shipped().Grid.Main);

    static float[] Aspects()
    {
        var aspects = new float[Buildings.Count + Props.Count];
        Array.Fill(aspects, 1f);
        return aspects;
    }

    static SpriteInstance[] Everything(CityPlan plan, StandingSprites standing)
    {
        var into = new SpriteInstance[StandingSprites.CapacityFor(plan)];
        var written = standing.Fill(plan.WorldSizeM * 0.5f, plan.WorldSizeM * 2f, into);

        Assert.Equal(into.Length, written);
        return into;
    }

    /// <summary>
    /// What was drawn, by where it stands. The instances are laid in the cull grid's order rather than
    /// the plan's, which is the whole point of them — so a test that wants one body's instance asks for
    /// it by the one thing the two orders share.
    /// </summary>
    static Dictionary<Vector2, SpriteInstance> DrawnByPlace(CityPlan plan, StandingSprites standing)
    {
        var byPlace = new Dictionary<Vector2, SpriteInstance>();
        foreach (var instance in Everything(plan, standing)) byPlace[instance.CentreM] = instance;
        return byPlace;
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStandingBodyIsDrawnExactlyOnce(string map)
    {
        var plan = Towns.Of(map);
        var drawn = Everything(plan, Lay(plan));

        var seen = new HashSet<Vector2>();
        var duplicates = 0;
        foreach (var instance in drawn)
        {
            if (!seen.Add(instance.CentreM)) duplicates++;
        }

        // Two props may genuinely be laid on the same point in no town this project ships, so a repeat
        // here is the counting sort having dropped one instance twice rather than a coincidence.
        Assert.Equal(0, duplicates);
        Assert.Equal(plan.Buildings.Count + plan.Props.Count, drawn.Length);
    }

    /// <summary>
    /// <b>A cull may only ever be a superset.</b> Anything whose quad touches the view has to be in the
    /// buffer; anything else in it is over-draw and costs nothing but a discarded fragment.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheCullDropsNothingItCanSee(string map)
    {
        var plan = Towns.Of(map);
        var standing = Lay(plan);

        var into = new SpriteInstance[StandingSprites.CapacityFor(plan)];
        var spanM = new Vector2(70f, 40f);

        for (var step = 0; step < 24; step++)
        {
            var centreM = plan.WorldSizeM * new Vector2((step % 6) / 5f, (step / 6) / 3f);
            var written = standing.Fill(centreM, spanM, into);
            var drawn = new HashSet<Vector2>();
            for (var instance = 0; instance < written; instance++) drawn.Add(into[instance].CentreM);

            for (var prop = 0; prop < plan.Props.Count; prop++)
            {
                var centreOfProp = plan.Props.CentreM[prop];
                var reachM = plan.Props.RadiusM[prop];
                var offset = Vector2.Abs(centreOfProp - centreM) - ((spanM * 0.5f) + new Vector2(reachM));
                if (offset.X > 0f || offset.Y > 0f) continue;

                Assert.True(drawn.Contains(centreOfProp), $"{map}: the prop at {centreOfProp} is in view at {centreM} and was not drawn");
            }
        }
    }

    /// <summary>
    /// <b>A prop is drawn inside the circle its body is</b> (GEN-6d): the longest side of the picture is
    /// the diameter the plan laid it at, so nothing is drawn past the ground a car is held off. And it
    /// wears a look from its own kind's set.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPropIsDrawnAtItsOwnSizeInItsOwnKind(string map)
    {
        var plan = Towns.Of(map);
        var drawn = DrawnByPlace(plan, Lay(plan));

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var instance = drawn[plan.Props.CentreM[prop]];
            Assert.Equal(
                plan.Props.RadiusM[prop], MathF.Max(instance.HalfSizeM.X, instance.HalfSizeM.Y), 4);

            var variant = (int)instance.Sheet - Buildings.Count;
            Assert.InRange(variant, 0, Props.Count - 1);
            Assert.Equal(plan.Props.Kind[prop], (byte)Props.Variants[variant].Kind);
        }
    }

    /// <summary>
    /// The two-step: a look inside the tolerance is picked from the band, and a size no look was
    /// authored for falls back to the nearest in the kind rather than to nothing.
    /// </summary>
    [Fact]
    public void APropTakesTheBandItsSizeNamesAndTheNearestWhenThereIsNoBand()
    {
        for (var kind = 0; kind <= 2; kind++)
        {
            var authored = Props.Variants.Where(variant => variant.Kind == kind).Select(variant => variant.DiameterM).ToArray();
            Assert.NotEmpty(authored);

            foreach (var diameterM in authored.Distinct())
            {
                for (var index = 0; index < 32; index++)
                {
                    var look = Props.Variants[Props.Look(kind, diameterM, index)];
                    Assert.Equal(kind, look.Kind);
                    Assert.True(MathF.Abs(look.DiameterM - diameterM) <= PropCatalog.BandToleranceM,
                        $"a {diameterM} m prop of kind {kind} wears {look.Id} at {look.DiameterM} m");
                }
            }

            var offTheScale = Props.Variants[Props.Look(kind, 40f, 0)];
            Assert.Equal(kind, offTheScale.Kind);
            Assert.Equal(authored.Max(), offTheScale.DiameterM);
        }
    }

    /// <summary>
    /// <b>No wild look turns</b> (GEN-6b). The wild pass drops its props on open ground and carries no
    /// bearing to turn one by, so a wild look that declared a front would be drawn upright wherever it
    /// stood — a picture asking for something the placement can never give it.
    /// </summary>
    [Fact]
    public void NoWildLookTurns()
    {
        var turned = Props.Variants
            .Where(variant => variant.Kind == (int)PropKind.WildNature && variant.Turns)
            .Select(variant => variant.Id);

        Assert.Empty(turned);
    }

    /// <summary>The look is a function of the prop's index, so two runs of the same town draw the same town.</summary>
    [Fact]
    public void TheSameIndexAlwaysDrawsTheSameLook()
    {
        for (var index = 0; index < 1_000; index++)
        {
            Assert.Equal(Props.Look(0, 2.2f, index), Props.Look(0, 2.2f, index));
        }

        // And it is a hash rather than a constant: the shipped catalogue has several looks in every
        // band, and a town of one look is what a broken mix looks like.
        var looks = new HashSet<int>();
        for (var index = 0; index < 1_000; index++) looks.Add(Props.Look(0, 2.2f, index));
        Assert.True(looks.Count > 1);
    }
}
