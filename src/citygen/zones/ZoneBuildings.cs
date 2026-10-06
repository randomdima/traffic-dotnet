using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Zones;

/// <summary>
/// <b>A town's buildings: what its map sets down, its services, and prefabs stood along the walk as its zones say</b>
/// (GEN-58, GEN-54): the buildings the map sets down stand first, then a wheel's services on their yards
/// (<see cref="BuildingStage"/>), and then the walk's outer face is walked round every ring and at each place on it a
/// prefab of a look its zone builds is seated on the building line, wherever it stands clear of the ground and of what
/// stood before it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is built is the zone's</b> (<see cref="ZoneSettings"/>), asked <see cref="CityGenFigures.ZoneDepthM"/>
/// behind the building line where a front would start, and again behind the middle of the front a prefab is seated on,
/// where the zone has to build the same look: ground whose zone builds nothing — a park, a car park, the whole map's own
/// ground — builds nothing, and the walk is asked again a pitch on (<see cref="CityGenFigures.BuildingPitchM"/>).
/// <b>As much of the frontage is built as the zone says</b> (<see cref="ZoneSettings.Frontage"/>): after each building
/// the walk leaves open a stretch drawn about as long as the zone leaves beside a building of its width.
/// </para>
/// <para>
/// <b>How a zone builds is its own too</b>: which look is drawn by the zone's shares of them; whether the building
/// repeats the one before it on the walk or is drawn afresh by its variety; how far its front stands off the
/// carriageway by its front distance and spread — never nearer than the building line
/// (<see cref="SimConfig.BuildingLineM"/>); and how far it turns off square by its skew. Its way in is on the walk's
/// outer lane (<see cref="SimConfig.BuildingWayInM"/>) in front of it, however far back it stands. A front the walk will
/// not seat straight — across a bend, round a block's corner, across a junction's mouth, down the side of a rank of
/// bays — is not stood.
/// </para>
/// <para>
/// <b>Which prefab is a draw</b>, keyed on the map's seed and leaning to the zone's own footprint
/// (<see cref="Prefabs.Drawn"/>): up to <see cref="CityGenFigures.ZonePrefabsTried"/> of a look's prefabs are drawn in
/// turn, and the first that stands is the one. A prefab is <b>laid only as it
/// was drawn</b> — its first side along the walk, so the door drawn on that wall opens onto it.
/// </para>
/// <para>
/// <b>Each ring is walked on its own, and stood in order</b>: a block's buildings are its ring's alone, so every ring is
/// walked at once on its own draw, and what each stood is then stood again in ring order against all before it — a
/// building two rings both reach stands for the first. <b>The ground behind the frontage is built after it</b>
/// (<see cref="Behind"/>), against what stands along the streets. <b>A town that plans fewer buildings than its zones
/// stand</b> (<see cref="ZoneParam.Buildings"/>) keeps each along the streets by a draw at the share its plan has room
/// for, so what it thins is thinned evenly over the whole town rather than the last rings walked, and builds behind
/// them only with what room is left — and one that plans none stands none of its zones'.
/// </para>
/// </remarks>
internal static class ZoneBuildings
{
    /// <summary>
    /// How the walk was built, for the probe that reads it: every place asked and those of ground that builds nothing,
    /// how many stood were thinned to the town's plan and how many of those standing are behind the frontage, and by
    /// look how many buildings stood and how many places of that look stood none.
    /// </summary>
    internal sealed record Laying(
        CityPlan.BuildingArrays Buildings, int Places, int Open, int Thinned, int Behind, int[] StoodByLook, int[] RefusedByLook);

    /// <summary>One building stood on a ring, before the rings are stood together.</summary>
    readonly record struct Stood(int Prefab, Vector2 CentreM, Vector2 SizeM, float HeadingRad, Vector2 EntryM);

    public static CityPlan.BuildingArrays Lay(
        ZoneTree zones, Survey survey, BuildingStage.Yards yards, Paving paving, GroundShapes ground, BuildingSizes sizes,
        SimConfig config) =>
        Laid(zones, survey, yards, paving, ground, sizes, config).Buildings;

    public static Laying Laid(
        ZoneTree zones, Survey survey, BuildingStage.Yards yards, Paving paving, GroundShapes ground, BuildingSizes sizes,
        SimConfig config)
    {
        var (stoodByLook, refusedByLook) = (new int[ZoneParams.Looks], new int[ZoneParams.Looks]);
        var built = new Built();
        var standing = new Standing(config.CityGen.ZonePartyWallM, config.Grid.Main.CellM);
        var services = yards.Services;
        for (var service = 0; service < services.Count; service++)
        {
            standing.Add(services.CentreM[service], services.HeadingRad[service], services.SizeM[service]);
            built.Add(new Stood(-1, services.CentreM[service], services.SizeM[service], services.HeadingRad[service], services.EntryPointM[service]), services.Use[service], services.Capacity[service]);
        }

        if (sizes.PrefabM.Length == 0) return new Laying(built.Arrays(), 0, 0, 0, 0, stoodByLook, refusedByLook);

        var rings = paving.Rings(config);
        var faces = rings.WalkEdge;
        var face = new Face(rings, config);
        var prefabs = new Prefabs(sizes);
        SetDown(survey.Buildings, prefabs, new Working(face, ground), standing, built, stoodByLook, sizes, config);

        var room = zones.Zones.Own(Map.TownMap.ZoneArrays.Root, ZoneParam.Buildings) is { } planned ? (int)planned - built.Count : int.MaxValue;
        if (room <= 0) return new Laying(built.Arrays(), 0, 0, 0, 0, stoodByLook, refusedByLook);

        var ringStood = new Stood[faces.Length][];
        var tallies = new Tally[faces.Length];
        InChunks.Over(
            faces.Length,
            () => new Working(face, ground),
            (own, ring) => (ringStood[ring], tallies[ring]) = Along(faces[ring], ring, survey.Seed, zones, yards, prefabs, ground, config, own));

        var most = room == int.MaxValue ? int.MaxValue : built.Count + room;
        var thin = new Rng(survey.Seed, Streams.Building);
        var thinned = 0;
        StandAll(ringStood);
        var alongTheStreets = built.Count;

        // <b>Then the ground behind, against what stands along the streets</b>: drawn once the frontage is stood, so a
        // point behind is offered where the frontage left room and no draw is spent on ground already built.
        var builtBehind = Enumerable.Range(0, zones.Count).Where(zone => zones[zone].Builds && zones[zone].Interior >= config.CityGen.ZoneBehindLeast).ToArray();
        var behindStood = new Stood[builtBehind.Length][];
        if (built.Count < most)
        {
            InChunks.Over(
                builtBehind.Length,
                () => new Working(face, ground),
                (own, at) => behindStood[at] = Behind(builtBehind[at], survey.Seed, zones, prefabs, ground, standing, config, own));
            StandAll(behindStood);
        }

        var (places, open) = (0, 0);
        foreach (var tally in tallies)
        {
            (places, open) = (places + tally.Places, open + tally.Open);
            for (var look = 0; look < ZoneParams.Looks; look++) refusedByLook[look] += tally.Refused[look];
        }

        return new Laying(built.Arrays(), places, open, thinned, built.Count - alongTheStreets, stoodByLook, refusedByLook);

        // Every building of each pool kept at the share the plan still has room for, stood where nothing stood before it.
        void StandAll(Stood[][] pools)
        {
            var candidates = 0;
            foreach (var stood in pools) candidates += stood?.Length ?? 0;

            var left = most == int.MaxValue ? int.MaxValue : most - built.Count;
            var keepShare = candidates > left ? left / (float)candidates : 1f;
            foreach (var stood in pools)
            {
                foreach (var building in stood)
                {
                    if (thin.NextFloat() >= keepShare || built.Count >= most)
                    {
                        thinned++;
                        continue;
                    }

                    // A way in past the map's edge is a door nobody can stand at (GEN-2b).
                    if (building.EntryM is not { X: >= 0f, Y: >= 0f } entryM || entryM.X > survey.WidthM || entryM.Y > survey.HeightM) continue;
                    if (!standing.Clear(building.CentreM, building.HeadingRad, building.SizeM)) continue;

                    standing.Add(building.CentreM, building.HeadingRad, building.SizeM);
                    built.Add(building, BuildingUse.Ordinary, config.CityGen.BuildingCapacity);
                    stoodByLook[(int)sizes.PrefabLook[building.Prefab]]++;
                }
            }
        }
    }

    /// <summary>
    /// <b>The buildings the map sets down</b>, each as the prefab of its look nearest its size — every prefab where the
    /// catalogue draws none of its look — and its way in on the walk in front of it where the walk is within a front's
    /// reach of its front wall, else on that wall.
    /// </summary>
    static void SetDown(
        Map.TownMap.StoodArrays setDown, Prefabs prefabs, Working own, Standing standing, Built built, int[] stoodByLook,
        BuildingSizes sizes, SimConfig config)
    {
        for (var building = 0; building < setDown.Count; building++)
        {
            var (centreM, sizeM, headingRad) = (setDown.CentreM[building], setDown.SizeM[building], setDown.HeadingRad[building]);
            var prefab = prefabs.Nearest(setDown.Look[building], sizeM);
            var front = -Heading.RightOf(Heading.Unit(headingRad));
            var wallM = centreM - (front * (sizeM.Y * 0.5f));
            var entryM = own.Face.Nearest(own, wallM, config.CityGen.ZoneFrontageReachM, out var onM, out var along) is not null
                ? onM + (Heading.RightOf(along) * (config.WalkOuterM - config.BuildingWayInM))
                : wallM;

            standing.Add(centreM, headingRad, sizeM);
            built.Add(new Stood(prefab, centreM, sizeM, headingRad, entryM), BuildingUse.Ordinary, config.CityGen.BuildingCapacity);
            stoodByLook[(int)sizes.PrefabLook[prefab]]++;
        }
    }

    /// <summary>What one ring's walk asked and refused.</summary>
    readonly record struct Tally(int Places, int Open, int[] Refused);

    /// <summary>The building last stood along a ring, which the next may repeat.</summary>
    readonly record struct Before(int Zone, BuildingLook Look, int Prefab);

    /// <summary>
    /// <b>One ring of the walk's outer face, walked once</b>: at each place the zone behind it is asked what it builds,
    /// and a prefab of a look it builds stood against the last, or the walk moved on a pitch.
    /// </summary>
    static (Stood[] Stood, Tally Tally) Along(
        ArcSeg[] ring, int index, ulong seed, ZoneTree zones, BuildingStage.Yards yards, Prefabs prefabs, GroundShapes ground,
        SimConfig config, Working own)
    {
        var figures = config.CityGen;
        var draw = new Rng(seed, Streams.Building ^ (ulong)index);
        var lineM = config.BuildingLineM - config.WalkOuterM;
        var standing = new Standing(figures.ZonePartyWallM, config.Grid.Main.CellM);
        var stood = new List<Stood>();
        var (places, open, refused) = (0, 0, new int[ZoneParams.Looks]);
        var lengthM = Spline.TotalLengthM(ring);
        var cursor = default(SplineCursor);
        Before? before = null;
        var alongM = 0f;
        while (alongM < lengthM)
        {
            var on = Spline.SampleFrom(ring, alongM, ref cursor);
            var (along, outward) = (on.Direction, -on.Right);
            places++;

            // A ring is walked with the street on its right, so the ground built on is on its left.
            var zone = zones.At(on.PositionM + (outward * (lineM + figures.ZoneDepthM)));
            var said = zones[zone];
            if (!said.Builds || said.Frontage <= 0f)
            {
                open++;
                before = null;
                alongM += figures.BuildingPitchM;
                continue;
            }

            var repeat = draw.NextFloat() >= said.Variety && before is { } last && last.Zone == zone;
            var look = repeat ? before!.Value.Look : said.Look(draw.NextFloat());
            var widthM = Stand(zone, said, look, repeat ? before!.Value.Prefab : -1, on.PositionM, along);
            if (widthM > 0f)
            {
                // As much frontage left open after it as the zone leaves beside a building of its width, on average.
                alongM += widthM * (1f + ((1f / said.Frontage) - 1f) * 2f * draw.NextFloat());
                continue;
            }

            refused[(int)look]++;
            before = null;
            alongM += figures.BuildingPitchM;
        }

        return ([.. stood], new Tally(places, open, refused));

        // The front's width along the walk of the prefab that stood, or nought where none did.
        float Stand(int zone, ZoneSettings said, BuildingLook look, int again, Vector2 faceM, Vector2 along)
        {
            var wears = prefabs.Of(look);
            var backM = MathF.Max(0f, said.FrontM - config.BuildingLineM) + (said.FrontSpreadM > 0f ? draw.NextFloat(-said.FrontSpreadM, said.FrontSpreadM) : 0f);
            var skewRad = said.SkewDeg > 0f ? draw.NextFloat(-said.SkewDeg, said.SkewDeg) * (MathF.PI / 180f) : 0f;
            var tries = Math.Min(wears.Count, figures.ZonePrefabsTried) + (again >= 0 ? 1 : 0);
            for (var tried = 0; tried < tries; tried++)
            {
                var prefab = again >= 0 && tried == 0 ? again : wears[prefabs.Drawn(look, said.FootprintM2, figures.ZoneFootprintSpread, ref draw)];
                var sizeM = prefabs.SizeM(prefab);
                if (!yards.Allows(faceM + (along * (sizeM.X * 0.5f)), sizeM.X * 0.5f, own.Near)) continue;
                if (own.Face.Seat(own, faceM + (along * (sizeM.X * 0.5f)), along, sizeM.X, config) is not var (lineWallM, seated)) continue;

                var outward = -Heading.RightOf(seated);
                var wallM = lineWallM + (outward * MathF.Max(0f, backM));
                var headingRad = MathF.Atan2(seated.Y, seated.X) + skewRad;
                var centreM = wallM + (-Heading.RightOf(Heading.Unit(headingRad)) * (sizeM.Y * 0.5f));
                var there = zones.SettingsAt(wallM + (outward * figures.ZoneDepthM));
                if (there.LookShares[(int)look] <= 0f) continue;
                if (!OnClearGround(ground, own.Ground, centreM, Heading.Unit(headingRad), sizeM, config) || !standing.Clear(centreM, headingRad, sizeM)) continue;

                standing.Add(centreM, headingRad, sizeM);
                var entryM = lineWallM - (outward * (lineM + config.WalkOuterM - config.BuildingWayInM));
                stood.Add(new Stood(prefab, centreM, sizeM, headingRad, entryM));
                before = new Before(zone, look, prefab);
                return sizeM.X;
            }

            return 0f;
        }
    }

    /// <summary>
    /// <b>The ground behind one zone's frontage, built over as the zone says</b> (<see cref="ZoneSettings.Interior"/>):
    /// places drawn over the zone's box, each offered a prefab of a look the zone builds, square to the zone's bearing
    /// where it says one and to the nearest street where it does not — wherever it stands inside the zone, on clear
    /// ground, a front row's depth behind the walk (<see cref="CityGenFigures.ZoneBehindClearM"/>) and clear of what stood
    /// before it, with its way in on the nearest walk within reach (<see cref="CityGenFigures.ZoneBehindReachM"/>) —
    /// until the zone's share of its ground is built, or as many places are offered as
    /// <see cref="CityGenFigures.ZoneBehindOffered"/> for each building the share wants.
    /// </summary>
    /// <remarks>
    /// <b>The share is of the whole zone</b>, as the scanner measures it, and most of a zone is ground nothing may stand
    /// on — the walk's band, the paving, the frontage — so places are offered until the ground is covered rather than
    /// each standing for its share of a square; and <b>bounded by what the share wants and not by the zone's box</b>, so a
    /// district a city wide that builds a little behind its streets asks a little. <b>Each place's draws are made before
    /// it is asked about</b>, so what the stream has spent is the places offered and never what the ground answered.
    /// </remarks>
    /// <param name="frontage">What stands along the streets, read and never written while the zones are walked.</param>
    static Stood[] Behind(
        int zone, ulong seed, ZoneTree zones, Prefabs prefabs, GroundShapes ground, Standing frontage, SimConfig config, Working own)
    {
        var figures = config.CityGen;
        var said = zones[zone];
        var draw = new Rng(seed, Streams.Behind ^ (ulong)zone);
        var meanM2 = said.BehindM2 > 0f ? said.BehindM2 : prefabs.MeanM2(said.LookShares);
        var (leastM, mostM) = zones.BoxOf(zone);
        float? bearingRad = said.BearingDeg is { } bearingDeg ? MathF.Atan2(-MathF.Cos(bearingDeg * (MathF.PI / 180f)), MathF.Sin(bearingDeg * (MathF.PI / 180f))) : null;

        var standing = new Standing(figures.ZonePartyWallM, config.Grid.Main.CellM);
        var stood = new List<Stood>();
        var leftM2 = said.Interior * zones.AreaM2Of(zone);
        var offered = (int)MathF.Ceiling(leftM2 / meanM2 * figures.ZoneBehindOffered);
        for (var at = 0; at < offered && leftM2 > 0f; at++)
        {
            var atM = Vector2.Lerp(leastM, mostM, new Vector2(draw.NextFloat(), draw.NextFloat()));
            var look = said.Look(draw.NextFloat());
            var prefab = prefabs.Of(look)[prefabs.Drawn(look, said.BehindM2, figures.ZoneFootprintSpread, ref draw)];
            if (zones.At(atM) != zone || own.Face.Nearest(own, atM, figures.ZoneBehindClearM, out _, out _) is not null) continue;
            if (Walked(atM) is not var (onM, street)) continue;

            var sizeM = prefabs.SizeM(prefab);
            var headingRad = bearingRad ?? MathF.Atan2(street.Y, street.X);
            if (!standing.Clear(atM, headingRad, sizeM) || !frontage.Clear(atM, headingRad, sizeM)) continue;
            if (!OnClearGround(ground, own.Ground, atM, Heading.Unit(headingRad), sizeM, config)) continue;

            standing.Add(atM, headingRad, sizeM);
            stood.Add(new Stood(prefab, atM, sizeM, headingRad, onM + (Heading.RightOf(street) * (config.WalkOuterM - config.BuildingWayInM))));
            leftM2 -= sizeM.X * sizeM.Y;
        }

        return [.. stood];

        // The nearest place on the walk's outer face within reach and its bearing there, looked for out to a front's reach
        // and then twice as far each time, so no search holds more pieces than a working set has room for.
        (Vector2 OnM, Vector2 Street)? Walked(Vector2 atM)
        {
            for (var reachM = figures.ZoneFrontageReachM; ; reachM = MathF.Min(reachM * 2f, figures.ZoneBehindReachM))
            {
                if (own.Face.Nearest(own, atM, reachM, out var onM, out var street) is not null) return (onM, street);
                if (reachM >= figures.ZoneBehindReachM) return null;
            }
        }
    }

    /// <summary>
    /// <b>Whether a box stands wholly on grass and off every paving</b> (GEN-2b, GEN-2c, GEN-54), asked round its
    /// walls a ground step apart and at its middle: a street is a line, so one that reaches a box crosses its wall.
    /// </summary>
    static bool OnClearGround(GroundShapes ground, GroundShapes.Scan scan, Vector2 centreM, Vector2 along, Vector2 sizeM, SimConfig config)
    {
        if (!Clear(centreM)) return false;

        var across = Heading.RightOf(along);
        var halfM = sizeM * 0.5f;
        var stepM = config.Terrain.GroundStepM;
        Span<Vector2> corners = [centreM - (along * halfM.X) - (across * halfM.Y), centreM + (along * halfM.X) - (across * halfM.Y), centreM + (along * halfM.X) + (across * halfM.Y), centreM - (along * halfM.X) + (across * halfM.Y)];
        for (var side = 0; side < 4; side++)
        {
            var (fromM, toM) = (corners[side], corners[(side + 1) % 4]);
            var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(fromM, toM) / stepM));
            for (var step = 0; step < steps; step++)
            {
                if (!Clear(Vector2.Lerp(fromM, toM, step / (float)steps))) return false;
            }
        }

        return true;

        bool Clear(Vector2 atM) => ground.Is(scan, atM, Ground.Grass) && !ground.PavingWithin(scan, atM, 0f);
    }

    /// <summary>One thread's working set: the scans, the profile a front is seated through, and the ranks near a place.</summary>
    sealed class Working(Face face, GroundShapes ground)
    {
        public List<(float AlongM, float WalkM)> Profile { get; } = [];
        public Face Face { get; } = face;
        public ChainIndex.Scan FaceScan { get; } = face.NewScan();
        public GroundShapes.Scan Ground { get; } = ground.NewScan();
        public List<int> Near { get; } = [];
        public int[] Ids { get; } = new int[64];
        public float[] AlongM { get; } = new float[64];
    }

    /// <summary>
    /// <b>The walk's outer face</b> (<see cref="GroundRings.WalkEdge"/>), piece by piece over the town's grid, asked
    /// whether a front laid on it lies straight along it.
    /// </summary>
    sealed class Face
    {
        readonly ArcSeg[] _pieces;
        readonly ChainIndex _index;

        public Face(GroundRings rings, SimConfig config)
        {
            _pieces = ArcRings.Flat(rings.WalkEdge);
            _index = ChainIndex.OfPieces(_pieces, config.Grid.Main);
        }

        public ChainIndex.Scan NewScan() => _index.NewScan();

        /// <summary>
        /// <b>A front of a width seated on the walk about a place on it</b>: the middle of its wall and the bearing it
        /// runs on, or nothing where the walk under it will not take one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The wall is the straight line nearest the walk under it, stood back until no part of it is on the
        /// walk</b>: the walk is read a ground step apart along the front, a line fitted to it by least squares, and
        /// the line moved off the street by the building line past the furthest the walk reaches beyond it — so a
        /// front on a gentle curve touches the kerb where the curve comes nearest and stands off it elsewhere.
        /// </para>
        /// <para>
        /// <b>It is refused where it would stand back further than <see cref="CityGenFigures.ZoneFrontageStraightM"/>
        /// anywhere</b>, or where the walk under any part of it turns more than <see cref="CityGenFigures.ZoneFrontageTurnDeg"/>
        /// off its bearing — a front round a block's corner, or across a junction's mouth — or is further than
        /// <see cref="CityGenFigures.ZoneFrontageReachM"/> from it.
        /// </para>
        /// </remarks>
        public (Vector2 WallM, Vector2 Along)? Seat(Working own, Vector2 onM, Vector2 along, float widthM, SimConfig config)
        {
            var figures = config.CityGen;
            var turnCos = MathF.Cos(figures.ZoneFrontageTurnDeg * (MathF.PI / 180f));
            var samples = Math.Max(2, (int)MathF.Ceiling(widthM / config.Terrain.GroundStepM) + 1);

            // Where the walk stands under each place along the front, as a distance off the street from the line it was asked on.
            var (sumT, sumP, sumTT, sumTP) = (0f, 0f, 0f, 0f);
            own.Profile.Clear();
            for (var sample = 0; sample < samples; sample++)
            {
                var alongM = widthM * ((sample / (float)(samples - 1)) - 0.5f);
                var atM = onM + (along * alongM);
                if (Nearest(own, atM, figures.ZoneFrontageReachM, out var faceM, out var direction) is null) return null;
                if (Vector2.Dot(direction, along) < turnCos) return null;

                var walkM = -Vector2.Dot(atM - faceM, -Heading.RightOf(direction));
                own.Profile.Add((alongM, walkM));
                (sumT, sumP, sumTT, sumTP) = (sumT + alongM, sumP + walkM, sumTT + (alongM * alongM), sumTP + (alongM * walkM));
            }

            var spread = (samples * sumTT) - (sumT * sumT);
            var slope = spread > 0f ? ((samples * sumTP) - (sumT * sumP)) / spread : 0f;
            var middleM = (sumP - (slope * sumT)) / samples;

            var (leastM, mostM) = (float.MaxValue, float.MinValue);
            foreach (var (alongM, walkM) in own.Profile)
            {
                var pastM = walkM - (middleM + (slope * alongM));
                (leastM, mostM) = (MathF.Min(leastM, pastM), MathF.Max(mostM, pastM));
            }

            if (mostM - leastM > figures.ZoneFrontageStraightM) return null;

            var outward = -Heading.RightOf(along);
            var lineM = config.BuildingLineM - config.WalkOuterM;
            var wallM = onM + (outward * (middleM + mostM + lineM + LineTolerance.JoinedM));
            return (wallM, Vector2.Normalize(along + (outward * slope)));
        }

        /// <summary>The nearest place on the face within a reach of a place, and the face's bearing there, or null where none is.</summary>
        public float? Nearest(Working own, Vector2 atM, float reachM, out Vector2 onM, out Vector2 direction)
        {
            (onM, direction) = (default, default);
            var found = Math.Min(_index.Near(own.FaceScan, atM, reachM, own.Ids, own.AlongM), own.Ids.Length);
            float? bestM = null;
            for (var at = 0; at < found; at++)
            {
                var piece = _pieces[own.Ids[at]];
                var pointM = piece.PointAtM(own.AlongM[at]);
                var offM = Vector2.Distance(pointM, atM);
                if (bestM is { } nearer && offM >= nearer) continue;

                (bestM, onM, direction) = (offM, pointM, Heading.Unit(piece.HeadingAtRad(own.AlongM[at])));
            }

            return bestM;
        }
    }

    /// <summary>The prefabs by look (<see cref="BuildingSizes.PrefabM"/>): a look's own, or every one where the catalogue draws nothing of it.</summary>
    sealed class Prefabs
    {
        readonly Vector2[] _sizeM;
        readonly List<int>[] _byLook;
        readonly List<int> _all = [];

        public Prefabs(BuildingSizes sizes)
        {
            _sizeM = sizes.PrefabM;
            _byLook = new List<int>[ZoneParams.Looks];
            for (var look = 0; look < _byLook.Length; look++) _byLook[look] = [];
            for (var prefab = 0; prefab < _sizeM.Length; prefab++)
            {
                _byLook[(int)sizes.PrefabLook[prefab]].Add(prefab);
                _all.Add(prefab);
            }
        }

        public List<int> Of(BuildingLook look) => _byLook[(int)look].Count > 0 ? _byLook[(int)look] : _all;

        public Vector2 SizeM(int prefab) => _sizeM[prefab];

        /// <summary>
        /// <b>Which of a look's prefabs is drawn</b>, as its place in <see cref="Of"/>: any alike where the zone says no
        /// footprint, and else each the more often the nearer its ground is to the zone's — by a bell over the ratio of
        /// the two, a prefab <paramref name="spread"/> times bigger or smaller drawn about three-fifths as often.
        /// </summary>
        public int Drawn(BuildingLook look, float footprintM2, float spread, ref Rng draw)
        {
            var wears = Of(look);
            if (footprintM2 <= 0f) return draw.NextInt(wears.Count);

            var width = MathF.Log(spread);
            var total = 0f;
            foreach (var prefab in wears) total += Weight(prefab);

            var left = draw.NextFloat() * total;
            for (var at = 0; at < wears.Count; at++)
            {
                left -= Weight(wears[at]);
                if (left < 0f) return at;
            }

            return wears.Count - 1;

            float Weight(int prefab)
            {
                var off = MathF.Log(_sizeM[prefab].X * _sizeM[prefab].Y / footprintM2) / width;
                return MathF.Exp(-0.5f * off * off);
            }
        }

        /// <summary>The ground a building of a mix of looks covers on average, each look's prefabs alike.</summary>
        public float MeanM2(float[] lookShares)
        {
            var meanM2 = 0f;
            for (var look = 0; look < lookShares.Length; look++)
            {
                if (lookShares[look] <= 0f) continue;

                var wears = Of((BuildingLook)look);
                var groundM2 = 0f;
                foreach (var prefab in wears) groundM2 += _sizeM[prefab].X * _sizeM[prefab].Y;
                meanM2 += lookShares[look] * groundM2 / wears.Count;
            }

            return meanM2;
        }

        /// <summary>The prefab of a look whose footprint, laid either way round, is nearest a size.</summary>
        public int Nearest(BuildingLook look, Vector2 sizeM)
        {
            var (best, bestM) = (-1, float.MaxValue);
            foreach (var prefab in Of(look))
            {
                var authoredM = _sizeM[prefab];
                var offM = MathF.Min(
                    MathF.Abs(authoredM.X - sizeM.X) + MathF.Abs(authoredM.Y - sizeM.Y),
                    MathF.Abs(authoredM.X - sizeM.Y) + MathF.Abs(authoredM.Y - sizeM.X));
                if (offM < bestM) (best, bestM) = (prefab, offM);
            }

            return best;
        }
    }

    /// <summary>
    /// <b>The buildings stood so far, filed by the town's grid</b>, asked whether a box stands clear of them: two
    /// boxes may stand into each other by a party wall's half and no more.
    /// </summary>
    sealed class Standing(float partyWallM, float cellM)
    {
        readonly List<(Vector2 CentreM, Vector2 Along, Vector2 HalfM)> _boxes = [];
        readonly Dictionary<(int, int), List<int>> _cells = [];

        public bool Clear(Vector2 centreM, float headingRad, Vector2 sizeM)
        {
            var along = Heading.Unit(headingRad);
            var halfM = Inside(sizeM);
            var (least, most) = Cells(centreM, sizeM);
            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var filed)) continue;

                    foreach (var index in filed)
                    {
                        var (otherM, otherAlong, otherHalfM) = _boxes[index];
                        if (Overlap(centreM, along, halfM, otherM, otherAlong, otherHalfM)) return false;
                    }
                }
            }

            return true;
        }

        public void Add(Vector2 centreM, float headingRad, Vector2 sizeM)
        {
            var index = _boxes.Count;
            _boxes.Add((centreM, Heading.Unit(headingRad), Inside(sizeM)));
            var (least, most) = Cells(centreM, sizeM);
            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var filed)) _cells[(x, y)] = filed = [];
                    filed.Add(index);
                }
            }
        }

        /// <summary>A box's half sides less the party wall, never quite nothing.</summary>
        Vector2 Inside(Vector2 sizeM) => Vector2.Max((sizeM * 0.5f) - new Vector2(partyWallM), new Vector2(LineTolerance.JoinedM));

        ((int X, int Y), (int X, int Y)) Cells(Vector2 centreM, Vector2 sizeM)
        {
            var reachM = sizeM.Length() * 0.5f;
            return (((int)MathF.Floor((centreM.X - reachM) / cellM), (int)MathF.Floor((centreM.Y - reachM) / cellM)),
                    ((int)MathF.Floor((centreM.X + reachM) / cellM), (int)MathF.Floor((centreM.Y + reachM) / cellM)));
        }

        /// <summary>Two boxes on bearings of their own overlap unless one of their four axes parts them.</summary>
        static bool Overlap(Vector2 aM, Vector2 aAlong, Vector2 aHalfM, Vector2 bM, Vector2 bAlong, Vector2 bHalfM)
        {
            var (aAcross, bAcross) = (Heading.RightOf(aAlong), Heading.RightOf(bAlong));
            var apartM = bM - aM;
            foreach (var axis in (ReadOnlySpan<Vector2>)[aAlong, aAcross, bAlong, bAcross])
            {
                var aReachM = (aHalfM.X * MathF.Abs(Vector2.Dot(aAlong, axis))) + (aHalfM.Y * MathF.Abs(Vector2.Dot(aAcross, axis)));
                var bReachM = (bHalfM.X * MathF.Abs(Vector2.Dot(bAlong, axis))) + (bHalfM.Y * MathF.Abs(Vector2.Dot(bAcross, axis)));
                if (MathF.Abs(Vector2.Dot(apartM, axis)) >= aReachM + bReachM) return false;
            }

            return true;
        }
    }

    /// <summary>The buildings as they are stood, one list a field: a service's prefab is none, and it wears its use's roof.</summary>
    sealed class Built
    {
        readonly List<Vector2> _centreM = [];
        readonly List<Vector2> _sizeM = [];
        readonly List<float> _headingRad = [];
        readonly List<Vector2> _entryM = [];
        readonly List<int> _prefab = [];
        readonly List<int> _capacity = [];
        readonly List<BuildingUse> _use = [];

        public int Count => _centreM.Count;

        public void Add(Stood building, BuildingUse use, int capacity)
        {
            _centreM.Add(building.CentreM);
            _sizeM.Add(building.SizeM);
            _headingRad.Add(building.HeadingRad);
            _entryM.Add(building.EntryM);
            _prefab.Add(building.Prefab);
            _capacity.Add(capacity);
            _use.Add(use);
        }

        public CityPlan.BuildingArrays Arrays()
        {
            var offsets = new int[_centreM.Count + 1];
            for (var at = 0; at < offsets.Length; at++) offsets[at] = at;

            return new CityPlan.BuildingArrays
            {
                CentreM = [.. _centreM],
                SizeM = [.. _sizeM],
                HeadingRad = [.. _headingRad],
                Capacity = [.. _capacity],
                Use = [.. _use],
                EntryOffsets = offsets,
                EntryPointM = [.. _entryM],
                Prefab = [.. _prefab],
            };
        }
    }
}
