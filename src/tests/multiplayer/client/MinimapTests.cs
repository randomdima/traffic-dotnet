using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Client;
using TrafficSimulation.Tests.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Client;

/// <summary>
/// MP-12: <b>a player's map draws inside its own square and nowhere else</b>, and a player's arrow is in its seat's
/// colour.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class MinimapTests
{
    static readonly SimConfig Config = SimConfig.Shipped();
    static readonly Vector2 UiPx = new(1600f, 900f);

    static readonly Lazy<Minimap> Map = new(() => new Minimap(Towns.Of(Towns.Fixture), Config));

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 0.3f)]
    public void NothingTheCornerMapDrawsReachesPastItsFrame(float acrossShare, float downShare)
    {
        var centreM = Towns.Of(Towns.Fixture).WorldSizeM * new Vector2(acrossShare, downShare);

        AllInside(Map.Value.Corner(UiPx, centreM));
    }

    [Fact]
    public void NothingTheWholeMapDrawsReachesPastItsFrame() => AllInside(Map.Value.Whole(UiPx));

    [Fact]
    public void TheWholeMapShowsTheWholeTownOnTheScreen()
    {
        var frame = Map.Value.Whole(UiPx);
        var screen = new MapFrame(Vector2.Zero, UiPx, Vector2.Zero, 1f);

        Assert.True(screen.Holds(frame.ToPx(Vector2.Zero)) && screen.Holds(frame.ToPx(Towns.Of(Towns.Fixture).WorldSizeM)));
    }

    static void AllInside(in MapFrame frame)
    {
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        var draw = new ScreenDraw(quads);

        Map.Value.Draw(ref draw, frame);

        var edgePx = Config.Multiplayer.MapEdgePx;
        var leastPx = frame.AtPx - new Vector2(edgePx);
        var mostPx = frame.EndPx + new Vector2(edgePx);
        foreach (var quad in quads.AsSpan(0, draw.Written))
        {
            foreach (var cornerPx in Corners(quad))
            {
                Assert.InRange(cornerPx.X, leastPx.X - 1e-3f, mostPx.X + 1e-3f);
                Assert.InRange(cornerPx.Y, leastPx.Y - 1e-3f, mostPx.Y + 1e-3f);
            }
        }
    }

    [Fact]
    public void APlayersArrowIsDrawnInItsSeatsColour()
    {
        var quads = new OverlayQuad[256];
        var draw = new ScreenDraw(quads);
        var centreM = new Vector2(300f, 300f);

        Map.Value.Arrow(ref draw, Map.Value.Corner(UiPx, centreM), centreM + new Vector2(40f, 0f), Vector2.UnitX, Theme.Player(3));

        var colours = quads.AsSpan(0, draw.Written).ToArray().Select(quad => quad.Colour).Distinct();
        Assert.Equal([Theme.MarkOutline, Theme.Player(3)], colours);
    }

    /// <summary>No seat's colour is a grey or a white, which would read as a road.</summary>
    [Fact]
    public void NoPlayersColourIsAGrey()
    {
        for (var seat = 0; seat < Config.Multiplayer.MostSeats; seat++)
        {
            var colour = Theme.Player(seat);
            var chroma = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z)) - MathF.Min(colour.X, MathF.Min(colour.Y, colour.Z));
            Assert.True(chroma >= 0.3f, $"seat {seat}'s colour is {chroma} from a grey");
        }
    }

    /// <summary>
    /// <b>The road a player stands on is under its arrow</b>, on the corner and on the whole town alike — however the
    /// strokes were found for the box or straightened for its scale.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheRoadAPlayerStandsOnIsDrawnUnderIt(bool whole)
    {
        var plan = Towns.Of(Towns.Fixture);
        var road = Enumerable.Range(0, plan.Roads.Count).First(each => !plan.Roads.IsABay(each) && !plan.Roads.SegmentsOf(each).IsEmpty);
        var piece = plan.Roads.SegmentsOf(road)[0];
        var standM = piece.PointAtM(piece.LengthM * 0.5f);
        // The whole town on a screen small enough that it is drawn coarser than the corner, off the straightened strokes.
        var frame = whole ? Map.Value.Whole(new Vector2(160f)) : Map.Value.Corner(UiPx, standM);
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        var draw = new ScreenDraw(quads);

        Map.Value.Draw(ref draw, frame);

        Assert.Equal(whole, frame.MetresPerPx > Config.MapMetresPerPx);

        var standPx = frame.ToPx(standM);
        Assert.Contains(quads[..draw.Written], quad => quad.Colour == Theme.MapRoad && Under(quad, standPx));
    }

    /// <summary>Whether a point lies on a quad, its edges and a pixel round them included.</summary>
    static bool Under(in OverlayQuad quad, Vector2 pointPx)
    {
        var (sin, cos) = MathF.SinCos(quad.Rotation);
        var offPx = pointPx - quad.Centre;
        return MathF.Abs(Vector2.Dot(offPx, new Vector2(cos, sin))) <= quad.HalfSize.X + 1f &&
               MathF.Abs(Vector2.Dot(offPx, new Vector2(-sin, cos))) <= quad.HalfSize.Y + 1f;
    }

    [Fact]
    public void APlayerPastTheMapsSquareIsNotDrawn()
    {
        var quads = new OverlayQuad[256];
        var draw = new ScreenDraw(quads);
        var centreM = new Vector2(300f, 300f);

        Map.Value.Arrow(
            ref draw, Map.Value.Corner(UiPx, centreM), centreM + new Vector2(Config.Multiplayer.MapSpanM, 0f), Vector2.UnitX,
            Theme.Player(3));

        Assert.Equal(0, draw.Written);
    }

    [Fact]
    public void ARoadAcrossTheSquareIsCutToItsSides()
    {
        var fromPx = new Vector2(-50f, 20f);
        var toPx = new Vector2(150f, 20f);

        Assert.True(Minimap.Clipped(ref fromPx, ref toPx, Vector2.Zero, new Vector2(100f)));
        Assert.Equal((new Vector2(0f, 20f), new Vector2(100f, 20f)), (fromPx, toPx));
    }

    static Vector2[] Corners(in OverlayQuad quad)
    {
        var (sin, cos) = MathF.SinCos(quad.Rotation);
        var along = new Vector2(cos, sin) * quad.HalfSize.X;
        var across = new Vector2(-sin, cos) * quad.HalfSize.Y;
        return [quad.Centre + along + across, quad.Centre + along - across, quad.Centre - along + across, quad.Centre - along - across];
    }
}
