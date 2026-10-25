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
/// is drawn exactly once, that the cull is a superset and never a subset, and that a prop is drawn in the
/// look the plan laid it in, at that look's own size.
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
        StandingSprites.Lay(plan, Buildings, uses, Props, 0, Buildings.Count, SimConfig.Shipped().Grid.Main);

    static SpriteInstance[] Everything(CityPlan plan, StandingSprites standing)
    {
        var (first, count) = standing.Range(plan.WorldSizeM * 0.5f, plan.WorldSizeM * 2f);

        Assert.Equal((0, StandingSprites.CapacityFor(plan)), (first, count));
        return standing.Instances.ToArray();
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

        var spanM = new Vector2(70f, 40f);

        for (var step = 0; step < 24; step++)
        {
            var centreM = plan.WorldSizeM * new Vector2((step % 6) / 5f, (step / 6) / 3f);
            var (first, count) = standing.Range(centreM, spanM);
            var drawn = new HashSet<Vector2>();
            foreach (var instance in standing.Instances.Span.Slice(first, count)) drawn.Add(instance.CentreM);

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

    /// <summary><b>A prop is drawn in the look the plan laid it in</b> (GEN-6e): the catalogue chooses nothing.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPropIsDrawnInThePlansLook(string map)
    {
        var plan = Towns.Of(map);
        var drawn = DrawnByPlace(plan, Lay(plan));

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            Assert.Equal((uint)(Buildings.Count + plan.Props.Look[prop]), drawn[plan.Props.CentreM[prop]].Sheet);
        }
    }

    /// <summary>
    /// <b>A prop is drawn at its look's own size</b> (GEN-6e), and nothing the plan laid stretches it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPropIsDrawnAtItsLooksAuthoredSize(string map)
    {
        var plan = Towns.Of(map);
        var drawn = DrawnByPlace(plan, Lay(plan));

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            // To the half an instance carries its size in (SpriteInstance): eleven bits of it.
            var sizeM = Props.Variants[plan.Props.Look[prop]].SizeM;
            var halfM = drawn[plan.Props.CentreM[prop]].HalfSizeM;
            Assert.Equal(sizeM.X * 0.5f, halfM.X, sizeM.X / 1024f);
            Assert.Equal(sizeM.Y * 0.5f, halfM.Y, sizeM.Y / 1024f);
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
}
