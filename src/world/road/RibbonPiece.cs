using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One piece of a way's line as the ground it covers</b> — a straight or an arc swept to half the way's
/// width either side, square at both ends: a rectangle, or a sector of an annulus. Two ribbons share ground
/// exactly where two such pieces do (<see cref="RibbonMarks"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The ground across one metre of it is a slice</b> (<see cref="SliceAt"/>): the segment square to the
/// line there, or on an arc the stretch of the radius through it — which stops at the centre, since a point
/// beyond the centre is read at the other side of the circle (<see cref="RibbonAtlas"/>). Every point of the
/// piece lies on the slice of the one metre its foot is at, so <b>which metres of a piece meet some ground is
/// which slices do</b>, and that is one segment tested against one shape.
/// </para>
/// <para>
/// <b>Worked in doubles.</b> A road's bends are huge radii, and an arc's centre stands kilometres off the
/// ground being asked about: a squared distance to it in floats has nothing left of the metre in question.
/// </para>
/// </remarks>
internal readonly struct RibbonPiece
{
    readonly double _startX;
    readonly double _startY;
    readonly double _headingRad;
    readonly double _headingCos;
    readonly double _headingSin;
    readonly double _curvature;
    readonly double _centreX;
    readonly double _centreY;
    readonly double _radiusM;
    readonly double _sweepRad;
    readonly double _firstRadialX;
    readonly double _firstRadialY;
    readonly double _lastRadialX;
    readonly double _lastRadialY;

    /// <param name="way">The way the piece is of.</param>
    /// <param name="wayFromM">Where on that way the piece begins.</param>
    /// <param name="arc">The piece of the way's line.</param>
    /// <param name="halfM">Half the way's width.</param>
    public RibbonPiece(int way, float wayFromM, in ArcSeg arc, float halfM)
    {
        Way = way;
        WayFromM = wayFromM;
        LengthM = arc.LengthM;
        HalfM = halfM;
        _startX = arc.StartM.X;
        _startY = arc.StartM.Y;
        _headingRad = arc.HeadingRad;
        (_headingSin, _headingCos) = Math.SinCos(_headingRad);
        IsStraight = RibbonAtlas.IsStraight(arc);
        _curvature = IsStraight ? 0.0 : arc.Curvature;

        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        RibbonAtlas.Bound(arc, halfM, ref leastM, ref mostM);
        LeastM = leastM;
        MostM = mostM;
        if (IsStraight) return;

        var turnM = 1.0 / _curvature;
        _centreX = _startX - (turnM * _headingSin);
        _centreY = _startY + (turnM * _headingCos);
        _radiusM = Math.Abs(turnM);
        _sweepRad = Math.Abs(_curvature) * LengthM;
        _firstRadialX = (_startX - _centreX) / _radiusM;
        _firstRadialY = (_startY - _centreY) / _radiusM;
        var (turnSin, turnCos) = Math.SinCos(_curvature * LengthM);
        _lastRadialX = (_firstRadialX * turnCos) - (_firstRadialY * turnSin);
        _lastRadialY = (_firstRadialX * turnSin) + (_firstRadialY * turnCos);
    }

    public int Way { get; }

    /// <summary>Where on its way the piece begins, in the way's own metres.</summary>
    public float WayFromM { get; }

    public float LengthM { get; }

    /// <summary>Half the width the piece is swept to.</summary>
    public float HalfM { get; }

    public bool IsStraight { get; }

    /// <summary>The box round the ground it covers at <see cref="HalfM"/>.</summary>
    public Vector2 LeastM { get; }

    public Vector2 MostM { get; }

    public bool BoxMeets(in RibbonPiece other) =>
        LeastM.X <= other.MostM.X && other.LeastM.X <= MostM.X
        && LeastM.Y <= other.MostM.Y && other.LeastM.Y <= MostM.Y;

    /// <summary>
    /// <b>Whether two straights' lines stand further apart than <paramref name="apartM"/></b> at their nearest
    /// — so that no ground within half of that of either line can meet the other's.
    /// </summary>
    public static bool StraightsFurtherApart(in RibbonPiece one, in RibbonPiece other, double apartM)
    {
        var oneToX = one._startX + (one.LengthM * one._headingCos);
        var oneToY = one._startY + (one.LengthM * one._headingSin);
        var otherToX = other._startX + (other.LengthM * other._headingCos);
        var otherToY = other._startY + (other.LengthM * other._headingSin);

        var otherFromSide = Side(one._startX, one._startY, oneToX, oneToY, other._startX, other._startY);
        var otherToSide = Side(one._startX, one._startY, oneToX, oneToY, otherToX, otherToY);
        var oneFromSide = Side(other._startX, other._startY, otherToX, otherToY, one._startX, one._startY);
        var oneToSide = Side(other._startX, other._startY, otherToX, otherToY, oneToX, oneToY);
        if (otherFromSide * otherToSide < 0.0 && oneFromSide * oneToSide < 0.0) return false;

        var square = apartM * apartM;
        return SquareOff(other._startX, other._startY, one._startX, one._startY, oneToX, oneToY) > square
               && SquareOff(otherToX, otherToY, one._startX, one._startY, oneToX, oneToY) > square
               && SquareOff(one._startX, one._startY, other._startX, other._startY, otherToX, otherToY) > square
               && SquareOff(oneToX, oneToY, other._startX, other._startY, otherToX, otherToY) > square;

        // Which side of the line from one point to another a third stands.
        static double Side(double fromX, double fromY, double toX, double toY, double atX, double atY) =>
            ((toX - fromX) * (atY - fromY)) - ((toY - fromY) * (atX - fromX));

        // The square of how far a point stands from the nearest point of a segment.
        static double SquareOff(double atX, double atY, double fromX, double fromY, double toX, double toY)
        {
            var alongX = toX - fromX;
            var alongY = toY - fromY;
            var square = (alongX * alongX) + (alongY * alongY);
            var t = square == 0.0
                ? 0.0
                : Math.Clamp((((atX - fromX) * alongX) + ((atY - fromY) * alongY)) / square, 0.0, 1.0);
            var offX = atX - (fromX + (t * alongX));
            var offY = atY - (fromY + (t * alongY));
            return (offX * offX) + (offY * offY);
        }
    }

    /// <summary>
    /// <b>The ground across one metre of the piece</b>, at a half-width of <paramref name="halfM"/>: the
    /// segment from one edge to the other, square to the line — stopping at the centre of an arc tighter than
    /// the half-width.
    /// </summary>
    public void SliceAt(double onM, double halfM, out double fromX, out double fromY, out double toX, out double toY)
    {
        var (sin, cos) = Math.SinCos(_headingRad + (_curvature * onM));

        // The right of the heading (Heading.RightOf), which is the side an arc of positive curvature
        // turns towards and has its centre on.
        var rightX = -sin;
        var rightY = cos;
        double atX;
        double atY;
        var leftM = -halfM;
        var rightM = halfM;
        if (IsStraight)
        {
            atX = _startX + (onM * _headingCos);
            atY = _startY + (onM * _headingSin);
        }
        else
        {
            var turnM = 1.0 / _curvature;
            atX = _centreX - (turnM * rightX);
            atY = _centreY - (turnM * rightY);
            if (_curvature > 0.0) rightM = Math.Min(halfM, _radiusM);
            else leftM = -Math.Min(halfM, _radiusM);
        }

        fromX = atX + (leftM * rightX);
        fromY = atY + (leftM * rightY);
        toX = atX + (rightM * rightX);
        toY = atY + (rightM * rightY);
    }

    /// <summary>
    /// <b>Whether a segment meets the ground this piece covers at a half-width of <paramref name="halfM"/></b>,
    /// with <paramref name="trimM"/> taken off each end — edges and ends included, so ground that only
    /// touches it meets it.
    /// </summary>
    public bool Meets(double fromX, double fromY, double toX, double toY, double halfM, double trimM = 0.0)
    {
        var alongX = toX - fromX;
        var alongY = toY - fromY;
        var least = 0.0;
        var most = 1.0;
        if (IsStraight)
        {
            var cos = _headingCos;
            var sin = _headingSin;
            var offX = fromX - _startX;
            var offY = fromY - _startY;
            var onM = (offX * cos) + (offY * sin);
            var onStep = (alongX * cos) + (alongY * sin);
            var asideM = (cos * offY) - (sin * offX);
            var asideStep = (cos * alongY) - (sin * alongX);
            return Clip(onM - trimM, onStep, ref least, ref most)
                   && Clip(LengthM - trimM - onM, -onStep, ref least, ref most)
                   && Clip(halfM - asideM, -asideStep, ref least, ref most)
                   && Clip(halfM + asideM, asideStep, ref least, ref most);
        }

        var fromCentreX = fromX - _centreX;
        var fromCentreY = fromY - _centreY;
        var square = (alongX * alongX) + (alongY * alongY);
        var linear = 2.0 * ((alongX * fromCentreX) + (alongY * fromCentreY));
        var distanceM = Math.Sqrt((fromCentreX * fromCentreX) + (fromCentreY * fromCentreY));

        // Inside the outer circle is one run of the segment, and outside the inner one is all of that run
        // but a stretch in its middle — so the ring is at most two runs, each then held to the sector.
        var outerM = _radiusM + halfM;
        if (!Roots(square, linear, (distanceM - outerM) * (distanceM + outerM), out var inFromT, out var inToT))
        {
            return false;
        }

        least = Math.Max(least, inFromT);
        most = Math.Min(most, inToT);
        if (least > most) return false;

        var sector = new Sector(this, trimM);
        if (sector.SweepRad < 0.0) return false;

        var innerM = _radiusM - halfM;
        if (innerM <= 0.0
            || !Roots(square, linear, (distanceM - innerM) * (distanceM + innerM), out var holeFromT, out var holeToT))
        {
            return sector.Holds(fromCentreX, fromCentreY, alongX, alongY, least, most);
        }

        return sector.Holds(fromCentreX, fromCentreY, alongX, alongY, least, Math.Min(most, holeFromT))
               || sector.Holds(fromCentreX, fromCentreY, alongX, alongY, Math.Max(least, holeToT), most);
    }

    /// <summary>The sector an arc sweeps between the radii through its two ends, less a trim off each.</summary>
    readonly struct Sector
    {
        readonly double _sense;
        readonly double _firstX;
        readonly double _firstY;
        readonly double _lastX;
        readonly double _lastY;

        public Sector(in RibbonPiece piece, double trimM)
        {
            _sense = Math.Sign(piece._curvature);
            SweepRad = piece._sweepRad - (2.0 * Math.Abs(piece._curvature) * trimM);
            _firstX = piece._firstRadialX;
            _firstY = piece._firstRadialY;
            _lastX = piece._lastRadialX;
            _lastY = piece._lastRadialY;
            if (trimM == 0.0) return;

            var (sin, cos) = Math.SinCos(piece._curvature * trimM);
            (_firstX, _firstY) = ((_firstX * cos) - (_firstY * sin), (_firstX * sin) + (_firstY * cos));
            (_lastX, _lastY) = ((_lastX * cos) + (_lastY * sin), (_lastY * cos) - (_lastX * sin));
        }

        public double SweepRad { get; }

        /// <summary>
        /// Whether any of the run <c>[least, most]</c> of a segment, given from the centre, lies in the sector.
        /// </summary>
        public bool Holds(double fromX, double fromY, double alongX, double alongY, double least, double most)
        {
            if (least > most) return false;
            if (SweepRad >= Math.Tau) return true;

            // Past its first radius and short of its last, measured the way the piece turns: each is one side
            // of a line through the centre, so a sector under a half turn is both and one over it is either.
            var pastFirst = _sense * ((_firstX * fromY) - (_firstY * fromX));
            var pastFirstStep = _sense * ((_firstX * alongY) - (_firstY * alongX));
            var shortOfLast = _sense * ((fromX * _lastY) - (fromY * _lastX));
            var shortOfLastStep = _sense * ((alongX * _lastY) - (alongY * _lastX));
            if (SweepRad <= Math.PI)
            {
                return Clip(pastFirst, pastFirstStep, ref least, ref most)
                       && Clip(shortOfLast, shortOfLastStep, ref least, ref most);
            }

            var otherLeast = least;
            var otherMost = most;
            return Clip(pastFirst, pastFirstStep, ref least, ref most)
                   || Clip(shortOfLast, shortOfLastStep, ref otherLeast, ref otherMost);
        }
    }

    /// <summary>The run <c>[least, most]</c> held to where <c>value + step·t ≥ 0</c>; false where nothing of it is left.</summary>
    static bool Clip(double value, double step, ref double least, ref double most)
    {
        if (step == 0.0) return value >= 0.0 && least <= most;

        var at = -value / step;
        if (step > 0.0) least = Math.Max(least, at);
        else most = Math.Min(most, at);
        return least <= most;
    }

    /// <summary>
    /// Where <c>square·t² + linear·t + constant</c> is at or under nothing, for a <paramref name="square"/>
    /// that is not negative: one run, or none.
    /// </summary>
    static bool Roots(double square, double linear, double constant, out double fromT, out double toT)
    {
        if (square == 0.0)
        {
            fromT = double.NegativeInfinity;
            toT = double.PositiveInfinity;
            return constant <= 0.0;
        }

        var discriminant = (linear * linear) - (4.0 * square * constant);
        if (discriminant < 0.0)
        {
            fromT = toT = 0.0;
            return false;
        }

        // The form that subtracts nothing alike: the root further from zero first, and the other from it.
        var far = -0.5 * (linear + (Math.CopySign(Math.Sqrt(discriminant), linear)));
        if (far == 0.0)
        {
            fromT = toT = 0.0;
            return true;
        }

        var one = far / square;
        var other = constant / far;
        fromT = Math.Min(one, other);
        toT = Math.Max(one, other);
        return true;
    }
}
