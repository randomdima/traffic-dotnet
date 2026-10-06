using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The town's scenery as instances (<see cref="ScenerySprites"/>): every piece in view drawn while the budget holds
/// them, an even share within it once it does not, and the same share whichever way the view is panned.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class ScenerySpriteTests
{
    static readonly PropCatalog Props = PropCatalog.Load();

    /// <summary>The town the scenery stands on, a kilometre square.</summary>
    static readonly Vector2 WorldM = new(1_000f);

    /// <summary>
    /// <b>Every piece in view is drawn while the budget holds them</b>, each where the plan stands it: a hundred by a
    /// hundred pieces on a lattice, a budget of all of them, a view over the whole town.
    /// </summary>
    [Fact]
    public void EveryPieceInViewIsDrawnWhileTheBudgetHoldsThem()
    {
        var scenery = Lattice(100);
        var sprites = Lay(scenery, mostDrawn: scenery.Count);

        var drawn = Drawn(sprites, WorldM * 0.5f, WorldM, scenery.Count);

        Assert.Equal(scenery.CentreM.Order(ByPlace).ToArray(), drawn.Select(instance => instance.CentreM).Order(ByPlace).ToArray());
    }

    /// <summary>
    /// <b>A view holding more than the budget draws every n-th piece, n the least power of two that brings it within
    /// the budget</b>: ten thousand in view and a budget of a thousand draw every sixteenth.
    /// </summary>
    [Fact]
    public void AViewPastTheBudgetDrawsEveryNthPiece()
    {
        var scenery = Lattice(100);
        var sprites = Lay(scenery, mostDrawn: 1_000);

        var drawn = Drawn(sprites, WorldM * 0.5f, WorldM, 1_000);

        Assert.Equal(10_000 / 16, drawn.Length);
    }

    /// <summary>
    /// <b>A pan draws the same pieces where its two views meet</b>: thinned at one zoom, the pieces drawn in the
    /// ground two views share are the same pieces, so the scenery does not shimmer as the view moves.
    /// </summary>
    [Fact]
    public void APanDrawsTheSamePiecesWhereItsViewsMeet()
    {
        var scenery = Lattice(100);
        var sprites = Lay(scenery, mostDrawn: 1_000);
        var spanM = new Vector2(600f);
        var sharedFromM = new Vector2(400f, 200f);
        var sharedToM = new Vector2(500f, 800f);

        var left = Within(Drawn(sprites, new Vector2(300f, 500f), spanM, 1_000), sharedFromM, sharedToM);
        var right = Within(Drawn(sprites, new Vector2(600f, 500f), spanM, 1_000), sharedFromM, sharedToM);

        Assert.NotEmpty(left);
        Assert.Equal(left, right);
    }

    /// <summary><paramref name="across"/> by <paramref name="across"/> pieces evenly over the town, every one a metre across.</summary>
    internal static CityPlan.SceneryArrays Lattice(int across)
    {
        var centreM = new Vector2[across * across];
        var stepM = WorldM / across;
        for (var row = 0; row < across; row++)
        {
            for (var column = 0; column < across; column++) centreM[(row * across) + column] = new Vector2(column + 0.5f, row + 0.5f) * stepM;
        }

        var radiusM = new float[centreM.Length];
        Array.Fill(radiusM, 0.5f);
        return new CityPlan.SceneryArrays { CentreM = centreM, RadiusM = radiusM };
    }

    internal static ScenerySprites Lay(CityPlan.SceneryArrays scenery, int mostDrawn)
    {
        var aspects = new float[Props.Count];
        Array.Fill(aspects, 1f);
        return ScenerySprites.Lay(scenery, WorldM, Props, 0, aspects, SimConfig.Shipped().Grid.Main, mostDrawn);
    }

    static SpriteInstance[] Drawn(ScenerySprites sprites, Vector2 viewCentreM, Vector2 viewSpanM, int room)
    {
        var into = new SpriteInstance[room];
        return into[..sprites.Fill(viewCentreM, viewSpanM, into)];
    }

    static Vector2[] Within(SpriteInstance[] drawn, Vector2 fromM, Vector2 toM) =>
    [
        .. drawn.Select(instance => instance.CentreM)
            .Where(atM => atM.X >= fromM.X && atM.Y >= fromM.Y && atM.X < toM.X && atM.Y < toM.Y)
            .Order(ByPlace),
    ];

    static readonly Comparer<Vector2> ByPlace = Comparer<Vector2>.Create(static (a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
}
