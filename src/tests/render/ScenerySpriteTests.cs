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

    /// <summary>
    /// <b>A piece is drawn at its look's own size</b> (GEN-6e), kept once a look and not once a piece.
    /// </summary>
    [Fact]
    public void APieceIsDrawnAtItsLooksAuthoredSize()
    {
        var scenery = Lattice(10);
        var sprites = Lay(scenery, mostDrawn: scenery.Count);

        var drawn = Drawn(sprites, WorldM * 0.5f, WorldM, scenery.Count);

        // To the half an instance carries its size in (SpriteInstance): eleven bits of it.
        var halfM = Props.Variants[Wild].SizeM * 0.5f;
        Assert.NotEmpty(drawn);
        Assert.All(drawn, instance =>
        {
            Assert.Equal(halfM.X, instance.HalfSizeM.X, halfM.X / 1024f);
            Assert.Equal(halfM.Y, instance.HalfSizeM.Y, halfM.Y / 1024f);
        });
    }

    /// <summary>The first wild look, which every piece of <see cref="Lattice"/> wears.</summary>
    static readonly ushort Wild = (ushort)Array.FindIndex(Props.Variants, variant => variant.Kind == (int)PropKind.WildNature);

    /// <summary><paramref name="across"/> by <paramref name="across"/> pieces evenly over the town, every one in the first wild look.</summary>
    internal static CityPlan.SceneryArrays Lattice(int across)
    {
        var centreM = new Vector2[across * across];
        var stepM = WorldM / across;
        for (var row = 0; row < across; row++)
        {
            for (var column = 0; column < across; column++) centreM[(row * across) + column] = new Vector2(column + 0.5f, row + 0.5f) * stepM;
        }

        var radiusM = new float[centreM.Length];
        Array.Fill(radiusM, Props.Variants[Wild].GirthM * 0.5f);
        var look = new ushort[centreM.Length];
        Array.Fill(look, Wild);
        return new CityPlan.SceneryArrays { CentreM = centreM, RadiusM = radiusM, Look = look };
    }

    internal static ScenerySprites Lay(CityPlan.SceneryArrays scenery, int mostDrawn) =>
        ScenerySprites.Lay(scenery, WorldM, Props, 0, SimConfig.Shipped().Grid.Main, mostDrawn);

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
