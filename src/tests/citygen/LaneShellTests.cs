using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The outside of the driven ground, merged out of the ribbons its lines lay</b>
/// (<see cref="LaneShell"/>): that it really is the outside, and that it turns only where the ground does.
/// </summary>
/// <remarks>
/// <b>That it closes at all is the shallow bar's</b> (<see cref="Conformance.ItsBoundaryCloses"/>), because
/// it is a question about a whole town rather than about this construction: every map this build lays is
/// held to it, and so is every city somebody ships.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
[Collection(TownGeometryCollection.Name)]
public class LaneShellTests
{
    /// <summary>
    /// <b>The driven ground is on the boundary's right and nothing is driven on its left</b> (TER-3c.9) —
    /// which is the whole of what a boundary claims, and the one thing a merge that dropped the wrong piece
    /// cannot satisfy.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the bands and not of a second boundary.</b> Whether a place is driven ground is whether
    /// some line passes within half its own width of it, which is what a band <em>is</em> — so a station
    /// that fails this is the merge disagreeing with the shapes it was merged out of rather than two
    /// constructions disagreeing with each other.
    /// </remarks>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void TheGroundIsInsideTheBoundaryAndNotOutsideIt(string map)
    {
        // <b>Wide enough to clear what closing a ring's joints moves a piece by</b>
        // (<see cref="ArcRings.Tightened"/>): a joint is closed onto the middle of the two ends standing at
        // it, so half a weld is the most any piece is drawn off the band it is the edge of. Narrower than
        // that, this asks the shape for a precision it does not claim; it is still far under anything the
        // town lays, which is metres wide.
        const float HairM = ArcRings.WeldM * 0.5f;

        // <b>Asked at the middle of each stretch and never at its ends</b>, and only of the stretches long
        // enough to have a middle: a corner is where two stretches meet and the ground within a hair of one
        // is on either side of it, so a station there answers about the corner rather than about the
        // boundary.
        const float LeastStretchM = 0.5f;

        var config = SimConfig.Shipped();
        var paving = Towns.Of(map).Paving(config);
        var shell = paving.Perimeter(config);
        var bands = new Bands(paving, config);

        var wrong = 0;
        var asked = 0;
        var firstM = Vector2.Zero;
        foreach (var ring in shell.Chains)
        {
            foreach (var stretch in ring)
            {
                if (stretch.LengthM < LeastStretchM) continue;

                var middleM = stretch.LengthM * 0.5f;
                var atM = stretch.PointAtM(middleM);
                var rightM = Heading.RightOf(Heading.Unit(stretch.HeadingAtRad(middleM)));
                asked++;
                if (bands.Cover(atM + (rightM * HairM)) && !bands.Cover(atM - (rightM * HairM))) continue;

                if (wrong++ == 0) firstM = atM;
            }
        }

        Assert.True(asked > 0, $"{map} laid no boundary to ask about");
        Assert.True(
            wrong == 0,
            $"{map}: {wrong} of {asked} boundary stations do not have the driven ground on their right and "
            + $"nothing on their left, the first at {firstM}");
    }

    /// <summary>
    /// <b>A ring turns only where the ground does</b>: no piece of a ring carries on into the piece after
    /// it, the seam between its last and its first included. A merge cuts a ribbon wherever anything else
    /// crosses it, which is mostly places the boundary carries straight on, and what those cuts leave is a
    /// point a reader downstream has to work out was never a corner.
    /// </summary>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void ARingTurnsOnlyWhereTheGroundDoes(string map)
    {
        const float JoinM = LineTolerance.RoundingM;

        var config = SimConfig.Shipped();
        var shell = Towns.Of(map).Paving(config).Perimeter(config);
        var carriedOn = 0;
        var pieces = 0;
        var firstM = Vector2.Zero;
        foreach (var ring in shell.Chains)
        {
            pieces += ring.Length;
            for (var at = 0; at < ring.Length && ring.Length > 1; at++)
            {
                var before = ring[(at + ring.Length - 1) % ring.Length];
                if (!Spline.CarriesOn(before, ring[at], JoinM, out _)) continue;

                if (carriedOn++ == 0) firstM = ring[at].StartM;
            }
        }

        Assert.True(pieces > 0, $"{map} laid no boundary to ask about");
        Assert.True(
            carriedOn == 0,
            $"{map}: {carriedOn} of {pieces} pieces of the boundary carry on from the piece before them "
            + $"rather than turning at it, the first at {firstM}");
    }

    /// <summary>
    /// <b>A ring has no hole in it</b> (<see cref="ArcRings.Tightened"/>): at every hand-over, one of the two
    /// pieces meeting there covers the place between their two ends — which is what a kerb struck down the
    /// ring, or a line drawn of it piece by piece, runs over rather than breaks at.
    /// </summary>
    /// <remarks>
    /// <b>Asked at the gap's own middle and not of how far the two ends stand apart.</b> Two pieces that
    /// overrun each other stand as far apart as two that fall short of each other, and only the second is a
    /// break in anything laid along the ring.
    /// </remarks>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void ARingHasNoHoleInIt(string map)
    {
        // What a frame can show: a centimetre is under a pixel until a metre covers a hundred of them, and
        // a twentieth of the narrowest thing the town lays beside a road.
        const float SeenM = 0.01f;

        var config = SimConfig.Shipped();
        var shell = Towns.Of(map).Paving(config).Perimeter(config);
        var holes = 0;
        var joints = 0;
        var worstM = 0f;
        var firstM = Vector2.Zero;
        foreach (var ring in shell.Chains)
        {
            for (var at = 0; at < ring.Length; at++)
            {
                var arriving = ring[at];
                var leaving = ring[(at + 1) % ring.Length];
                var middleM = (arriving.EndM + leaving.StartM) * 0.5f;
                joints++;

                var offM = MathF.Min(Off(arriving, middleM), Off(leaving, middleM));
                if (offM <= SeenM) continue;

                if (holes++ == 0) firstM = arriving.EndM;

                worstM = MathF.Max(worstM, offM);
            }
        }

        Assert.True(joints > 0, $"{map} laid no boundary to ask about");
        Assert.True(
            holes == 0,
            $"{map}: {holes} of {joints} joints of the boundary are a hole neither of the pieces meeting "
            + $"there covers, the worst {worstM * 1000f:F1} mm and the first at {firstM}");
    }

    /// <summary>How far a place stands off one piece, measured to the piece and not to the circle it lies on.</summary>
    static float Off(in ArcSeg piece, Vector2 pointM)
    {
        var alongM = Spline.ProjectM([piece], pointM, piece.LengthM * 0.5f, piece.LengthM);
        return Vector2.Distance(piece.PointAtM(alongM), pointM);
    }

    /// <summary>
    /// Whether any line's band covers a place, which is the definition of the driven ground — <b>at its
    /// own width with the arithmetic's rounding on it</b> (<see cref="LineTolerance.RoundingM"/>), and off its
    /// square ends not at all.
    /// </summary>
    /// <remarks>
    /// <b>A place on the seam two bands share is on both of them and not on neither.</b> A row of bays
    /// stands side by side and two lanes of a carriageway share the edge between them, so the town is full
    /// of places that are strictly inside neither band by a fraction of a millimetre either way — and the
    /// boundary runs along a seam like that wherever the bands under it are one shape. A station landing
    /// on one is a question about a tie rather than about the merge, and the millimetre is what settles it
    /// the way the merge itself settles one.
    /// </remarks>
    sealed class Bands
    {
        readonly Paving _paving;
        readonly ChainIndex _lines;
        readonly int[] _near;
        readonly float[] _alongM;
        readonly float _widestM;

        public Bands(Paving paving, SimConfig config)
        {
            _paving = paving;
            _lines = paving.DrivenLines(config);
            _near = new int[paving.DrivenCount];
            _alongM = new float[paving.DrivenCount];
            for (var line = 0; line < paving.DrivenCount; line++)
            {
                _widestM = MathF.Max(_widestM, paving.DrivenWidthM(line) * 0.5f);
            }
        }

        public bool Cover(Vector2 pointM)
        {
            const float RoundingM = LineTolerance.RoundingM;

            var found = _lines.Near(pointM, _widestM + RoundingM, _near, _alongM);
            for (var at = 0; at < found && at < _near.Length; at++)
            {
                var line = _near[at];
                var alongM = _alongM[at];

                // A band has square ends, so the ground off the end of a line is not the line's however
                // near it stands.
                if (alongM <= 0f || alongM >= _paving.DrivenLengthM(line)) continue;

                var halfM = _paving.DrivenWidthM(line) * 0.5f + RoundingM;
                var onM = Spline.SampleAt(_paving.ArcsOfDriven(line), alongM).PositionM;
                if (Vector2.DistanceSquared(onM, pointM) < halfM * halfM) return true;
            }

            return false;
        }
    }
}
