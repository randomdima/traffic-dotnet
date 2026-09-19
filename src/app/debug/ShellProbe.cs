using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>OBS-2w — the one shape a debug session strikes for itself</b>: the driven ground's boundary moved
/// out by a distance the reader turns, drawn beside the layers the town was really laid with (OBS-2u) and
/// never taken for one of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It asks what the shipped layers cannot be asked</b> — how the outset answers at a distance nothing in
/// the town uses: where it swallows a corner, where two kerbs come back as one line, and how far out the
/// shape stops resembling the boundary it was struck from. A second set of layers under the perimeter's own
/// switch would be a picture of ground nobody stands on (OBS-2u); under a switch of its own it is the whole
/// reading.
/// </para>
/// <para>
/// <b>Struck once per distance and kept</b>: an outset is cut against the whole town's shape (TER-7b) and
/// costs tens of milliseconds on a city, so the rings are held against the shell they came off and the
/// distance they were struck at. <b>And the distance moves in steps</b> (<see cref="StepM"/>), so a drag
/// across the track asks a few dozen of those questions rather than one a pixel.
/// </para>
/// <para>
/// <b>And the rounding is the second figure, because it is the second half of the answer</b>
/// (<see cref="RoundedM"/>). The move leaves a corner turning in on the shape as sharp as the fold cut it
/// and the rounding is what takes it off, so a reader with one figure and not the other cannot tell a notch
/// the distance made from one the rounding left. <b>It is a radius in metres and turns free of the
/// distance</b>: every line the town lays is struck at one radius of its own
/// (<c>Core.Config.RoadFigures.LineRoundedM</c>, TER-3c.10), and what the row is dragged past that figure
/// for is the reading nothing shipped can give — a radius past the distance, where the corners the town
/// turns away at are cut round as well.
/// </para>
/// </remarks>
internal sealed class ShellProbe
{
    /// <summary>Whether it is drawn, which is its row on the debug page — off, as every layer is (OBS-2b).</summary>
    public bool Drawn;

    /// <summary>
    /// What the figures page calls the distance, beside the figures the build ships
    /// (<c>Core.Config.TrimFigures.Names</c>). <b>Printable ASCII only</b>, as every string the interface
    /// draws is.
    /// </summary>
    public const string Named = "Shell probe";

    /// <summary>And what the row that turns the rounding is called, the two standing together on that page.</summary>
    public const string NamedRounding = "Shell rounding";

    /// <summary>How far out the shape standing was struck, which is what the slider on the figures page turns.</summary>
    public float OutwardM => _outwardM;

    /// <summary>
    /// <b>How far out it may be taken</b>: nothing, where the answer should be the boundary itself and the
    /// reading is whether it is, out to a distance several streets wide — far enough that a town comes back
    /// as a handful of blobs and every corner and gap in it has been swallowed, which is the far end of what
    /// there is to see.
    /// </summary>
    public const float LeastM = 0f;

    public const float MostM = 20f;

    /// <summary>
    /// Where it stands before anybody turns it: just outside the walk the town ships, so the first picture
    /// is the probe against the layers it is read beside rather than a line on top of one of them.
    /// </summary>
    public const float StartsAtM = 5f;

    /// <summary>
    /// <b>The step the distance moves in.</b> A drag is a pixel at a time and an outset is the town's whole
    /// shape, so a track read straight off the pointer would strike two hundred shells across one sweep of
    /// a city; a tenth of a metre is finer than the thing being looked at and coarse enough to drag.
    /// </summary>
    public const float StepM = 0.1f;

    /// <summary>
    /// <b>How tightly the shape struck is allowed to turn anywhere, in metres of radius</b>
    /// (<c>Core.Geometry.ArcOutset.Of</c>): at nought the notch a fold cut is left as sharp as it was cut,
    /// and past the distance moved the corners the town turns away at are cut round as well.
    /// </summary>
    /// <remarks>
    /// <b>It is a radius and not a share of the distance</b>, so the two rows ask two questions instead of
    /// one: how far off the town the line stands, and how rugged it is allowed to be. Tied to the distance,
    /// the reading at a hand's breadth out was a hand's breadth of radius — a line exactly as rugged as the
    /// town whatever the row was dragged to, which is what sent this one back to the geometry.
    /// </remarks>
    public float RoundedM => _roundedM;

    /// <summary>
    /// Where the rounding stands before anybody turns it: none, which is how the town's own ground layers
    /// are struck, so the first picture is the answer the construction gives rather than a tidied one.
    /// </summary>
    public const float StartsRoundM = 0f;

    /// <summary>
    /// <b>The whole of the range it has</b>: from a line rounded nowhere out to a radius that swallows a
    /// street corner whole, which is the far end of what a rounding has to show.
    /// </summary>
    public const float LeastRoundM = 0f;

    public const float MostRoundM = 20f;

    /// <summary>The step it moves in, on the same reasoning as <see cref="StepM"/>: a shell a step and not a pixel.</summary>
    public const float StepRoundM = 0.1f;

    /// <summary>
    /// A number that changes whenever the distance does, which <see cref="DebugSwitches.Generation"/>
    /// carries: the layer is drawn into the cache the town's own geometry is held in, so a distance moved
    /// has to lay that cache again exactly as a switch does.
    /// </summary>
    public int Generation { get; private set; }

    ArcSeg[][] _rings = [];
    ArcSeg[][] _loose = [];
    BandShell? _struckOff;
    float _struckAtM = float.NaN;
    float _struckRoundM = float.NaN;

    /// <summary>The distance to where the pointer put it, in steps and inside the stops.</summary>
    public void SetOutwardM(float outwardM) =>
        Moved(ref _outwardM, outwardM, LeastM, MostM, StepM);

    /// <summary>And the rounding, on the same terms.</summary>
    public void SetRoundedM(float roundedM) =>
        Moved(ref _roundedM, roundedM, LeastRoundM, MostRoundM, StepRoundM);

    /// <summary>
    /// <b>The shape at the two figures standing</b>, struck off <paramref name="shell"/> the first time any
    /// of the three changes and handed back as it stands after that — the rings the move closed, and the
    /// runs it could not (<see cref="BandShell.Loose"/>).
    /// </summary>
    public (ArcSeg[][] Rings, ArcSeg[][] Loose) Off(BandShell shell)
    {
        if (ReferenceEquals(shell, _struckOff) && _struckAtM == OutwardM && _struckRoundM == RoundedM)
        {
            return (_rings, _loose);
        }

        (_rings, _loose) = shell.Outset(OutwardM, RoundedM);
        _struckOff = shell;
        _struckAtM = OutwardM;
        _struckRoundM = RoundedM;
        return (_rings, _loose);
    }

    /// <summary>
    /// One figure to where the pointer put it, in its own steps and inside its own stops. <b>A figure that
    /// did not move is not a shape to strike</b>: the drag is read every frame and a step is the whole town's
    /// shape cut against itself.
    /// </summary>
    void Moved(ref float figure, float to, float least, float most, float step)
    {
        var held = MathF.Round(Math.Clamp(to, least, most) / step) * step;
        if (held == figure) return;

        figure = held;
        Generation++;
    }

    float _outwardM = StartsAtM;
    float _roundedM = StartsRoundM;
}
