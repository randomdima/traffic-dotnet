using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The street furniture, scattered over what is left</b>: the one thing a town has more of than anything
/// else, and the only stage whose failure to place something costs nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Props are what the ground affords and not a count anybody authored</b> (GEN-6), and they are laid in
/// passes that answer different questions (GEN-6b). <b>The kerbs are walked first</b>, because a verge is a
/// line and not an area: what stands along one is found by following the line the concrete stops at, on that
/// line's own bearing, and not by sweeping a lattice and asking each square whether it happens to be near a
/// street — a tree-lined ring's row at its pitch first (GEN-6f), then the scatter, a share of its stations a run of
/// one look (GEN-6h). <b>Then the ground the town is not on is swept</b>, well clear of the walk that pass took, each
/// candidate that stands the heart of a clump as thick as the ground's thickets are there (GEN-6g).
/// </para>
/// <para>
/// <b>Neither pass searches for anything</b>: a candidate that does not stand is not a prop, and the ground
/// it was refused for is not tried again from another angle. Everything the stage needs was laid before
/// it — the roads and the boundary they leave — which is what makes one pass over each enough.
/// </para>
/// <para>
/// <b>A prop is laid at the size of the look it will wear</b> (GEN-6e): the pass draws its kind, then one of that
/// kind's looks as often as each one's share says (<see cref="PropSizes"/>), and the prop's girth is that look's. The art
/// states every size and the plan none, so a bollard is laid a bollard's width and a great tree a great tree's, and a
/// kind nothing was drawn for lays nothing.
/// </para>
/// </remarks>
internal static class PropStage
{
    /// <summary>
    /// <b>The two passes, kept apart</b>: what the map sets down and what a town puts along its kerbs as props, and what
    /// grows beyond them as scenery that stands nothing — a city's yards, parks and waste ground being millions of props
    /// too many to stand as bodies.
    /// </summary>
    /// <param name="setDown">
    /// The props the map sets down, laid before either pass and so kept clear of by both — each in a look of its kind
    /// drawn as a pass draws one, and none where the art holds nothing of that kind.
    /// </param>
    /// <param name="growth">How thickly what grows wild covers the open ground at a place, as a share of the most it holds.</param>
    /// <param name="looks">The looks a prop may wear (GEN-6e): a town handed none lays no prop.</param>
    public static (CityPlan.PropArrays AlongTheKerbs, CityPlan.SceneryArrays Beyond) LayApart(
        Vector2 acrossM, Paving paving, GroundShapes ground, GenClaims claims, Map.TownMap.PropArrays setDown,
        Func<Vector2, float> growth, PropSizes looks, SimConfig config, ref Rng draw)
    {
        var scatter = PropScatter.Over(config.Grid, acrossM, looks.WidestM, config.CityGen.PropApartM);
        for (var prop = 0; prop < setDown.Count; prop++)
        {
            var kind = setDown.Kind[prop];
            if (!Drawn(looks, kind, draw.NextFloat(), out var look)) continue;

            scatter.Add(setDown.CentreM[prop], looks.GirthM[look] * 0.5f, setDown.BearingRad[prop], kind, look);
        }

        var rings = paving.Rings(config);
        TreeLined(rings, ground, claims, looks, config, scatter, ref draw);
        AlongTheKerbs(rings, ground, claims, looks, config, scatter, ref draw);
        var kerbside = scatter.CentreM.Count;
        OverWhatIsLeft(acrossM, ground, claims, growth, looks, config, scatter, ref draw);

        var beyond = scatter.CentreM.Count - kerbside;
        return (
            Arrays(scatter, kerbside, looks),
            new CityPlan.SceneryArrays
            {
                CentreM = CollectionsMarshal.AsSpan(scatter.CentreM).Slice(kerbside, beyond).ToArray(),
                RadiusM = CollectionsMarshal.AsSpan(scatter.RadiusM).Slice(kerbside, beyond).ToArray(),
                Look = CollectionsMarshal.AsSpan(scatter.Look).Slice(kerbside, beyond).ToArray(),
            });
    }

    /// <summary>
    /// The first <paramref name="count"/> props laid, each standing as its look's body (GEN-6e): spaced and cleared by
    /// its girth, and nothing but what stands bodies reads the body.
    /// </summary>
    static CityPlan.PropArrays Arrays(PropScatter scatter, int count, PropSizes looks)
    {
        count = Math.Min(count, scatter.CentreM.Count);
        var bodyRadiusM = new float[count];
        for (var prop = 0; prop < count; prop++) bodyRadiusM[prop] = looks.BodyM[scatter.Look[prop]] * 0.5f;

        return new()
        {
            CentreM = CollectionsMarshal.AsSpan(scatter.CentreM)[..count].ToArray(),
            RadiusM = CollectionsMarshal.AsSpan(scatter.RadiusM)[..count].ToArray(),
            BodyRadiusM = bodyRadiusM,
            BearingRad = CollectionsMarshal.AsSpan(scatter.BearingRad)[..count].ToArray(),
            Kind = CollectionsMarshal.AsSpan(scatter.Kind)[..count].ToArray(),
            Look = CollectionsMarshal.AsSpan(scatter.Look)[..count].ToArray(),
        };
    }

    /// <summary>
    /// <b>A tree-lined street's trees, before anything else along its kerb</b> (GEN-6f): a share of the walk's rings
    /// each carry one crowned look at a regular pitch, its near rim on the near edge of the verge, so the row runs
    /// straight along the street and the furniture and planting laid after it fill between its trees.
    /// </summary>
    /// <remarks>
    /// <b>A ring is one block's kerb all round</b>, so a block is lined or not and one look lines it: a street's two
    /// sides are two rings and may differ, which is what a street planted a block at a time looks like. A tree that does
    /// not stand — a drive, a corner's sight line (TER-4c.4), a building up against the walk — leaves its place in the
    /// row empty and moves nothing else.
    /// </remarks>
    static void TreeLined(
        GroundRings rings, GroundShapes ground, GenClaims claims, PropSizes looks, SimConfig config, PropScatter scatter,
        ref Rng draw)
    {
        var nearM = config.CityGen.PropVergeNearM;
        var pitchM = config.CityGen.PropStreetTreePitchM;
        var candidates = new Candidates(ground, claims, scatter, config.Terrain.GroundStepM, standOffM: 0f);
        foreach (var face in rings.WalkEdge)
        {
            // Drawn whether or not the ring is lined, so what the stream has spent by a ring is the rings before it.
            var lined = draw.NextFloat() < config.CityGen.PropTreeLinedShare;
            var look = looks.DrawCrown(PropKind.UrbanNature, draw.NextFloat());
            var phaseM = draw.NextFloat() * pitchM;
            if (!lined || look < 0) continue;

            var reachM = looks.GirthM[look] * 0.5f;
            var lengthM = Spline.TotalLengthM(face);
            var cursor = default(SplineCursor);
            for (var stationM = phaseM; stationM < lengthM; stationM += pitchM)
            {
                var on = Spline.SampleFrom(face, stationM, ref cursor);
                candidates.Add(on.PositionM - (on.Right * (reachM + nearM)), reachM, on.HeadingRad, PropKind.UrbanNature, (ushort)look);
            }
        }

        candidates.Flush();
    }

    /// <summary>
    /// <b>The first pass: what a town puts along its own kerbs</b> (GEN-6b). The line walked is the walk's
    /// outer face (<see cref="GroundRings.WalkEdge"/>) — the whole town's kerb in one set of closed rings —
    /// and a candidate stands out in the verge beyond it, <b>on that face's own bearing there</b>, so a look
    /// with a front runs with the street rather than with the compass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The band is measured to the prop's own near rim and not to its centre</b>, which is what puts a
    /// narrow look at the near edge of the verge and pushes a wide one out by its own width (GEN-6b). The
    /// girth is still asked of the ground (GEN-6a), but the ground's answer parts from the drawn walk at a
    /// corner (<see href="../../../docs/index.md">known gaps</see>, <c>GroundShapes.At</c>), so what places a
    /// prop off the walk is the figure the walk was struck at rather than an answer about the point it
    /// stands on.
    /// </para>
    /// <para>
    /// <b>A ring is walked once and the outward hand is the ring's own</b>: a shell is walked with the ground
    /// it covers on its right (<see cref="BandShell.Chains"/>), so the verge is to its left, whether the ring
    /// is the one round the outside of the town or one round a block inside it. Nothing here knows what a
    /// junction, a roundabout or a bay is — the concrete wraps all of them, and the face is continuous over
    /// every one.
    /// </para>
    /// </remarks>
    static void AlongTheKerbs(
        GroundRings rings, GroundShapes ground, GenClaims claims, PropSizes looks, SimConfig config, PropScatter scatter,
        ref Rng draw)
    {
        var nearM = config.CityGen.PropVergeNearM;
        var bandM = config.CityGen.PropVergeFarM - nearM;
        var pitchM = config.CityGen.PropVergePitchM;
        var gapM = config.CityGen.PropRunGapM;
        var runMost = Math.Max(config.CityGen.PropRunMost, 1);

        var candidates = new Candidates(ground, claims, scatter, config.Terrain.GroundStepM, standOffM: 0f);
        foreach (var face in rings.WalkEdge)
        {
            var lengthM = Spline.TotalLengthM(face);
            var cursor = default(SplineCursor);
            for (var alongM = 0f; alongM < lengthM; alongM += pitchM)
            {
                // Drawn before the ground is asked about anything, so that what the stream has spent by
                // station n is the rings' own length and the looks handed down, and never what the ground
                // answered at the stations before it.
                var stationM = alongM + (draw.NextFloat() * pitchM);
                var kind = OnAVerge(config, ref draw);
                var drawn = Drawn(looks, kind, draw.NextFloat(), out var look);
                var outU = draw.NextFloat();
                var runs = draw.NextFloat() < config.CityGen.PropRunShare;

                // Two to the most a run lays, and one where that most is one — on one draw either way.
                var count = Math.Min(runMost, 2 + draw.NextInt(Math.Max(runMost - 1, 1)));
                if (stationM >= lengthM || !drawn) continue;

                var reachM = looks.GirthM[look] * 0.5f;
                var outM = reachM + nearM + (outU * bandM);

                var on = Spline.SampleFrom(face, stationM, ref cursor);
                candidates.Add(on.PositionM - (on.Right * outM), reachM, on.HeadingRad, kind, look);
                if (!runs || !RunsOf(looks, kind, look)) continue;

                // A run is the one look again, a gap of grass apart along the face and on its bearing there.
                var runCursor = cursor;
                for (var member = 1; member < count; member++)
                {
                    var memberM = stationM + (member * ((2f * reachM) + gapM));
                    if (memberM >= lengthM) break;

                    var at = Spline.SampleFrom(face, memberM, ref runCursor);
                    candidates.Add(at.PositionM - (at.Right * outM), reachM, at.HeadingRad, kind, look);
                }
            }
        }

        candidates.Flush();
    }

    /// <summary>
    /// <b>What a verge lays a run of</b> (GEN-6h): what a town sets out by hand — its furniture and its planting — and
    /// never a crowned look, whose row is the street's (GEN-6f), nor the wild set, which nobody set out.
    /// </summary>
    static bool RunsOf(PropSizes looks, PropKind kind, ushort look) =>
        kind != PropKind.WildNature && !(looks.BodyM[look] < looks.GirthM[look]);

    /// <summary>
    /// <b>The last pass: what grows where the town is not</b> (GEN-6b). A stratified sweep — one candidate
    /// per cell of a coarse lattice, jittered inside it — so the cost is the ground rather than the count,
    /// and every candidate standing within a stand-off of a walk or a car park is left to the first pass.
    /// <b>As thickly as the zone it stands in grows</b> (GEN-58): a candidate on open ground is kept at its zone's
    /// share. <b>Each that stands is a clump's heart</b> (GEN-6g), and what grows round it is laid once every heart has
    /// been, so a heart is never crowded out by the clump of the cell before it. <b>Nothing here is laid on a
    /// bearing</b>: what the wild set holds has no front to turn.
    /// </summary>
    static void OverWhatIsLeft(
        Vector2 acrossM, GroundShapes ground, GenClaims claims, Func<Vector2, float> growth, PropSizes looks, SimConfig config,
        PropScatter scatter, ref Rng draw)
    {
        if (!looks.Holds(PropKind.WildNature)) return;

        var spacingM = config.CityGen.PropSpacingM;
        var standOffM = config.CityGen.PropWildStandOffM;
        var columns = (int)(acrossM.X / spacingM);
        var rows = (int)(acrossM.Y / spacingM);

        var thickets = new Thickets(
            ((ulong)draw.NextUint() << 32) | draw.NextUint(), config.CityGen.PropThicketM, config.CityGen.PropThicketContrast);
        var firstHeart = scatter.CentreM.Count;
        var candidates = new Candidates(
            ground, claims, scatter, config.Terrain.GroundStepM, standOffM, atM => growth(atM) * thickets.At(atM));
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                // Every cell draws the same whatever its ground, so the stream is the lattice's and the cells' ground,
                // growth and stand-off are asked of them all at once, on every thread, as a block is flushed.
                var atM = new Vector2(
                    (column + draw.NextFloat()) * spacingM,
                    (row + draw.NextFloat()) * spacingM);
                var growsAbove = draw.NextFloat();
                Drawn(looks, PropKind.WildNature, draw.NextFloat(), out var look);
                candidates.Add(atM, looks.GirthM[look] * 0.5f, 0f, PropKind.WildNature, look, growsAbove);
            }
        }

        candidates.Flush();
        Clumps(firstHeart, scatter.CentreM.Count, thickets, growth, ground, claims, looks, config, scatter, ref draw);
    }

    /// <summary>
    /// <b>What grows round each heart the sweep laid</b> (GEN-6g): of the wild set no wider than the heart, each a
    /// clearance and up to <see cref="CityGenFigures.PropClumpReachM"/> more off its rim — a thicket round a tree, a patch
    /// of weeds round a weed, and never a tree sheltering under a bush. <b>As many as the ground is thick there</b>: up to
    /// <see cref="CityGenFigures.PropClumpMost"/> where the thickets' field is at its thickest, half that where it is
    /// even, and none in a clearing.
    /// </summary>
    /// <remarks>
    /// <b>A member keeps the stand-off from the paving its heart kept</b> (GEN-6b), asked of each one with the rest of its
    /// ground: a clump at the edge of the open ground reaches toward the verge, and the strip between the passes stays a
    /// strip. <b>And grows only as thickly as its own zone does</b> (GEN-58): one reaching over a zone's edge into ground
    /// that grows thinner than its heart's is kept at the share the two growths stand in, so a car park beside a wood
    /// grows nothing of it. A member that does not stand is not a prop, as anywhere else.
    /// <para>
    /// <b>Each heart draws its clump on a stream of its own, keyed on where it stands</b> (GEN-11): which hearts stood is
    /// what the ground answered, so one stream walked heart by heart spent what the ground answered, and a heart more or
    /// fewer anywhere redrew every clump after it.
    /// </para>
    /// </remarks>
    static void Clumps(
        int firstHeart, int hearts, Thickets thickets, Func<Vector2, float> growth, GroundShapes ground, GenClaims claims,
        PropSizes looks, SimConfig config, PropScatter scatter, ref Rng draw)
    {
        var most = Math.Max(config.CityGen.PropClumpMost, 0);
        var reachM = config.CityGen.PropClumpReachM;
        var apartM = config.CityGen.PropApartM;
        var clumpSeed = ((ulong)draw.NextUint() << 32) | draw.NextUint();
        var candidates = new Candidates(
            ground, claims, scatter, config.Terrain.GroundStepM, standOffM: config.CityGen.PropWildStandOffM, growth);
        for (var heart = firstHeart; heart < hearts; heart++)
        {
            var heartM = scatter.CentreM[heart];
            var heartRadiusM = scatter.RadiusM[heart];
            var heartGrowth = growth(heartM);
            var own = new Rng(clumpSeed, ConnectionPoints.Keyed(heartM, heartM));
            var members = own.NextInt((int)MathF.Round(most * thickets.At(heartM) / Thickets.Thickest) + 1);
            for (var member = 0; member < members; member++)
            {
                var look = looks.DrawNoWider(PropKind.WildNature, own.NextFloat(), 2f * heartRadiusM);
                var towards = Heading.Unit(own.NextFloat() * MathF.Tau);
                var outU = own.NextFloat();
                var keptU = own.NextFloat();
                if (look < 0) continue;

                var memberRadiusM = looks.GirthM[look] * 0.5f;
                var offM = heartRadiusM + memberRadiusM + apartM + (outU * reachM);
                candidates.Add(heartM + (towards * offM), memberRadiusM, 0f, PropKind.WildNature, (ushort)look, keptU * heartGrowth);
            }
        }

        candidates.Flush();
    }

    /// <summary>
    /// <b>A pass's candidates, a block at a time</b>: drawn in the pass's own order, every one's ground and claims
    /// asked on every thread at once, and each laid against the props before it in the order it was drawn.
    /// </summary>
    /// <remarks>
    /// <b>The town laid is the one the sweep alone lays.</b> What makes one candidate's fate hang on another's is the
    /// stream and the scatter, and both stay in order; what the ground and the claims answer of a candidate hangs on
    /// nothing a pass lays, so it is the same answer asked on any thread (<see cref="GroundShapes.Scan"/>).
    /// </remarks>
    /// <param name="standOffM">How far off the paving a candidate keeps, asked here; nought where its pass asked it already or keeps none.</param>
    /// <param name="growth">
    /// How thickly the ground grows at a place, where a candidate is kept only above the growth it was added with (GEN-6g);
    /// none where the pass keeps every candidate the ground affords.
    /// </param>
    sealed class Candidates(
        GroundShapes ground, GenClaims claims, PropScatter scatter, float stepM, float standOffM,
        Func<Vector2, float>? growth = null)
    {
        /// <summary>How many are drawn before they are asked about: a working set a few megabytes whatever the town.</summary>
        const int Block = 1 << 16;

        readonly Vector2[] _atM = new Vector2[Block];
        readonly float[] _reachM = new float[Block];
        readonly float[] _bearingRad = new float[Block];
        readonly PropKind[] _kind = new PropKind[Block];
        readonly ushort[] _look = new ushort[Block];
        readonly float[] _growsAbove = new float[Block];
        readonly bool[] _stands = new bool[Block];

        /// <summary>The threads' working sets, kept from one block to the next rather than laid again for each.</summary>
        readonly ConcurrentBag<GroundShapes.Scan> _scans = [];

        int _count;

        /// <param name="growsAbove">The growth the ground must pass at the candidate for it to stand, where the pass asks one.</param>
        public void Add(Vector2 atM, float reachM, float bearingRad, PropKind kind, ushort look, float growsAbove = 0f)
        {
            (_atM[_count], _reachM[_count], _bearingRad[_count], _kind[_count], _look[_count], _growsAbove[_count]) =
                (atM, reachM, bearingRad, kind, look, growsAbove);
            if (++_count == Block) Flush();
        }

        public void Flush()
        {
            InChunks.Over(
                _count,
                () => _scans.TryTake(out var scan) ? scan : ground.NewScan(),
                (scan, at) => _stands[at] = (growth is null || growth(_atM[at]) > _growsAbove[at]) && Stands(scan, _atM[at], _reachM[at]),
                _scans.Add);
            for (var at = 0; at < _count; at++)
            {
                if (!_stands[at] || scatter.Reaches(_atM[at], _reachM[at])) continue;

                scatter.Add(_atM[at], _reachM[at], _bearingRad[at], _kind[at], _look[at]);
            }

            _count = 0;
        }

        /// <summary>Whether a prop of this girth stands here at all, the props already laid aside.</summary>
        /// <remarks>
        /// <para>
        /// <b>Its whole girth on grass</b> (GEN-6a). Grass is what is left over — every shape laid before this
        /// answers for its own ground, and everything standing on grass claimed it — and the same test is what
        /// keeps a prop on the map (GEN-2b): off the town is not grass.
        /// </para>
        /// <para>
        /// <b>And no collar</b> (GEN-6a): the girth is cleared against the ground's answer, which is the walk
        /// down every street, and a collar over that would only hold the verge back from the street it is a
        /// verge of. The answer is the drawn walk itself — the rings it is filled from (<c>GroundShapes.At</c>) — so
        /// a girth cleared of it is clear of what is drawn, at a corner as much as down a street.
        /// </para>
        /// <para>
        /// <b>And clear of the props already laid</b> (GEN-6c), asked in <see cref="Flush"/>. The ground cannot see
        /// them: a prop is no shape of the town's and claims nothing, because the only thing that ever has to know
        /// where one stands is the next candidate along.
        /// </para>
        /// <para>
        /// <b>The one point before the girth</b>, because a disc is walked from its rim and most of a verge and of a
        /// lattice is somebody's tarmac: reading the ground round a place that has already failed buys nothing.
        /// </para>
        /// </remarks>
        bool Stands(GroundShapes.Scan scan, Vector2 atM, float reachM) =>
            ground.At(scan, atM) == Ground.Grass
            && (standOffM <= 0f || !ground.PavingWithin(scan, atM, standOffM))
            && !ground.InAVerge(scan, atM, reachM)
            && ground.IsAll(scan, atM, reachM, stepM, Ground.Grass)
            && claims.IsFree(atM, reachM);
    }

    /// <summary>
    /// What a prop on a verge is (GEN-6b): a share of the street's furniture, and planting for the rest.
    /// </summary>
    static PropKind OnAVerge(SimConfig config, ref Rng draw)
    {
        if (draw.NextFloat() < config.CityGen.PropFurnitureShare) return PropKind.UrbanFurniture;

        // A verge is not a flower bed end to end: a share of what is planted along one is whatever the
        // country either side of the town grows anyway, which is what keeps a street from reading as a
        // catalogue of the things a town plants, laid out along the kerb.
        return draw.NextFloat() < config.CityGen.PropWildOnAVergeShare ? PropKind.WildNature : PropKind.UrbanNature;
    }

    /// <summary>
    /// <b>One look of a kind, each as often as the art's share of it says</b> (GEN-6e), off one draw <paramref name="u"/>
    /// in [0, 1) — and false, with nothing drawn, where the art holds none of the kind.
    /// </summary>
    static bool Drawn(PropSizes looks, PropKind kind, float u, out ushort look)
    {
        var drawn = looks.Draw(kind, u);
        look = (ushort)Math.Max(drawn, 0);
        return drawn >= 0;
    }
}
