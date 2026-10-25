using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The first stage: the ground everything else is laid on, and the water cut out of it.</b> What it
/// hands the stages after it is the rings the map carries — which are the water, and the only description
/// of it there is (TER-7).
/// </summary>
/// <remarks>
/// <para>
/// <b>The water is decided before a single node is placed</b>, because every later stage asks the ground
/// whether it is free and the answer has to already be true. A river discovered after the streets were laid
/// is a town whose roads have to be taken back, which is the retry this generator does not do.
/// </para>
/// <para>
/// <b>A river is as wide as its banks stand apart at each point of it</b>, widening into pools and narrowing between
/// them on waves of its own (<see cref="RiversBrief.WidthSpread"/>). <b>Its tributaries flow in from past the
/// map's edge</b> (<see cref="RiversBrief.Tributaries"/>), each into a river or into another tributary, pointing
/// downstream, narrowing toward their heads, and joined into the water they meet as one ring
/// (<see cref="WaterOutline.Joined"/>) — as a river is into the sea it flows into. One that would cross another water is
/// drawn somewhere else, or not at all.
/// </para>
/// </remarks>
internal static class TerrainStage
{
    /// <summary>The fewest points a shoreline is drawn through, however straight the wave it is drawn from.</summary>
    const int FewestOutlinePoints = 24;

    /// <summary>How far past the map's own edge the far side of an outline is pushed, so no shore closes inside the town.</summary>
    const float OverrunM = 200f;

    /// <summary>How many places a tributary is drawn at before it is left out: drawn again, never searched for.</summary>
    const int TributaryTries = 8;

    /// <summary>
    /// The three waves a bank wanders on: how many times each runs over the shore's length, and how much of
    /// the wander is its own. <b>Three of them, of their own phase and period</b> — a shore drawn from one is
    /// a sine and one drawn from noise per point is a saw.
    /// </summary>
    static readonly Vector3 WavesOver = new(1f, 2.3f, 4.1f);

    static readonly Vector3 WaveShare = new(0.25f, 0.15f, 0.1f);

    /// <summary>The most the three waves reach together, which a width's wave is divided by to run from minus one to one.</summary>
    static readonly float WaveMost = WaveShare.X + WaveShare.Y + WaveShare.Z;

    /// <summary>
    /// The water a town stands on: the rings the map draws it and its shore from, the courses they are drawn along, and
    /// the sea's own outline.
    /// </summary>
    /// <param name="Rings">
    /// The water's own edge and the outer edge of the shore it is set in (GEN-2c). <b>Both are the same wave at
    /// two offsets</b>, so the strip is one width the whole way round.
    /// </param>
    /// <param name="Courses">The sea's coast first where there is one, then every river, then every tributary.</param>
    /// <param name="SeaM">
    /// The sea's own outline before any river was joined into it, and empty where there is no sea: <b>what a bridge is
    /// asked of</b> (GEN-62, <see cref="InTheSea"/>), since the rings are one water where a river meets it.
    /// </param>
    internal readonly record struct Water(CityPlan.WaterArrays Rings, TownMap.CourseArrays Courses, Vector2[] SeaM)
    {
        public static Water None => new(CityPlan.WaterArrays.None, TownMap.CourseArrays.None, []);

        public bool Any => Rings.Outline.Count > 0;

        /// <summary>
        /// <b>Whether a point is the sea's</b>, which no road spans (GEN-14b): a coast has one shore inside the town and
        /// whatever is past it is off the map, so a deck laid over it goes nowhere.
        /// </summary>
        public bool InTheSea(Vector2 pointM) => SeaM.Length > 0 && Polygons.Encloses(SeaM, pointM);
    }

    /// <summary>One water's course as it is drawn: its line, and each bank's offset off it along the way across.</summary>
    sealed record Course(TownMap.WaterBody Kind, Vector2 Across, int Parent, int Depth, Vector2[] CentreM, float[] NearM, float[] FarM);

    /// <summary>How many places a river is drawn at before it is left out: drawn again, never searched for.</summary>
    const int RiverTries = 8;

    /// <summary>
    /// How much of a river's run its wander rises over from either end, so it leaves the edge and meets the sea where it
    /// was drawn to.
    /// </summary>
    const float RiverRiseShare = 0.15f;

    /// <summary>
    /// <b>The sea, the rivers and their tributaries</b> (GEN-62), each drawn once: a river that would cross water already
    /// laid is drawn again somewhere else, or not at all — what fits is what is laid.
    /// </summary>
    public static Water Lay(TownBrief brief, SimConfig config, ref Rng draw)
    {
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var courses = new List<Course>();
        var outlines = new List<Vector2[]>();
        var shores = new List<Vector2[]>();
        var seaM = Array.Empty<Vector2>();
        var seaward = Vector2.Zero;
        var strandedM = 0f;

        if (brief.Sea is { } sea)
        {
            (var coast, seaward, strandedM) = Coast(sea, extentM, config, ref draw);
            courses.Add(coast);
            var (outline, shore) = CourseRings(coast, config);
            (seaM, _) = (outline, shore);
            outlines.Add(outline);
            shores.Add(shore);
        }

        var rivers = (int)MathF.Round(brief.Rivers.PerKm * MathF.Max(extentM.X, extentM.Y) / 1000f);
        for (var river = 0; river < rivers; river++)
        {
            for (var tries = 0; tries < RiverTries; tries++)
            {
                var (entersM, leavesM) = seaM.Length > 0
                    ? IntoTheSea(seaward, strandedM, extentM, ref draw)
                    : Across(brief.Rivers.BearingDeg, extentM, ref draw);
                var laid = River(entersM, leavesM, seaM.Length > 0 ? 0 : TownMap.CourseArrays.NoParent, brief.Rivers, config, ref draw);
                var (laidOutline, laidShore) = CourseRings(laid, config);
                if (!Fits(laid, laidOutline, laidShore, outlines, shores, config)) continue;

                courses.Add(laid);
                outlines.Add(laidOutline);
                shores.Add(laidShore);
                break;
            }
        }

        for (var tributary = 0; tributary < brief.Rivers.Tributaries; tributary++)
        {
            if (!courses.Exists(course => course.Kind == TownMap.WaterBody.River)) break;

            for (var tries = 0; tries < TributaryTries; tries++)
            {
                if (Tributary(courses, brief.Rivers, config, extentM, ref draw) is not { } laid) continue;

                var (laidOutline, laidShore) = CourseRings(laid, config);
                if (!Fits(laid, laidOutline, laidShore, outlines, shores, config)) continue;

                courses.Add(laid);
                outlines.Add(laidOutline);
                shores.Add(laidShore);
                break;
            }
        }

        if (courses.Count == 0) return Water.None;

        var arrays = Arrays(courses);
        return new Water(Rings(arrays, extentM, config), arrays, seaM);
    }

    /// <summary>
    /// <b>The sea's coast</b>: a river with one bank pushed off the map, so the sea has no far shore inside the town and
    /// its outline is one closed ring rather than two kinds of shape — standing as far in from the map's middle, toward
    /// the sea, as leaves the sea its share of the map's breadth that way.
    /// </summary>
    static (Course Coast, Vector2 Seaward, float StrandedM) Coast(SeaBrief sea, Vector2 extentM, SimConfig config, ref Rng draw)
    {
        var seaward = Heading.Unit(sea.BearingDeg * MathF.PI / 180f);
        var bearing = -Heading.RightOf(seaward);
        var breadthM = BreadthM(extentM, seaward);
        var seaWideM = breadthM * sea.Share;
        var strandedM = (breadthM * 0.5f) - seaWideM;
        var wanderM = seaWideM * sea.Meander;
        var runM = extentM.Length() + (OverrunM * 2f);
        var phase = DrawPhase(ref draw);

        var points = PointsAlong(runM, wanderM, config.CityGen.ShoreChordToleranceM);
        var middleM = new Vector2[points];
        var (nearM, farM) = (new float[points], new float[points]);
        var farOffM = extentM.Length() + OverrunM;
        for (var point = 0; point < points; point++)
        {
            var along = point / (float)(points - 1);
            middleM[point] = (extentM * 0.5f) + (bearing * ((along - 0.5f) * runM)) + (seaward * ((Wander(along, phase) * wanderM) + strandedM));
            (nearM[point], farM[point]) = (0f, farOffM);
        }

        return (new Course(TownMap.WaterBody.Sea, seaward, TownMap.CourseArrays.NoParent, 0, middleM, nearM, farM), seaward, strandedM);
    }

    /// <summary>How far across the map a line through its middle runs on this heading, edge to edge.</summary>
    static float BreadthM(Vector2 extentM, Vector2 heading) =>
        MathF.Min(
            MathF.Abs(heading.X) > 1e-4f ? extentM.X / MathF.Abs(heading.X) : float.PositiveInfinity,
            MathF.Abs(heading.Y) > 1e-4f ? extentM.Y / MathF.Abs(heading.Y) : float.PositiveInfinity);

    /// <summary>
    /// <b>Where a river flowing into the sea enters the map and where it meets the coast</b>: a place along the coast the
    /// map shows, and the edge a line from it reaches running inland, turned off straight away from the sea by a draw.
    /// </summary>
    static (Vector2 EntersM, Vector2 LeavesM) IntoTheSea(Vector2 seaward, float strandedM, Vector2 extentM, ref Rng draw)
    {
        var along = -Heading.RightOf(seaward);
        var middleM = extentM * 0.5f;
        var coastM = middleM + (seaward * strandedM) + (along * (draw.NextFloat(-0.35f, 0.35f) * BreadthM(extentM, along)));
        var inland = Heading.Unit(Trig.Atan2(-seaward.Y, -seaward.X) + draw.NextFloat(-0.8f, 0.8f));
        return (coastM + (inland * ToTheEdgeM(coastM, inland, extentM)), coastM);
    }

    /// <summary>
    /// <b>Where a river crossing a map with no sea enters and leaves it</b>: a line on the brief's bearing, turned by a
    /// draw and passing the middle at a drawn offset, edge to edge.
    /// </summary>
    static (Vector2 EntersM, Vector2 LeavesM) Across(float bearingDeg, Vector2 extentM, ref Rng draw)
    {
        var heading = Heading.Unit((bearingDeg * MathF.PI / 180f) + draw.NextFloat(-0.35f, 0.35f));
        var side = Heading.RightOf(heading);
        var passesM = (extentM * 0.5f) + (side * (draw.NextFloat(-0.35f, 0.35f) * BreadthM(extentM, side)));
        return (passesM - (heading * ToTheEdgeM(passesM, -heading, extentM)), passesM + (heading * ToTheEdgeM(passesM, heading, extentM)));
    }

    /// <summary>
    /// <b>One river, from where it enters the map to where it leaves it or meets the sea</b>, run on past both by the
    /// overrun so no bank closes inside the town: as wide as the brief says or as a bridge spans at its widest pool,
    /// whichever is less (GEN-62), and wandering off its line by its share of the run, rising from nothing at either end.
    /// </summary>
    static Course River(Vector2 entersM, Vector2 leavesM, int parent, RiversBrief rivers, SimConfig config, ref Rng draw)
    {
        var heading = Vector2.Normalize(leavesM - entersM);
        var fromM = entersM - (heading * OverrunM);
        var runM = Vector2.Distance(fromM, leavesM) + OverrunM;
        var across = Heading.RightOf(heading);
        var narrowing = rivers.WidthSpread;
        var widthM = MathF.Min(rivers.WidthM, WidestSpannedM(config) / (1f + narrowing));
        var widening = MathF.Min(narrowing, MathF.Max(0f, (WidestSpannedM(config) / widthM) - 1f));
        var wanderM = runM * rivers.Meander;
        var (phase, widthPhase) = (DrawPhase(ref draw), DrawPhase(ref draw));

        var points = PointsAlong(runM, wanderM + (narrowing * widthM * 0.5f / WaveMost), config.CityGen.ShoreChordToleranceM);
        var centreM = new Vector2[points];
        var (nearM, farM) = (new float[points], new float[points]);
        for (var point = 0; point < points; point++)
        {
            var along = point / (float)(points - 1);
            var risen = Math.Clamp(MathF.Min(along, 1f - along) / RiverRiseShare, 0f, 1f);
            risen *= risen * (3f - (2f * risen));
            centreM[point] = fromM + (heading * (along * runM)) + (across * (Wander(along, phase) * wanderM * risen));
            var halfM = widthM * 0.5f * Widened(along, widthPhase, narrowing, widening);
            (nearM[point], farM[point]) = (halfM, -halfM);
        }

        return new Course(TownMap.WaterBody.River, across, parent, 1, centreM, nearM, farM);
    }

    /// <summary>
    /// <b>Whether a water just drawn fits among those laid</b> (one ring a course, in the courses' order): its shore
    /// crosses no other's but the one it flows into, which it reaches into as one water (<see cref="WaterOutline.Joined"/>).
    /// </summary>
    static bool Fits(Course laid, Vector2[] outline, Vector2[] shore, List<Vector2[]> outlines, List<Vector2[]> shores, SimConfig config)
    {
        for (var water = 0; water < outlines.Count; water++)
        {
            if (water != laid.Parent && Polygons.Cross(shore, shores[water])) return false;
        }

        return laid.Parent == TownMap.CourseArrays.NoParent
               || JoinedBanks(outlines[laid.Parent], shores[laid.Parent], outline, shore, config, out _, out _);
    }

    /// <summary>
    /// <b>A tributary drawn into one of the rivers already laid</b>, or none where the place drawn has no room for one:
    /// its mouth inside that river at a place along it the map shows, and its line run upstream off it at an angle
    /// from that river's own way upstream, out past the map's edge.
    /// </summary>
    static Course? Tributary(List<Course> laid, RiversBrief rivers, SimConfig config, Vector2 extentM, ref Rng draw)
    {
        var figures = config.CityGen;
        bool Feeds(Course course) => course.Kind == TownMap.WaterBody.River && course.Depth <= figures.TributaryDepthMost;

        var parents = 0;
        foreach (var course in laid) parents += Feeds(course) ? 1 : 0;

        var pick = draw.NextInt(parents);
        var parentAt = 0;
        for (; parentAt < laid.Count; parentAt++)
        {
            if (Feeds(laid[parentAt]) && pick-- == 0) break;
        }

        var parent = laid[parentAt];
        var (first, last) = OnTheMap(parent.CentreM, extentM);
        var insetPoints = (int)((last - first) * figures.TributaryMouthInsetShare);
        var (side, share, joinRad) = (draw.NextFloat() < 0.5f ? 1f : -1f, draw.NextFloat(figures.TributaryWidthLeastShare, figures.TributaryWidthMostShare),
            draw.NextFloat(config.TributaryJoinLeastRad, config.TributaryJoinMostRad));
        var (phase, widthPhase) = (DrawPhase(ref draw), DrawPhase(ref draw));

        var mouths = (last - insetPoints) - (first + insetPoints) + 1;
        if (mouths <= 0) return null;

        var at = first + insetPoints + draw.NextInt(mouths);

        // Where the mouth stands: as far into the parent off the bank on this side as the tributary and its shore need to
        // close inside it, and no further than the parent's middle.
        var (nearM, farM) = (parent.NearM[at], parent.FarM[at]);
        var parentWidthM = MathF.Abs(nearM - farM);
        var mouthWidthM = parentWidthM * share;
        var bankM = side == MathF.Sign(nearM - farM) ? nearM : farM;
        var intoM = MathF.Min(parentWidthM * 0.5f, mouthWidthM + (2f * figures.ShoreWidthM));
        var mouthM = parent.CentreM[at] + (parent.Across * (bankM - (side * intoM)));
        if (!Within(mouthM, extentM)) return null;

        // Run off the parent's way upstream, toward the side drawn.
        var downstream = Vector2.Normalize(parent.CentreM[Math.Min(at + 1, parent.CentreM.Length - 1)] - parent.CentreM[Math.Max(at - 1, 0)]);
        var sideways = Heading.RightOf(downstream);
        if (Vector2.Dot(sideways, parent.Across * side) < 0f) sideways = -sideways;

        var (sin, cos) = MathF.SinCos(joinRad);
        var upstream = (-downstream * cos) + (sideways * sin);
        var runM = ToTheEdgeM(mouthM, upstream, extentM) + OverrunM;
        var across = Heading.RightOf(-upstream);

        // The wander starts once the river is clear of the water it flows into, so its mouth stays where it was drawn.
        var clearM = intoM + (2f * mouthWidthM);
        var wanderM = runM * rivers.Meander;
        var points = PointsAlong(runM, wanderM + (rivers.WidthSpread * mouthWidthM * 0.5f / WaveMost), figures.ShoreChordToleranceM);
        var centreM = new Vector2[points];
        var (near, far) = (new float[points], new float[points]);
        for (var point = 0; point < points; point++)
        {
            // Drawn from its head down to its mouth, which is the way it flows.
            var fromMouth = 1f - (point / (float)(points - 1));
            var outM = fromMouth * runM;
            var risen = Math.Clamp(outM / clearM, 0f, 1f);
            risen *= risen * (3f - (2f * risen));
            centreM[point] = mouthM + (upstream * outM) + (across * (Wander(fromMouth, phase) * wanderM * risen));
            var halfM = mouthWidthM * 0.5f * (1f - (figures.TributaryHeadNarrowShare * fromMouth)) * Widened(fromMouth, widthPhase, rivers.WidthSpread, rivers.WidthSpread);
            (near[point], far[point]) = (halfM, -halfM);
        }

        return new Course(TownMap.WaterBody.River, across, parentAt, parent.Depth + 1, centreM, near, far);
    }

    /// <summary>
    /// <b>The rings every water drawn along a course is drawn from</b>: its two banks at their own offsets across the
    /// course, and the shore laid landward of them (GEN-2c), each tributary joined into the water it flows into — a map's
    /// own courses read back (<see cref="TownMap.Courses"/>) drawn as the brief that authored them drew them.
    /// </summary>
    /// <remarks>
    /// A tributary that does not meet its parent as one (<see cref="WaterOutline.Joined"/>), which an authored map never
    /// holds, is drawn as a ring of its own.
    /// </remarks>
    public static CityPlan.WaterArrays Rings(TownMap.CourseArrays courses, Vector2 extentM, SimConfig config)
    {
        var (outlines, shores) = (new List<Vector2[]>(), new List<Vector2[]>());
        var ringOf = new int[courses.Count];
        for (var water = 0; water < courses.Count; water++)
        {
            var (outline, shore) = CourseRings(
                courses.CourseOf(water), courses.Across[water], courses.NearOf(water), courses.FarOf(water), config);
            if (courses.Parent[water] is var parent and not TownMap.CourseArrays.NoParent
                && JoinedBanks(outlines[ringOf[parent]], shores[ringOf[parent]], outline, shore, config, out var joinedOutline, out var joinedShore))
            {
                ringOf[water] = ringOf[parent];
                (outlines[ringOf[water]], shores[ringOf[water]]) = (joinedOutline, joinedShore);
                continue;
            }

            ringOf[water] = outlines.Count;
            outlines.Add(outline);
            shores.Add(shore);
        }

        // Cut to the map own edges (GEN-2b): the rings are what the water and its shore are drawn from and
        // what says a point is wet, so a bank drawn past the town would be ground nobody could stand on
        // outside it.
        return new CityPlan.WaterArrays { Outline = CutRings(outlines, extentM), Shore = CutRings(shores, extentM) };
    }

    /// <summary>
    /// A tributary's water and shore joined into the ones it flows into (<see cref="WaterOutline.Joined"/>), each mouth's
    /// corners rounded — the shore's about the bank's own centres — or false where either will not join.
    /// </summary>
    static bool JoinedBanks(
        Vector2[] outline, Vector2[] shore, Vector2[] laidOutline, Vector2[] laidShore, SimConfig config,
        out Vector2[] joinedOutline, out Vector2[] joinedShore)
    {
        var toleranceM = config.CityGen.ShoreChordToleranceM;
        joinedShore = [];
        return WaterOutline.Joined(outline, laidOutline, config.CityGen.ConfluenceRadiusM, toleranceM, out joinedOutline)
               && WaterOutline.Joined(shore, laidShore, config.ConfluenceShoreRadiusM, toleranceM, out joinedShore);
    }

    static (Vector2[] Outline, Vector2[] Shore) CourseRings(Course course, SimConfig config) =>
        CourseRings(course.CentreM, course.Across, course.NearM, course.FarM, config);

    /// <summary>One course's water and its shore, as two closed rings not yet cut to the map.</summary>
    static (Vector2[] Outline, Vector2[] Shore) CourseRings(
        ReadOnlySpan<Vector2> middleM, Vector2 across, ReadOnlySpan<float> nearM, ReadOnlySpan<float> farM, SimConfig config)
    {
        var shoreM = config.CityGen.ShoreWidthM;

        // Which way is away from the water on the near bank, so that a shore laid on either side of the line
        // is laid on the land side of it.
        var landward = MathF.Sign(nearM[0] - farM[0]);
        return (Ring(middleM, across, nearM, farM, 0f), Ring(middleM, across, nearM, farM, landward * shoreM));
    }

    /// <summary>The rings, each cut to the map (GEN-2b), as the arrays a plan carries a set of them in.</summary>
    static CityPlan.RingArrays CutRings(List<Vector2[]> rings, Vector2 extentM)
    {
        var offsets = new List<int> { 0 };
        var pointM = new List<Vector2>();
        foreach (var ring in rings)
        {
            var cutM = WaterOutline.CutToTheMap(ring, extentM);
            if (cutM.Length == 0) continue;

            pointM.AddRange(cutM);
            offsets.Add(pointM.Count);
        }

        return offsets.Count == 1 ? CityPlan.RingArrays.None : new CityPlan.RingArrays { Offsets = [.. offsets], PointM = [.. pointM] };
    }

    /// <summary>One closed ring around a line, as the two banks standing at their own offsets across it, each pushed out by <paramref name="outM"/>.</summary>
    static Vector2[] Ring(ReadOnlySpan<Vector2> middleM, Vector2 across, ReadOnlySpan<float> nearM, ReadOnlySpan<float> farM, float outM)
    {
        var ring = new Vector2[middleM.Length * 2];
        for (var point = 0; point < middleM.Length; point++)
        {
            ring[point] = middleM[point] + (across * (nearM[point] + outM));
            ring[^(point + 1)] = middleM[point] + (across * (farM[point] - outM));
        }

        return ring;
    }

    /// <summary>
    /// <b>The widest a river may pool to: what the town's deck spans square on</b> (GEN-14a), less a bridgehead's own
    /// ground on either bank and the step each bank is found to. A pool wider than its mean water may not be wider
    /// than this, or the bridge the town is turned to stand on is one it cannot build and the town falls in two.
    /// </summary>
    static float WidestSpannedM(SimConfig config) =>
        config.CityGen.BridgeDeckLongestM - (2f * (config.CityGen.WaterfrontClearM + TownGen.CorridorM(config, LayoutEdge.MostLanes) + config.CityGen.ShoreWidthM));

    static TownMap.CourseArrays Arrays(List<Course> courses)
    {
        var offsets = new int[courses.Count + 1];
        for (var water = 0; water < courses.Count; water++) offsets[water + 1] = offsets[water] + courses[water].CentreM.Length;

        return new TownMap.CourseArrays
        {
            Kind = [.. courses.Select(course => course.Kind)], Across = [.. courses.Select(course => course.Across)],
            Parent = [.. courses.Select(course => course.Parent)], PointOffsets = offsets,
            PointM = [.. courses.SelectMany(course => course.CentreM)], NearM = [.. courses.SelectMany(course => course.NearM)],
            FarM = [.. courses.SelectMany(course => course.FarM)],
        };
    }

    /// <summary>The first and the last point of a line that stand on the map.</summary>
    static (int First, int Last) OnTheMap(Vector2[] lineM, Vector2 extentM)
    {
        var (first, last) = (lineM.Length, -1);
        for (var point = 0; point < lineM.Length; point++)
        {
            if (!Within(lineM[point], extentM)) continue;

            first = Math.Min(first, point);
            last = point;
        }

        return (first, last);
    }

    static bool Within(Vector2 atM, Vector2 extentM) => atM.X >= 0f && atM.Y >= 0f && atM.X <= extentM.X && atM.Y <= extentM.Y;

    /// <summary>How far a line from a place on the map runs before it leaves it.</summary>
    static float ToTheEdgeM(Vector2 fromM, Vector2 heading, Vector2 extentM)
    {
        var runM = float.PositiveInfinity;
        if (heading.X > 0f) runM = MathF.Min(runM, (extentM.X - fromM.X) / heading.X);
        if (heading.X < 0f) runM = MathF.Min(runM, -fromM.X / heading.X);
        if (heading.Y > 0f) runM = MathF.Min(runM, (extentM.Y - fromM.Y) / heading.Y);
        if (heading.Y < 0f) runM = MathF.Min(runM, -fromM.Y / heading.Y);
        return runM;
    }

    /// <summary>
    /// How many points a bank is drawn through: enough that no chord of it stands further off the wave than
    /// the tolerance. <b>Derived from the wave's own curvature</b> — the sharpest a sum of sines bends is the
    /// sum of what each of them bends — so a wild meander is drawn through more points and a straight coast
    /// through few, and neither is a count anybody has to keep true by eye.
    /// </summary>
    static int PointsAlong(float runM, float wanderM, float toleranceM)
    {
        var bendsPerM = 0f;
        for (var wave = 0; wave < 3; wave++)
        {
            var perM = MathF.Tau * At(WavesOver, wave) / runM;
            bendsPerM += perM * perM * At(WaveShare, wave) * wanderM;
        }

        if (bendsPerM <= 0f) return FewestOutlinePoints;

        // A chord of a curve stands off it by about r²/8 of the curve's own bend, which is the sagitta.
        var stepM = MathF.Sqrt(8f * toleranceM / bendsPerM);
        return Math.Max(FewestOutlinePoints, (int)MathF.Ceiling(runM / stepM) + 1);
    }

    static Vector3 DrawPhase(ref Rng draw) =>
        new(draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau));

    static float Wander(float along, Vector3 phase) =>
        (Trig.Sin((along * MathF.Tau * WavesOver.X) + phase.X) * WaveShare.X)
        + (Trig.Sin((along * MathF.Tau * WavesOver.Y) + phase.Y) * WaveShare.Y)
        + (Trig.Sin((along * MathF.Tau * WavesOver.Z) + phase.Z) * WaveShare.Z);

    /// <summary>How wide a water is here against its mean: its own waves, scaled to how far it narrows and how far it widens.</summary>
    static float Widened(float along, Vector3 phase, float narrowing, float widening) =>
        Wander(along, phase) / WaveMost is var wave && wave < 0f ? 1f + (narrowing * wave) : 1f + (widening * wave);

    static float At(Vector3 of, int index) => index switch { 0 => of.X, 1 => of.Y, _ => of.Z };
}
