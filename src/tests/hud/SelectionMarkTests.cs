using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// CTL-1's mark: that it is a shape standing outside the unit rather than anything done to the unit's
/// own picture, and that it is laid in the frame that picture is drawn in — turning with a car, upright
/// on a walker.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SelectionMarkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A street framing: the marks are a screen weight, so the zoom decides how thick they come out.</summary>
    const float PixelsPerMetre = 24f;

    static TownWorld Town() => new(Towns.Of(Towns.Fixture), Config);

    /// <summary>
    /// A town with somebody standing in it from the first tick. <b>On a town everybody starts indoors</b>
    /// (GEN-7) and a body inside a building is not drawn, so a walker's mark is asked of the proving
    /// ground — where nobody has anywhere to be and the fifteen of them are put down beside the road.
    /// </summary>
    static TownWorld WithAWalkerOutside() => new(Towns.Of("Track"), Config);

    static OverlayQuad[] Marks(TownWorld world)
    {
        var into = new OverlayQuad[TownRenderer.OverlayCapacity];
        var draw = new ScreenDraw(into);
        SelectionMark.Draw(ref draw, world, Config, PixelsPerMetre);
        return into[..draw.Written];
    }

    /// <summary>Every offset from the marked unit's own middle, which is what a change of heading acts on.</summary>
    static Vector2[] OffsetsFrom(OverlayQuad[] marks, Vector2 centreM) =>
        [.. marks.Select(mark => mark.Centre - centreM)];

    [Fact]
    public void NothingSelectedIsNothingDrawn()
    {
        using var world = Town();
        world.SelectNone();

        Assert.Empty(Marks(world));
    }

    /// <summary>
    /// CTL-1b: <b>one set of brackets a unit and no other shape</b>. A group is read off the marks on its
    /// members, so two units are two units' worth of brackets and nothing is drawn round the pair.
    /// </summary>
    [Fact]
    public void EverySelectedUnitWearsItsOwnBrackets()
    {
        using var world = Town();
        Assert.True(world.Cars.Count >= 2, "the fixture stands fewer cars than a group needs");

        world.Select(new Selection(SelectionKind.Car, 0));
        var one = Marks(world).Length;

        world.SelectAlso(new Selection(SelectionKind.Car, 1));

        Assert.Equal(2, world.SelectedCount);
        Assert.Equal(one * 2, Marks(world).Length);
    }

    /// <summary>
    /// <b>Four brackets of two arms each</b>, all of them in the town's own metres, and every one clear
    /// of the box it wraps: a mark drawn over the bodywork is a mark that hides what it is pointing at.
    /// </summary>
    [Fact]
    public void ACarIsWrappedByFourBracketsStandingOutsideItsOwnFootprint()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var marks = Marks(world);
        Assert.Equal(8, marks.Length);

        ref readonly var build = ref world.Cars.BuildOf(0);
        var forward = Heading.Unit(world.Cars.HeadingRad[0]);
        var right = Heading.RightOf(forward);
        foreach (var mark in marks)
        {
            Assert.Equal(0u, mark.Screen);
            Assert.Equal(Theme.SelectionMark, mark.Colour);

            var offset = mark.Centre - world.Cars.PositionM[0];
            var alongM = MathF.Abs(Vector2.Dot(offset, forward));
            var acrossM = MathF.Abs(Vector2.Dot(offset, right));
            Assert.True(
                alongM > build.HalfLengthM || acrossM > build.FlankM,
                $"a bracket stands {alongM:F2} m by {acrossM:F2} m from the middle of a " +
                $"{build.LengthM:F2} m by {build.WidthM:F2} m car, which is on it rather than round it");
        }
    }

    /// <summary>
    /// <b>The brackets turn with the car</b>, because they wrap the box the picture is stretched over and
    /// that box turns. Drawn in the world's axes instead, they would slide off the nose of anything not
    /// driving east.
    /// </summary>
    [Fact]
    public void ACarsBracketsAreLaidInTheCarsOwnFrame()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        const float TurnRad = 0.7f;
        var before = OffsetsFrom(Marks(world), world.Cars.PositionM[0]);
        world.Cars.HeadingRad[0] += TurnRad;
        var after = OffsetsFrom(Marks(world), world.Cars.PositionM[0]);

        Assert.Equal(before.Length, after.Length);
        foreach (var offset in before)
        {
            var turned = Rotated(offset, TurnRad);
            Assert.Contains(after, at => (at - turned).Length() < 1e-3f);
        }
    }

    /// <summary>
    /// <b>And a walker's do not</b>: its art draws every facing upright and none of it turns with the
    /// body, so brackets that turned would be wrapping a box nothing is drawn in.
    /// </summary>
    [Fact]
    public void AWalkersBracketsStandUprightWhicheverWayItFaces()
    {
        using var world = WithAWalkerOutside();
        world.Select(new Selection(SelectionKind.Person, 0));

        var before = OffsetsFrom(Marks(world), world.People.PositionM[0]);
        world.People.HeadingRad[0] += 1.3f;
        var after = OffsetsFrom(Marks(world), world.People.PositionM[0]);

        Assert.Equal(8, before.Length);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// A walker is wrapped at <b>the height it is drawn at</b> and not at the width of the disc a click
    /// tests: the picking disc is a stride wide and the figure standing on it is head-high, so brackets
    /// sized off the disc would sit in the middle of the body.
    /// </summary>
    [Fact]
    public void AWalkerIsWrappedAtTheHeightItsOwnVariantIsDrawnAt()
    {
        using var world = WithAWalkerOutside();
        world.Select(new Selection(SelectionKind.Person, 0));

        var variant = world.People.Variant[0] % PersonCatalog.Shared.SheetCount;
        var heightM = PersonCatalog.Shared.Variants[variant].HeightM;
        var offsets = OffsetsFrom(Marks(world), world.People.PositionM[0]);

        Assert.True(
            offsets.Max(offset => MathF.Abs(offset.Y)) > heightM * 0.5f,
            "the brackets stand inside the figure they are drawn round");
    }

    static Vector2 Rotated(Vector2 offset, float byRad)
    {
        var (sin, cos) = MathF.SinCos(byRad);
        return new Vector2((offset.X * cos) - (offset.Y * sin), (offset.X * sin) + (offset.Y * cos));
    }
}
