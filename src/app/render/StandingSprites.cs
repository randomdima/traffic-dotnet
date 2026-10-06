using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The town's buildings and its props as instances of the same pipeline the walkers use, laid
/// <b>once</b> when the plan is opened and indexed by a grid of cells so a frame copies the runs it can
/// see rather than walking the town.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here moves</b>, so nothing here is decided per frame: a roof's picture, its size and its
/// bearing are all fixed by the plan, and a prop's look is fixed by its kind, its size and its index.
/// What a frame does is a range copy per row of cells in view — a town of ninety-five thousand props at
/// a street framing costs the dozen that are on screen, and the whole town only when the whole town is.
/// </para>
/// <para>
/// <b>Why a cell grid rather than the roster order.</b> Emitting the town's statics is otherwise the one
/// place a frame's cost is O(the size of the town) whatever is on screen — which is not a crossing, so
/// rule 1 does not forbid it, but it is exactly the shape rule 1 exists to keep out of the frame. The
/// cells are laid row-major, so the rows in view are one stretch of what the device was handed once, and
/// a frame writes where it starts and how long it is (<see cref="Range"/>).
/// </para>
/// <para>
/// <b>A building is drawn at the roof's own authored footprint and a prop at the size the plan laid
/// it.</b> The two differ on purpose: a roof is one picture of one authored building and stretching it
/// to a plan box that is a few centimetres off would show as a bent ridge line, while a prop's whole
/// size range <em>is</em> the jitter the plan carries, and its body is that circle. Where the roof and
/// the box disagree, the generator sized the building off this catalogue and the disagreement is
/// centimetres.
/// </para>
/// </remarks>
internal sealed class StandingSprites
{
    readonly SpriteInstance[] _instances;

    /// <summary>Where each row of <see cref="_window"/> starts in <see cref="_instances"/>, and one past the last.</summary>
    readonly int[] _rowStarts;

    /// <summary>
    /// <b>The cull grid is the town's own</b> (SIM-8): every instance filed by its centre in a cell of the
    /// main level over the whole town, row by row, so the rows a view touches are one stretch.
    /// </summary>
    readonly GridWindow _window;

    /// <summary>
    /// <b>How far the widest instance reaches from its centre</b>, whichever way it faces — what a view is
    /// grown by, because an instance is filed where its centre is and drawn wherever it reaches.
    /// </summary>
    readonly float _reachM;

    StandingSprites(SpriteInstance[] instances, int[] rowStarts, GridWindow window, float reachM)
    {
        _instances = instances;
        _rowStarts = rowStarts;
        _window = window;
        _reachM = reachM;
    }

    public static StandingSprites Nothing { get; } =
        new([], [0], GridWindow.Of(new WorldGrid(1f).Main, 0, 0, 0, 0), 0f);

    public int Count => _instances.Length;

    /// <summary>Every instance, in the order the grid files them: what the device is handed once (<see cref="TownRenderer.LayStanding"/>).</summary>
    public ReadOnlyMemory<SpriteInstance> Instances => _instances;

    /// <summary>How many instances a town's standing geometry needs, which is what the buffer is laid for.</summary>
    public static int CapacityFor(CityPlan plan) => plan.Buildings.Count + plan.Props.Count;

    /// <param name="level">The level of the grid the instances are filed at — its main one.</param>
    public static StandingSprites Lay(
        CityPlan plan, BuildingCatalog buildings, BuildingUses uses, PropCatalog props, int firstBuildingSheet,
        int firstPropSheet, ReadOnlySpan<float> aspects, GridLevel level)
    {
        var window = GridWindow.Over(level, Vector2.Zero, Vector2.Max(plan.WorldSizeM, Vector2.Zero));

        var total = CapacityFor(plan);
        var cells = new int[window.Count];
        var cellOf = new int[total];
        var instances = new SpriteInstance[total];
        var reachM = 0f;

        var written = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            instances[written] = Roof(plan, buildings, uses, firstBuildingSheet, building);
            written = Filed(instances, cellOf, cells, window, written, ref reachM);
        }

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            instances[written] = Look(plan, props, firstPropSheet, aspects, prop);
            written = Filed(instances, cellOf, cells, window, written, ref reachM);
        }

        // A counting sort, because the town is laid once and read sixty times a second: the offsets are
        // the running total, and the second pass drops each instance where its cell says.
        var offsets = new int[cells.Length + 1];
        for (var cell = 0; cell < cells.Length; cell++) offsets[cell + 1] = offsets[cell] + cells[cell];

        var next = (int[])offsets.Clone();
        var sorted = new SpriteInstance[total];
        for (var instance = 0; instance < total; instance++) sorted[next[cellOf[instance]]++] = instances[instance];

        var rowStarts = new int[window.Height + 1];
        for (var row = 0; row < window.Height; row++) rowStarts[row] = offsets[window.IndexOf(window.FromX, window.FromY + row)];
        rowStarts[window.Height] = total;

        return new StandingSprites(sorted, rowStarts, window, reachM);
    }

    /// <summary>One instance filed under the cell its centre is in, and how far it reaches taken into the widest.</summary>
    static int Filed(
        SpriteInstance[] instances, int[] cellOf, int[] cells, GridWindow window, int written, ref float reachM)
    {
        ref readonly var instance = ref instances[written];
        reachM = MathF.Max(reachM, instance.HalfSizeM.Length());
        cellOf[written] = window.IndexAt(instance.CentreM);
        cells[cellOf[written]]++;
        return written + 1;
    }

    /// <summary>
    /// <b>The stretch of <see cref="Instances"/> a view can see</b>: whole rows, from the first the view reaches to
    /// the last, which are one stretch because the instances are laid row after row. What stands past the view's
    /// sides in those rows is drawn and clipped, which costs the device less than choosing it would cost here.
    /// </summary>
    public (int First, int Count) Range(Vector2 viewCentreM, Vector2 viewSpanM)
    {
        if (_instances.Length == 0) return (0, 0);

        // A row is drawn whole, so the margin only has to cover a body standing outside its own row, which is
        // as far as the widest instance reaches from its centre.
        var half = (viewSpanM * 0.5f) + new Vector2(_reachM);
        _window.TryRange(viewCentreM - half, viewCentreM + half, out var inView);

        var first = _rowStarts[inView.FromY - _window.FromY];
        return (first, _rowStarts[inView.ToY - _window.FromY + 1] - first);
    }

    /// <summary>
    /// One building's roof, drawn where <see cref="BuildingRoofs"/> says it stands. <b>The choice is not
    /// made here</b>: the same answer stands this building's walls, and two constructions of it would be
    /// two buildings.
    /// </summary>
    static SpriteInstance Roof(
        CityPlan plan, BuildingCatalog catalogue, BuildingUses uses, int firstSheet, int building)
    {
        var roof = BuildingRoofs.Of(plan, catalogue, uses, building);
        return new SpriteInstance(
            plan.Buildings.CentreM[building], roof.FootprintM * 0.5f, Vector2.Zero, Vector2.One,
            PersonSprites.Plain, (uint)(firstSheet + roof.Variant), roof.HeadingRad);
    }

    static SpriteInstance Look(
        CityPlan plan, PropCatalog catalogue, int firstSheet, ReadOnlySpan<float> aspects, int prop)
    {
        var diameterM = plan.Props.RadiusM[prop] * 2f;
        var variant = catalogue.Look(plan.Props.Kind[prop], diameterM, prop);

        // <b>The picture fits inside the disc the town kept for it</b> (GEN-6d): the longest side of the
        // sheet is the prop's own diameter and the other follows its aspect, so what is drawn is what a car
        // is held off. Drawn <em>diameterM tall</em> instead, a sheet half again as wide as it is high
        // reaches half a metre past its own girth and stands in the next prop along.
        var look = catalogue.Variants[variant];
        var aspect = aspects[firstSheet + variant];
        var sizeM = new Vector2(aspect, 1f) * (diameterM / MathF.Max(aspect, 1f));

        // <b>The bearing is the plan's and whether it is used is the look's</b> (GEN-6b). A prop laid along
        // a kerb carries the road's own bearing there, and a look with a front is turned onto it; a tree
        // seen from above has no front, so it is drawn upright and the same look does not read as several.
        return new SpriteInstance(
            plan.Props.CentreM[prop], sizeM * 0.5f, Vector2.Zero, Vector2.One,
            PersonSprites.Plain, (uint)(firstSheet + variant), look.Turns ? plan.Props.BearingRad[prop] : 0f);
    }
}
