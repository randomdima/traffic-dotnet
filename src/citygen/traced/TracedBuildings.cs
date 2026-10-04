using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A traced town's buildings: prefabs stood on the walk where its footprints are</b> (GEN-57, GEN-54): every
/// footprint cut into the rectangles it is built of (<see cref="FootprintParts"/>), each moved onto the walk's outer
/// face nearest it, turned square to it and worn as the prefab of its look nearest its size and roundness — or, where
/// that cannot be done cleanly, nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every building fronts the walk, as a generated town's does</b>: the wall of a rectangle that faces the walk
/// nearest it is its front, and the rectangle is moved square onto the face there, its front wall on the building
/// line (<see cref="SimConfig.BuildingLineM"/>) and its way in on the walk's outer lane
/// (<see cref="SimConfig.BuildingWayInM"/>). One whose front is further than
/// <see cref="CityGenFigures.TracedFrontageReachM"/> from the line, or whose front would not lie straight along it —
/// across a bend, round a block's corner — stands nowhere.
/// </para>
/// <para>
/// <b>A rectangle longer than its look's longest prefab is cut into equal sections</b> before it is worn: along the
/// face they each front it, and back from it they stand one behind another, each only where the one in front of it
/// stood — a slab a hundred metres deep is the stairwell sections it was built in.
/// </para>
/// <para>
/// <b>Read in parallel, stood in order</b>: where a rectangle would stand and which prefabs would stand there on
/// clear ground is a fact about its footprint alone, asked of every footprint at once. Whether a prefab stands clear
/// of the buildings before it is the one question the order answers, so it is walked serially, <b>the least moved
/// first</b> — what the survey already put on the walk claims its frontage before anything moved onto it — and a
/// rectangle takes the nearest of its prefabs that stands clear, or nothing (GEN-8).
/// </para>
/// </remarks>
internal static class TracedBuildings
{
    /// <summary>One prefab offered to a section: which, and the box it would stand in.</summary>
    readonly record struct Candidate(int Prefab, Vector2 CentreM, Vector2 SizeM);

    /// <summary>
    /// One section of a rectangle, ready to be stood: where it is among its rectangle's sections, how far its rectangle
    /// was moved, its bearing, its way in, and its prefabs, nearest first.
    /// </summary>
    readonly record struct Offer(
        int Footprint, int Part, int Column, int Row, float MovedM, BuildingLook Look, Vector2 SectionM, float CornerShare,
        float HeadingRad, Vector2 EntryM, Candidate[] Candidates, Fate Refused);

    /// <summary>What became of one section: stood, or why not.</summary>
    internal enum Fate : byte
    {
        Stood,

        /// <summary>No prefab near its size stood wholly on grass and off every paving.</summary>
        OffTheGround,

        /// <summary>The walk under its front bends, jogs or turns a corner more than a front may.</summary>
        Crooked,

        /// <summary>Every prefab that would have stood stands in a building stood before it.</summary>
        Crowded,

        /// <summary>It stands behind a section of its rectangle that did not stand.</summary>
        NothingInFront,
    }

    /// <summary>One section offered, for the probe: what it is, how big, how round, how far moved, what became of it and the building it stood as or −1.</summary>
    internal readonly record struct Section(int Footprint, BuildingLook Look, int Row, Vector2 SizeM, float CornerShare, float MovedM, Fate Fate, int Building);

    /// <summary>
    /// What became of a traced town's footprints, for the probe that reads how well they were fitted: every section
    /// offered, and how many rectangles stood nowhere for being too far from any walk.
    /// </summary>
    internal sealed record Fitting(CityPlan.BuildingArrays Buildings, Section[] Offered, int Footprints, int Parts, int TooFar);

    public static CityPlan.BuildingArrays Lay(
        CityPlan.FootprintArrays footprints, Paving paving, GroundShapes ground, BuildingSizes sizes, SimConfig config) =>
        Fit(footprints, paving, ground, sizes, config).Buildings;

    public static Fitting Fit(
        CityPlan.FootprintArrays footprints, Paving paving, GroundShapes ground, BuildingSizes sizes, SimConfig config)
    {
        var face = new Face(paving.Rings(config), config);
        var prefabs = new Prefabs(sizes, config.CityGen);
        var offered = new Offer[footprints.Count][];
        var parts = new int[footprints.Count];
        var tooFar = new int[footprints.Count];
        InChunks.Over(
            footprints.Count,
            () => new Working(face, ground),
            (own, footprint) => offered[footprint] = OffersOf(footprints, footprint, face, ground, prefabs, config, own, out parts[footprint], out tooFar[footprint]));

        var offers = offered.SelectMany(offer => offer).ToArray();
        Array.Sort(offers, static (a, b) =>
            a.MovedM != b.MovedM ? a.MovedM.CompareTo(b.MovedM)
            : a.Footprint != b.Footprint ? a.Footprint.CompareTo(b.Footprint)
            : a.Part != b.Part ? a.Part.CompareTo(b.Part)
            : a.Row != b.Row ? a.Row.CompareTo(b.Row)
            : a.Column.CompareTo(b.Column));

        var standing = new Standing(config.CityGen.TracedPartyWallM, config.Grid.Main.CellM);
        var built = new Built();
        var stood = new HashSet<(int Footprint, int Part, int Column, int Row)>();
        var sections = new Section[offers.Length];
        for (var at = 0; at < offers.Length; at++)
        {
            var offer = offers[at];
            var (building, fate) = (-1, offer.Candidates.Length == 0 ? offer.Refused : Fate.Crowded);
            if (offer.Row > 0 && !stood.Contains((offer.Footprint, offer.Part, offer.Column, offer.Row - 1)))
            {
                fate = Fate.NothingInFront;
            }
            else
            {
                foreach (var candidate in offer.Candidates)
                {
                    if (!standing.Clear(candidate.CentreM, offer.HeadingRad, candidate.SizeM)) continue;

                    standing.Add(candidate.CentreM, offer.HeadingRad, candidate.SizeM);
                    (building, fate) = (built.Add(candidate, offer, config.CityGen.BuildingCapacity), Fate.Stood);
                    stood.Add((offer.Footprint, offer.Part, offer.Column, offer.Row));
                    break;
                }
            }

            sections[at] = new Section(offer.Footprint, offer.Look, offer.Row, offer.SectionM, offer.CornerShare, offer.MovedM, fate, building);
        }

        return new Fitting(built.Arrays(), sections, footprints.Count, parts.Sum(), tooFar.Sum());
    }

    /// <summary>
    /// Every section of every rectangle one footprint is cut into that fronts a walk, each with the prefabs that would
    /// stand there cleanly; and how many rectangles it was cut into, and how many of them front none.
    /// </summary>
    static Offer[] OffersOf(
        CityPlan.FootprintArrays footprints, int footprint, Face face, GroundShapes ground, Prefabs prefabs, SimConfig config,
        Working own, out int parts, out int tooFar)
    {
        own.Parts.Clear();
        own.Cutter.Cut(footprints, footprint, config.CityGen, own.Parts);
        (parts, tooFar) = (own.Parts.Count, 0);
        if (own.Parts.Count == 0) return [];

        var areaM2 = 0f;
        foreach (var part in own.Parts) areaM2 += part.SizeM.X * part.SizeM.Y;

        var look = LookOf(footprints.Use[footprint], footprints.HeightM[footprint], areaM2, config.CityGen);
        var longestM = prefabs.LongestM(look);
        own.Offers.Clear();
        for (var part = 0; part < own.Parts.Count; part++)
        {
            var offered = own.Offers.Count;
            var fronted = false;
            foreach (var front in face.Fronts(own, own.Parts[part], config))
            {
                fronted = true;
                Sections(footprint, part, look, own.Parts[part], front, longestM, ground, prefabs, config, own);
                if (own.Offers.Skip(offered).Any(offer => offer.Row == 0 && offer.Candidates.Length > 0)) break;

                own.Offers.RemoveRange(offered, own.Offers.Count - offered);
            }

            if (!fronted) tooFar++;
        }

        return [.. own.Offers];
    }

    /// <summary>
    /// <b>One rectangle moved onto the walk through one of its walls</b>, cut into sections no longer than its look's
    /// longest prefab, each section offered the prefabs that would stand there cleanly.
    /// </summary>
    static void Sections(
        int footprint, int part, BuildingLook look, FootprintPart rectangle, Face.Frontage front, float longestM,
        GroundShapes ground, Prefabs prefabs, SimConfig config, Working own)
    {
        var columns = Math.Max(1, (int)MathF.Ceiling(front.WidthM / longestM));
        var rows = Math.Max(1, (int)MathF.Ceiling(front.DepthM / longestM));
        var sectionM = new Vector2(front.WidthM / columns, front.DepthM / rows);
        var cornerShare = columns * rows == 1 ? rectangle.CornerShare : 0f;
        var lineM = config.BuildingLineM - config.WalkOuterM;

        for (var column = 0; column < columns; column++)
        {
            var onM = front.AtM + (front.Along * (((column + 0.5f) * sectionM.X) - (front.WidthM * 0.5f)));
            var nearest = prefabs.Nearest(look, sectionM, cornerShare, own.Fits);
            if (nearest.Count > config.CityGen.TracedPrefabsTried) nearest.RemoveRange(config.CityGen.TracedPrefabsTried, nearest.Count - config.CityGen.TracedPrefabsTried);

            // The widest front any of them would need that the walk will seat, which seats every narrower one too.
            (Vector2 WallM, Vector2 Along)? seat = null;
            var seatedM = 0f;
            foreach (var widthM in nearest.Select(fit => fit.SizeM.X).Distinct().OrderDescending())
            {
                if (own.Face.Seat(own, onM, front.Along, widthM, config) is not { } found) continue;

                (seat, seatedM) = (found, widthM);
                break;
            }

            if (seat is not var (wallM, along))
            {
                for (var row = 0; row < rows; row++)
                {
                    own.Offers.Add(new Offer(
                        footprint, part, column, row, front.MovedM, look, sectionM, cornerShare, 0f, default, [], Fate.Crooked));
                }

                continue;
            }

            var outward = -Heading.RightOf(along);
            var entryM = wallM - (outward * (lineM + config.WalkOuterM - config.BuildingWayInM));
            var headingRad = MathF.Atan2(along.Y, along.X);
            for (var row = 0; row < rows; row++)
            {
                own.Candidates.Clear();
                foreach (var (prefab, sizeM, _) in nearest)
                {
                    if (sizeM.X > seatedM) continue;

                    var centreM = wallM + (outward * ((row * sectionM.Y) + (sizeM.Y * 0.5f)));
                    if (OnClearGround(ground, own.Ground, centreM, along, sizeM, config)) own.Candidates.Add(new Candidate(prefab, centreM, sizeM));
                }

                own.Offers.Add(new Offer(
                    footprint, part, column, row, front.MovedM, look, sectionM, cornerShare, headingRad, entryM, [.. own.Candidates],
                    Fate.OffTheGround));
            }
        }
    }

    /// <summary>
    /// <b>What a building is drawn as</b>, off what the survey says it is for and, for a home it says no more of, how
    /// tall it stands or else how much ground it covers.
    /// </summary>
    public static BuildingLook LookOf(FootprintUse use, float heightM, float areaM2, CityGenFigures figures) => use switch
    {
        FootprintUse.House => BuildingLook.House,
        FootprintUse.Apartments => heightM >= figures.TracedTowerHeightM ? BuildingLook.Tower : BuildingLook.Apartments,
        FootprintUse.Garages => BuildingLook.Garages,
        FootprintUse.Shed => BuildingLook.Shed,
        FootprintUse.Kiosk => areaM2 <= figures.TracedShedLargestM2 ? BuildingLook.Kiosk : BuildingLook.Retail,
        FootprintUse.Retail => BuildingLook.Retail,
        FootprintUse.Office => BuildingLook.Office,
        FootprintUse.Industrial or FootprintUse.School or FootprintUse.Hospital or FootprintUse.Religious
            when areaM2 <= figures.TracedShedLargestM2 => BuildingLook.Shed,
        FootprintUse.Industrial => BuildingLook.Industrial,
        FootprintUse.School => BuildingLook.School,
        FootprintUse.Hospital => BuildingLook.Hospital,
        FootprintUse.Religious => BuildingLook.Religious,
        FootprintUse.Canopy => BuildingLook.Canopy,
        FootprintUse.Greenhouse => BuildingLook.Greenhouse,
        FootprintUse.Unknown when heightM <= 0f && areaM2 >= figures.TracedWorksSmallestM2 => BuildingLook.Industrial,
        _ => HomeOf(heightM, areaM2, figures),
    };

    static BuildingLook HomeOf(float heightM, float areaM2, CityGenFigures figures)
    {
        if (heightM >= figures.TracedTowerHeightM) return BuildingLook.Tower;
        if (heightM >= figures.TracedFlatsHeightM || areaM2 > figures.TracedHouseLargestM2) return BuildingLook.Apartments;
        return heightM <= 0f && areaM2 <= figures.TracedShedLargestM2 ? BuildingLook.Shed : BuildingLook.House;
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

    /// <summary>One thread's working set: the cutter's grid, the scans, and the lists a footprint is offered through.</summary>
    sealed class Working(Face face, GroundShapes ground)
    {
        public FootprintParts Cutter { get; } = new();
        public List<FootprintPart> Parts { get; } = [];
        public List<Offer> Offers { get; } = [];
        public List<Candidate> Candidates { get; } = [];
        public List<Face.Frontage> Fronts { get; } = [];
        public List<(float AlongM, float WalkM)> Profile { get; } = [];
        public List<(int Prefab, Vector2 SizeM, float Error)> Fits { get; } = [];
        public Face Face { get; } = face;
        public ChainIndex.Scan FaceScan { get; } = face.NewScan();
        public GroundShapes.Scan Ground { get; } = ground.NewScan();
        public int[] Ids { get; } = new int[64];
        public float[] AlongM { get; } = new float[64];
    }

    /// <summary>
    /// <b>The walk's outer face</b> (<see cref="GroundRings.WalkEdge"/>), piece by piece over the town's grid, asked
    /// which stretch of it a rectangle could be moved onto and whether a front laid on it lies straight along it.
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
        /// The place on the face a rectangle is moved to, the face's own direction there and the way off the street,
        /// the front's length and the depth behind it, and how far the front is moved to stand on the building line.
        /// </summary>
        public readonly record struct Frontage(Vector2 AtM, Vector2 Along, Vector2 Outward, float WidthM, float DepthM, float MovedM);

        /// <summary>
        /// <b>Each wall a rectangle could front the walk through, nearest first</b>: a wall facing the stretch of face
        /// nearest it — within a quarter turn of square to it, so any rectangle has one — with the rectangle on the side
        /// of it off the street, and no further from the building line than the reach. A face is walked with the street
        /// on its right, so the way off the street is its left.
        /// </summary>
        public List<Frontage> Fronts(Working own, FootprintPart part, SimConfig config)
        {
            var reachM = config.CityGen.TracedFrontageReachM;
            var lineM = config.BuildingLineM - config.WalkOuterM;
            var along = Heading.Unit(part.HeadingRad);
            var across = Heading.RightOf(along);
            own.Fronts.Clear();
            foreach (var (normal, widthM, depthM) in (ReadOnlySpan<(Vector2, float, float)>)
                     [(across, part.SizeM.X, part.SizeM.Y), (-across, part.SizeM.X, part.SizeM.Y), (along, part.SizeM.Y, part.SizeM.X), (-along, part.SizeM.Y, part.SizeM.X)])
            {
                var wallM = part.CentreM + (normal * (depthM * 0.5f));
                if (Nearest(own, wallM, reachM + lineM, out var atM, out var direction) is null) continue;

                var outward = -Heading.RightOf(direction);
                if (Vector2.Dot(normal, -outward) < MathF.Sqrt(0.5f) || Vector2.Dot(part.CentreM - atM, outward) <= 0f) continue;

                var movedM = MathF.Abs(Vector2.Dot(wallM - atM, outward) - lineM);
                if (movedM <= reachM) own.Fronts.Add(new Frontage(atM, direction, outward, widthM, depthM, movedM));
            }

            own.Fronts.Sort(static (a, b) => a.MovedM.CompareTo(b.MovedM));
            return own.Fronts;
        }

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
        /// <b>It is refused where it would stand back further than <see cref="CityGenFigures.TracedFrontageStraightM"/>
        /// anywhere</b>, or where the walk under any part of it turns more than <see cref="CityGenFigures.TracedFrontageTurnDeg"/>
        /// off its bearing — a front round a block's corner, or across a junction's mouth.
        /// </para>
        /// </remarks>
        public (Vector2 WallM, Vector2 Along)? Seat(Working own, Vector2 onM, Vector2 along, float widthM, SimConfig config)
        {
            var figures = config.CityGen;
            var outward = -Heading.RightOf(along);
            var turnCos = MathF.Cos(figures.TracedFrontageTurnDeg * (MathF.PI / 180f));
            var samples = Math.Max(2, (int)MathF.Ceiling(widthM / config.Terrain.GroundStepM) + 1);

            // Where the walk stands under each place along the front, as a distance off the street from the line it was asked on.
            var (sumT, sumP, sumTT, sumTP) = (0f, 0f, 0f, 0f);
            own.Profile.Clear();
            for (var sample = 0; sample < samples; sample++)
            {
                var alongM = widthM * ((sample / (float)(samples - 1)) - 0.5f);
                var atM = onM + (along * alongM);
                if (Nearest(own, atM, figures.TracedFrontageReachM, out var faceM, out var direction) is null) return null;
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

            if (mostM - leastM > figures.TracedFrontageStraightM) return null;

            var lineM = config.BuildingLineM - config.WalkOuterM;
            var wallM = onM + (outward * (middleM + mostM + lineM + LineTolerance.JoinedM));
            return (wallM, Vector2.Normalize(along + (outward * slope)));
        }

        float? Nearest(Working own, Vector2 atM, float reachM, out Vector2 onM, out Vector2 direction)
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

    /// <summary>
    /// <b>The prefabs by look</b> (<see cref="BuildingSizes.PrefabM"/>), offered to a section nearest first. A prefab
    /// is a rounded rectangle <b>laid only as it was drawn</b> — its first side along the walk, so the door drawn on
    /// that wall opens onto it; turned a quarter, its door would face along the street. A plot narrower at the street
    /// than it is deep wears a prefab drawn that way, its door on its narrow end. <b>Nearest is by ratio</b>, a size half again as big
    /// as far off as one two thirds as big — a prefab larger than the section further off than one as much smaller
    /// (<see cref="CityGenFigures.TracedPrefabLargerWeighs"/>) — and by how much rounder or squarer it is
    /// (<see cref="CityGenFigures.TracedPrefabRoundWeighs"/>).
    /// </summary>
    /// <remarks>A town handed no prefab at all stands each section as itself.</remarks>
    sealed class Prefabs
    {
        readonly Vector2[] _sizeM;
        readonly float[] _cornerShare;
        readonly List<int>[] _byLook;
        readonly List<int> _all = [];
        readonly float _largerWeighs;
        readonly float _roundWeighs;

        public Prefabs(BuildingSizes sizes, CityGenFigures figures)
        {
            (_largerWeighs, _roundWeighs) = (figures.TracedPrefabLargerWeighs, figures.TracedPrefabRoundWeighs);
            _sizeM = sizes.PrefabM;
            _cornerShare = new float[_sizeM.Length];
            _byLook = new List<int>[Enum.GetValues<BuildingLook>().Length];
            for (var look = 0; look < _byLook.Length; look++) _byLook[look] = [];
            for (var prefab = 0; prefab < _sizeM.Length; prefab++)
            {
                _cornerShare[prefab] = sizes.PrefabCornerM[prefab] / (MathF.Min(_sizeM[prefab].X, _sizeM[prefab].Y) * 0.5f);
                _byLook[(int)sizes.PrefabLook[prefab]].Add(prefab);
                _all.Add(prefab);
            }
        }

        /// <summary>The prefabs a look wears: its own, or every one where the catalogue draws nothing of it.</summary>
        List<int> Of(BuildingLook look) => _byLook[(int)look].Count > 0 ? _byLook[(int)look] : _all;

        /// <summary>The longest side of any prefab a look wears; no bound where there is no prefab.</summary>
        public float LongestM(BuildingLook look)
        {
            var longestM = 0f;
            foreach (var prefab in Of(look)) longestM = MathF.Max(longestM, MathF.Max(_sizeM[prefab].X, _sizeM[prefab].Y));

            return longestM > 0f ? longestM : float.PositiveInfinity;
        }

        /// <summary>The nearest prefabs of a look to a section, its <c>x</c> the front.</summary>
        public List<(int Prefab, Vector2 SizeM, float Error)> Nearest(
            BuildingLook look, Vector2 sizeM, float cornerShare, List<(int Prefab, Vector2 SizeM, float Error)> into)
        {
            into.Clear();
            if (_sizeM.Length == 0)
            {
                into.Add((-1, sizeM, 0f));
                return into;
            }

            foreach (var prefab in Of(look))
            {
                var laidM = _sizeM[prefab];
                var roundness = MathF.Abs(_cornerShare[prefab] - cornerShare) * _roundWeighs;
                into.Add((prefab, laidM, Off(laidM.X / sizeM.X) + Off(laidM.Y / sizeM.Y) + roundness));
            }

            into.Sort(static (a, b) => a.Error != b.Error ? a.Error.CompareTo(b.Error) : a.Prefab.CompareTo(b.Prefab));
            return into;

            float Off(float ratio) => ratio >= 1f ? MathF.Log(ratio) * _largerWeighs : -MathF.Log(ratio);
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

    /// <summary>The buildings as they are stood, one list a field.</summary>
    sealed class Built
    {
        readonly List<Vector2> _centreM = [];
        readonly List<Vector2> _sizeM = [];
        readonly List<float> _headingRad = [];
        readonly List<Vector2> _entryM = [];
        readonly List<int> _prefab = [];
        readonly List<int> _capacity = [];

        /// <summary>One building more, and its number.</summary>
        public int Add(Candidate candidate, Offer offer, int capacity)
        {
            _centreM.Add(candidate.CentreM);
            _sizeM.Add(candidate.SizeM);
            _headingRad.Add(offer.HeadingRad);
            _entryM.Add(offer.EntryM);
            _prefab.Add(candidate.Prefab);
            _capacity.Add(capacity);
            return _centreM.Count - 1;
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
                Use = new BuildingUse[_centreM.Count],
                EntryOffsets = offsets,
                EntryPointM = [.. _entryM],
                Prefab = _prefab.Contains(-1) ? [] : [.. _prefab],
            };
        }
    }
}
