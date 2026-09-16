using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The pavement, laid once as the band the walk runs down</b> (TER-3c): the town's own tarmac — the
/// ribbons the driven lines lay (<see cref="LaneShell"/>) — wrapped at half a walk, and the runs of that
/// wrap that are really the outside of it.
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
/// <b>The pavement is the ground within half a walk of the half-walk line, and it is nothing else</b>
/// (TER-3c.3). That line is the town's tarmac wrapped at half a walk (<see cref="Perimeter"/>) and cut to
/// the runs no tarmac stands nearer to — <em>the same line the walking lanes are laid on</em> — so the band is a walk wide the whole
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
    Paving(GroundPieces pieces, LaneLines lanes)
    {
        Of = pieces;
        Lanes = lanes;
    }

    /// <summary>The shapes the pavement was laid off, for a reader that wants the road a band belongs to.</summary>
    public GroundPieces Of { get; }

    /// <summary>
    /// <b>The lines the town is driven on</b>, laid once here and read by everything that needs the tarmac's
    /// own shape rather than the records it was drawn from.
    /// </summary>
    public LaneLines Lanes { get; }

    /// <summary>
    /// <b>Every movement in the town</b>: the lines cars are turned through a box on. A junction is the
    /// union of them (TER-5), so whatever draws or answers one walks this and has no case for a junction.
    /// </summary>
    public int MovementCount => Lanes.ConnectorCount;

    public ReadOnlySpan<ArcSeg> ArcsOfMovement(int movement) => Lanes.ArcsOfConnector(movement);

    /// <summary>
    /// <b>How wide the ground one movement is driven over is, which is a lane's width either way</b>
    /// (GEN-4c): the narrower of the two lanes it joins.
    /// </summary>
    public float MovementWidthM(int movement) => Lanes.ConnectorWidthM(movement);

    /// <summary>The movement's own metres, end to end.</summary>
    public float MovementLengthM(int movement) => Lanes.ConnectorLengthM[movement];

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

    ChainIndex? _drivenLines;

    /// <summary>
    /// <b>Every driven line over the town's own geometry grid</b> (<see cref="ChainIndex"/>): the one index
    /// that holds all of them — every lane, every movement through a box and every way into a bay — so that
    /// which lines are near a place, or could cross one, costs the cells round it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Kept here because the lines are kept here.</b> It was laid inside the shell's own walk and thrown
    /// away with it, which meant the only complete index of the town's geometry existed for the length of one
    /// method — so a second reader either indexed the same lines again or asked an index of some subset of
    /// them and got an answer about that subset. The lanes' own index (<c>RoadGraph</c>) is the one a tick
    /// asks and is right to hold lanes alone: "which lane is this car on" may not answer with a bay's way.
    /// </para>
    /// <para>
    /// Laid on the first ask, like the perimeter, and at the cell the town indexes everything at
    /// (<see cref="SimConfig.NearestChainCellM"/>) — which is what puts its cells on the same lattice as
    /// every other index's.
    /// </para>
    /// </remarks>
    public ChainIndex DrivenLines(SimConfig config)
    {
        if (_drivenLines is not null) return _drivenLines;

        var building = new ChainIndex.Builder();
        for (var line = 0; line < DrivenCount; line++)
        {
            building.Add(line, ArcsOfDriven(line), DrivenLengthM(line));
        }

        return _drivenLines = building.Seal(config.NearestChainCellM);
    }

    BandShell? _perimeter;

    /// <summary>
    /// <b>The outside of the driven ground, as the merge of the ribbons every line lays</b>
    /// (<see cref="LaneShell"/>), laid on the first ask and not before: nothing the town needs to be laid
    /// reads it, and working it out costs every ribbon cut against every ribbon near it.
    /// </summary>
    /// <remarks>
    /// <b>Kept here because it is the town's and not a reader's.</b> The merge is the same for everyone who
    /// asks, and the picture redrawing on a pan asks it every frame.
    /// </remarks>
    public BandShell Perimeter(SimConfig config) => _perimeter ??= LaneShell.Of(this, config);

    GroundRings? _rings;

    /// <summary>
    /// <b>The ground beside a road, as the boundary moved by the one figure that strikes a layer of it</b>
    /// (<see cref="GroundRings"/>, TER-3c.3, TER-7b) — laid on the first ask, for the reason
    /// <see cref="Perimeter"/> is and at a higher price: it is a move of the whole town's outline and a cut.
    /// </summary>
    /// <remarks>
    /// <b>Kept here because the boundary is kept here.</b> The layers are a fact about the town's own shell,
    /// so a reader that struck its own would be moving a boundary the town does not have.
    /// </remarks>
    public GroundRings Rings(SimConfig config) => _rings ??= GroundRings.Of(Perimeter(config), config);

    public static Paving Lay(GroundPieces pieces, SimConfig config) =>
        new(pieces, LaneLines.Of(pieces, config));
}
