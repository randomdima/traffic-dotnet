using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The outside of the driven ground, merged out of the ribbons its lines lay</b>
/// (<see cref="LaneShell"/>): that it closes, and that it really is the outside.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class LaneShellTests
{
    /// <summary>
    /// <b>The boundary of a union of closed bands is closed</b>, so every stretch the merge keeps is in a
    /// ring and none is left with two ends (<see cref="LaneShell.Loose"/>). A run that does not shut is a
    /// crossing the merge did not find, and it is a length of the town's edge nothing accounts for.
    /// </summary>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void EveryRunShuts(string map)
    {
        var config = SimConfig.Shipped();
        var shell = Towns.Of(map).Paving(config).Perimeter(config);
        var lengthM = 0f;
        foreach (var run in shell.Loose) lengthM += Spline.TotalLengthM(run);

        Assert.True(
            shell.Loose.Length == 0,
            $"{map} left {shell.Loose.Length} runs of boundary open, {lengthM:F3} m in all, the first of "
            + $"them from {(shell.Loose.Length > 0 ? shell.Loose[0][0].StartM : Vector2.Zero)}");
    }

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
        // Wide enough to clear the last bits of a float at a town's coordinates and far under anything the
        // town lays, which is metres wide.
        const float HairM = 0.02f;

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
