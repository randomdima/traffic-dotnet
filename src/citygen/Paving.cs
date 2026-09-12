using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The pavement, laid once as the band the walk runs down</b> (TER-3c): the town's own tarmac —
/// <see cref="Kerbs"/>'s list — wrapped at half a walk, and the runs of that wrap that are really the
/// outside of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a step and not a derivation two readers each do for themselves.</b> The shape of the pavement
/// used to be written down twice — once as the draw calls that lay it (<c>GroundMesh.Build</c>) and once as
/// the coverage tests that answer what the ground is at a point (<see cref="GroundShapes"/>) — in two
/// slices, at two tiers, kept in step by nobody but whoever remembered. That is the figure that exists in
/// two places and eventually disagrees with itself: a band widened in the picture and not in the answer is
/// a walker refused ground it can see it is standing on. Laid here, the two read one list.
/// </para>
/// <para>
/// <b>The pavement is the ground within half a walk of <see cref="Walk"/>, and it is nothing else</b>
/// (TER-3c.3). That line is the town's tarmac wrapped at half a walk and cut to the runs no tarmac stands
/// nearer to — <em>the same line the walking lanes are laid on</em> — so the band is a walk wide the whole
/// way round, the kerb is its inner edge, the shell against the grass is its outer edge, and a walker walks
/// down the middle of it.
/// </para>
/// <para>
/// <b>The carriageway ends where the pavement starts</b> (TER-3c.7). Everything inside the kerb is tarmac,
/// including the pockets the town's own pieces leave between them — a movement narrower than the arm it
/// leaves, a car park set back off the street it fronts.
/// </para>
/// <para>
/// <b>A junction has no piece here, because a junction has no shape</b> (TER-5): the ground inside a box is
/// the ground its own movements take (<see cref="LaneLines"/>) and the fillets that round the wedges
/// between its arms, and each of those is wrapped like any other piece of tarmac.
/// </para>
/// <para>
/// <b>Nothing here is the picture's.</b> The drawing is a stack of layers and a layer is the union of the
/// shapes in it (TER-7b), so it takes the same pieces at three sizes and works nothing out about how they
/// meet — where it once needed each road cut into the stretches its sides did not change over, each
/// junction's box walked as one outline, and every hand-over between two runs closed with a wedge, a round
/// or a bridge.
/// </para>
/// </remarks>
internal sealed class Paving
{
    readonly float _bayWidthM;

    Paving(float walkM, GroundPieces pieces, LaneLines lanes, BayLines bays, float bayWidthM, Kerbs kerbs)
    {
        WalkM = walkM;
        Of = pieces;
        Lanes = lanes;
        Bays = bays;
        _bayWidthM = bayWidthM;
        Kerbs = kerbs;
    }

    /// <summary>The shapes the pavement was laid off, for a reader that wants the road a band belongs to.</summary>
    public GroundPieces Of { get; }

    /// <summary>
    /// <b>The lines the town is driven on</b>, laid once here and read by everything that needs the tarmac's
    /// own shape rather than the records it was drawn from.
    /// </summary>
    public LaneLines Lanes { get; }

    /// <summary>
    /// <b>The lines a car is driven into and out of a bay on</b> (<see cref="BayLines"/>). A car park is
    /// the union of these and has no shape of its own, so nothing that draws or answers ground carries one.
    /// </summary>
    public BayLines Bays { get; }

    /// <summary>
    /// <b>Every movement in the town, in one numbering</b>: the lines cars are turned through a box on,
    /// then the lines they are driven into and out of a bay on. A junction is the union of the first
    /// (TER-5) and a car park the union of the second, so whatever draws or answers either walks this and
    /// has no case for one.
    /// </summary>
    public int MovementCount => Lanes.ConnectorCount + Bays.GroundWays.Length;

    public ReadOnlySpan<ArcSeg> ArcsOfMovement(int movement) =>
        movement < Lanes.ConnectorCount
            ? Lanes.ArcsOfConnector(movement)
            : Bays.ArcsOf(Bays.GroundWays[movement - Lanes.ConnectorCount]);

    /// <summary>How wide the ground one movement is driven over is: a lane's share, or a bay's own space.</summary>
    public float MovementWidthM(int movement) =>
        movement < Lanes.ConnectorCount ? Lanes.ConnectorWidthM(movement) : _bayWidthM;

    /// <summary>The movement's own metres, end to end.</summary>
    public float MovementLengthM(int movement) =>
        movement < Lanes.ConnectorCount
            ? Lanes.ConnectorLengthM[movement]
            : Bays.LengthM[Bays.GroundWays[movement - Lanes.ConnectorCount]];

    /// <summary>
    /// The junction a movement crosses, or <see cref="CityPlan.NoRecord"/> where it crosses none — which a
    /// bay's way never does.
    /// </summary>
    public int JunctionOfMovement(int movement) =>
        movement < Lanes.ConnectorCount ? Lanes.JunctionOfConnector(movement) : CityPlan.NoRecord;

    /// <summary>
    /// <b>Every line the town is driven on, in one numbering</b>: the lanes, then the movements
    /// (<see cref="MovementCount"/>). A lane and never a carriageway — the tarmac's own piece for a street
    /// is one band about its middle, and the middle of a two-lane street is ground nobody drives.
    /// </summary>
    public int DrivenCount => Lanes.LaneCount + MovementCount;

    public ReadOnlySpan<ArcSeg> ArcsOfDriven(int line) =>
        line < Lanes.LaneCount ? Lanes.ArcsOf(line) : ArcsOfMovement(line - Lanes.LaneCount);

    /// <summary>How wide the ground one driven line is driven over is, which is the band it lays.</summary>
    public float DrivenWidthM(int line) =>
        line < Lanes.LaneCount ? Lanes.LaneWidthM[line] : MovementWidthM(line - Lanes.LaneCount);

    public float DrivenLengthM(int line) =>
        line < Lanes.LaneCount ? Lanes.LaneLengthM[line] : MovementLengthM(line - Lanes.LaneCount);

    LaneShell? _perimeter;

    /// <summary>
    /// <b>The outside of the driven ground, as stretches of the lines themselves</b>
    /// (<see cref="LaneShell"/>), laid on the first ask and not before: nothing the town needs to be laid
    /// reads it, and working it out costs a walk of every line against the shape it is part of.
    /// </summary>
    public LaneShell Perimeter(SimConfig config) => _perimeter ??= LaneShell.Of(this, config);

    /// <summary>The tarmac as one shape, for whoever wants to ask how far off it a point stands.</summary>
    public Kerbs Kerbs { get; }

    /// <summary>
    /// How wide the band is. <b>The map's own figure where it has one</b>, and the town's where it does not,
    /// so a map laid without a pavement of its own is walked at the same width it is drawn.
    /// </summary>
    public float WalkM { get; }

    public static Paving Lay(GroundPieces pieces, SimConfig config)
    {
        var walkM = pieces.PavementWidthM > 0f ? pieces.PavementWidthM : config.PavementWidthM;
        var lanes = LaneLines.Of(pieces, config);
        var bays = BayLines.Lay(pieces, lanes, config);

        // The junctions a road runs through as one line (<see cref="RoadCuts.RunsThrough"/>): a movement
        // through one stands inside the two arms' own bands, so it is not a piece of the outline.
        var through = RoadCuts.RunsThrough(pieces);
        var kerbs = Kerbs.Of(pieces, lanes, bays, config.ParkingSpaceWidthM, through);

        return new Paving(walkM, pieces, lanes, bays, config.ParkingSpaceWidthM, kerbs);
    }
}
