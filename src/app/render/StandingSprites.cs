using System.Numerics;
using TrafficSimulation.CityGen;
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
/// cells are laid row-major, so the cells in view on one row are contiguous and the copy is a
/// <c>Span.CopyTo</c>.
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
    /// <summary>
    /// How wide a cell of the cull grid is. Big enough that a city is a few thousand cells rather than a
    /// hundred thousand, small enough that a street framing does not sweep a district.
    /// </summary>
    const float CellM = 32f;

    readonly SpriteInstance[] _instances;
    readonly int[] _cellOffsets;
    readonly int _columns;
    readonly int _rows;

    StandingSprites(SpriteInstance[] instances, int[] cellOffsets, int columns, int rows)
    {
        _instances = instances;
        _cellOffsets = cellOffsets;
        _columns = columns;
        _rows = rows;
    }

    public static StandingSprites Nothing { get; } = new([], [0], 1, 1);

    public int Count => _instances.Length;

    /// <summary>How many instances a town's standing geometry needs, which is what the buffer is laid for.</summary>
    public static int CapacityFor(CityPlan plan) => plan.Buildings.Count + plan.Props.Count;

    public static StandingSprites Lay(
        CityPlan plan, BuildingCatalog buildings, BuildingUses uses, PropCatalog props, int firstBuildingSheet,
        int firstPropSheet, ReadOnlySpan<float> aspects)
    {
        var columns = Math.Max(1, (int)MathF.Ceiling(plan.WorldSizeM.X / CellM));
        var rows = Math.Max(1, (int)MathF.Ceiling(plan.WorldSizeM.Y / CellM));

        var total = CapacityFor(plan);
        var cells = new int[columns * rows];
        var cellOf = new int[total];
        var instances = new SpriteInstance[total];

        var written = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            instances[written] = Roof(plan, buildings, uses, firstBuildingSheet, building);
            cellOf[written] = Cell(instances[written].CentreM, columns, rows);
            cells[cellOf[written]]++;
            written++;
        }

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            instances[written] = Look(plan, props, firstPropSheet, aspects, prop);
            cellOf[written] = Cell(instances[written].CentreM, columns, rows);
            cells[cellOf[written]]++;
            written++;
        }

        // A counting sort, because the town is laid once and read sixty times a second: the offsets are
        // the running total, and the second pass drops each instance where its cell says.
        var offsets = new int[cells.Length + 1];
        for (var cell = 0; cell < cells.Length; cell++) offsets[cell + 1] = offsets[cell] + cells[cell];

        var next = (int[])offsets.Clone();
        var sorted = new SpriteInstance[total];
        for (var instance = 0; instance < total; instance++) sorted[next[cellOf[instance]]++] = instances[instance];

        return new StandingSprites(sorted, offsets, columns, rows);
    }

    /// <summary>Copies every standing instance whose cell the view touches, and answers how many.</summary>
    public int Fill(Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        if (_instances.Length == 0) return 0;

        // A cell is emitted whole, so the margin only has to cover a body standing outside its own cell:
        // half a roof is the widest thing in the town.
        var half = viewSpanM * 0.5f + new Vector2(CellM * 0.5f);
        var from = (viewCentreM - half) / CellM;
        var to = (viewCentreM + half) / CellM;

        var firstColumn = Math.Clamp((int)MathF.Floor(from.X), 0, _columns - 1);
        var lastColumn = Math.Clamp((int)MathF.Floor(to.X), 0, _columns - 1);
        var firstRow = Math.Clamp((int)MathF.Floor(from.Y), 0, _rows - 1);
        var lastRow = Math.Clamp((int)MathF.Floor(to.Y), 0, _rows - 1);

        var written = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var start = _cellOffsets[(row * _columns) + firstColumn];
            var end = _cellOffsets[(row * _columns) + lastColumn + 1];
            var run = end - start;
            if (run <= 0) continue;

            if (written + run > into.Length) run = into.Length - written;
            if (run <= 0) break;

            _instances.AsSpan(start, run).CopyTo(into[written..]);
            written += run;
        }

        return written;
    }

    static int Cell(Vector2 centreM, int columns, int rows)
    {
        var column = Math.Clamp((int)MathF.Floor(centreM.X / CellM), 0, columns - 1);
        var row = Math.Clamp((int)MathF.Floor(centreM.Y / CellM), 0, rows - 1);
        return (row * columns) + column;
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

        // The art is drawn upright and never turned: a tree seen from above has no bearing, and turning
        // one would make the same look read as several.
        var aspect = aspects[firstSheet + variant];
        return new SpriteInstance(
            plan.Props.CentreM[prop], new Vector2(diameterM * aspect, diameterM) * 0.5f, Vector2.Zero, Vector2.One,
            PersonSprites.Plain, (uint)(firstSheet + variant), 0f);
    }
}
