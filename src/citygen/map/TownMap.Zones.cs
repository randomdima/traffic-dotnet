using System.Numerics;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.CityGen.Map;

internal sealed partial record TownMap
{
    /// <summary>
    /// <b>What a map is zoned for</b> (GEN-58): a tree of zones, the first the whole map, every other one inside its
    /// parent and written after it — each its kind, the settings it says of itself, and its rings, flat with offsets
    /// beside them: its outline first, then any hole cut out of it, none closed on its first point.
    /// </summary>
    /// <remarks>
    /// <b>A zone is a part of the town built alike, and has no name</b>: it says only what it means to, and a setting it
    /// leaves unsaid is its kind's (<see cref="ZoneTree"/>).
    /// </remarks>
    internal sealed class ZoneArrays
    {
        /// <summary>The zone that is the whole map.</summary>
        public const int Root = 0;

        /// <summary>The zone each is inside, −1 for the whole map's.</summary>
        public required int[] Parent { get; init; }

        public required ZoneKind[] Kind { get; init; }

        /// <summary>Count + 1 entries, over <see cref="ParamKey"/> and <see cref="ParamValue"/>.</summary>
        public required int[] ParamOffsets { get; init; }

        public required ZoneParam[] ParamKey { get; init; }

        /// <summary>Each held to its own step (<see cref="ZoneParams.Held"/>), so a map in memory is the map it reads back as.</summary>
        public required float[] ParamValue { get; init; }

        /// <summary>Count + 1 entries, over the rings: zone i's are <c>RingOffsets[i]..RingOffsets[i + 1]</c>.</summary>
        public required int[] RingOffsets { get; init; }

        /// <summary>One entry a ring + 1, over <see cref="PointM"/>.</summary>
        public required int[] PointOffsets { get; init; }

        public required Vector2[] PointM { get; init; }

        public int Count => Kind.Length;

        public ReadOnlySpan<Vector2> RingOf(int ring) => PointM.AsSpan(PointOffsets[ring], PointOffsets[ring + 1] - PointOffsets[ring]);

        /// <summary>A zone's outline.</summary>
        public ReadOnlySpan<Vector2> OutlineOf(int zone) => RingOf(RingOffsets[zone]);

        /// <summary>What a zone says of a setting itself, or null where it leaves it to its parent.</summary>
        public float? Own(int zone, ZoneParam param)
        {
            for (var at = ParamOffsets[zone]; at < ParamOffsets[zone + 1]; at++)
            {
                if (ParamKey[at] == param) return ParamValue[at];
            }

            return null;
        }

        /// <summary>How many buildings the town plans (<see cref="ZoneParam.Buildings"/>), or nought where it says none.</summary>
        public int Planned => Count > 0 ? (int)(Own(Root, ZoneParam.Buildings) ?? 0f) : 0;

        /// <summary>How many zones a zone stands inside: its parent, its parent's, and so on to the whole map.</summary>
        public int Depth(int zone)
        {
            var depth = 0;
            for (var at = Parent[zone]; at >= 0; at = Parent[at]) depth++;
            return depth;
        }

        /// <summary>A map zoned as nothing: no zone, not even the whole map's, which a map read is refused for.</summary>
        public static ZoneArrays None => new Builder().Arrays();

        /// <summary>These zones with one setting of one zone said, or said again.</summary>
        public ZoneArrays With(int zone, ZoneParam param, float value)
        {
            var zones = new Builder();
            for (var at = 0; at < Count; at++)
            {
                var settings = new List<(ZoneParam, float)>();
                for (var own = ParamOffsets[at]; own < ParamOffsets[at + 1]; own++)
                {
                    if (at != zone || ParamKey[own] != param) settings.Add((ParamKey[own], ParamValue[own]));
                }

                if (at == zone) settings.Add((param, value));

                var rings = new Vector2[RingOffsets[at + 1] - RingOffsets[at]][];
                for (var ring = 0; ring < rings.Length; ring++) rings[ring] = RingOf(RingOffsets[at] + ring).ToArray();

                zones.Add(Parent[at], Kind[at], [.. settings], rings);
            }

            return zones.Arrays();
        }

        /// <summary>A map zoned as the whole of it and nothing else, of a kind and with settings given.</summary>
        public static ZoneArrays Whole(Vector2 sizeM, ZoneKind root, ReadOnlySpan<(ZoneParam, float)> settings)
        {
            var zones = new Builder();
            zones.Add(-1, root, settings, [WholeOutline(sizeM)]);
            return zones.Arrays();
        }

        /// <summary>
        /// Refuses zones that cannot describe a map: the first is the whole map and the one zone with no parent, of a
        /// kind a whole map is, and every other is of a kind a part is, after its parent, with an outline.
        /// </summary>
        public void Check(string what)
        {
            if (Count == 0) throw new InvalidDataException($"{what}: no zone, where the first is the whole map.");

            for (var zone = 0; zone < Count; zone++)
            {
                var whole = Kind[zone] is ZoneKind.Town or ZoneKind.Wheel;
                if (zone == Root != whole) throw new InvalidDataException($"{what}: zone {zone} is a {Kind[zone]}, and only the first is the whole map.");
                if (zone == Root ? Parent[zone] != -1 : (uint)Parent[zone] >= (uint)zone)
                {
                    throw new InvalidDataException($"{what}: zone {zone} is inside zone {Parent[zone]}, which is not written before it.");
                }

                if (!Enum.IsDefined(Kind[zone])) throw new InvalidDataException($"{what}: zone {zone} is of kind {(int)Kind[zone]}, which this build does not know.");
                if (RingOffsets[zone + 1] == RingOffsets[zone] || OutlineOf(zone).Length < 3) throw new InvalidDataException($"{what}: zone {zone} has no outline.");

                for (var at = ParamOffsets[zone]; at < ParamOffsets[zone + 1]; at++)
                {
                    if (!ZoneParams.Known(ParamKey[at])) throw new InvalidDataException($"{what}: zone {zone} sets {(int)ParamKey[at]}, which this build does not know.");
                    if (!float.IsFinite(ParamValue[at])) throw new InvalidDataException($"{what}: zone {zone} sets {ParamKey[at]} to {ParamValue[at]}.");
                }
            }
        }

        /// <summary>Zones added one at a time, parents first, each setting held to its step as it is added.</summary>
        internal sealed class Builder
        {
            readonly List<int> _parent = [];
            readonly List<ZoneKind> _kind = [];
            readonly List<int> _paramOffsets = [0];
            readonly List<ZoneParam> _paramKey = [];
            readonly List<float> _paramValue = [];
            readonly List<int> _ringOffsets = [0];
            readonly List<int> _pointOffsets = [0];
            readonly List<Vector2> _pointM = [];

            public int Count => _kind.Count;

            /// <returns>The zone's index, which its own zones name as their parent.</returns>
            public int Add(int parent, ZoneKind kind, ReadOnlySpan<(ZoneParam Param, float Value)> settings, ReadOnlySpan<Vector2[]> rings)
            {
                _parent.Add(parent);
                _kind.Add(kind);
                foreach (var (param, value) in settings)
                {
                    _paramKey.Add(param);
                    _paramValue.Add(ZoneParams.Held(param, value));
                }

                _paramOffsets.Add(_paramKey.Count);
                foreach (var ring in rings)
                {
                    foreach (var pointM in ring) _pointM.Add(new Vector2(MathF.Round(pointM.X), MathF.Round(pointM.Y)));
                    _pointOffsets.Add(_pointM.Count);
                }

                _ringOffsets.Add(_pointOffsets.Count - 1);
                return _kind.Count - 1;
            }

            /// <summary>
            /// One zone of another map's, moved by an offset and cut to a frame, under a parent of this one's — or −1 and
            /// nothing added where none of its outline stands inside the frame. A hole cut away to nothing is dropped.
            /// </summary>
            public int Add(ZoneArrays from, int zone, int parent, Vector2 offsetM, Vector2 sizeM)
            {
                var settings = new (ZoneParam, float)[from.ParamOffsets[zone + 1] - from.ParamOffsets[zone]];
                for (var at = 0; at < settings.Length; at++) settings[at] = (from.ParamKey[from.ParamOffsets[zone] + at], from.ParamValue[from.ParamOffsets[zone] + at]);

                var rings = new List<Vector2[]>();
                for (var ring = from.RingOffsets[zone]; ring < from.RingOffsets[zone + 1]; ring++)
                {
                    var points = from.RingOf(ring).ToArray();
                    for (var at = 0; at < points.Length; at++) points[at] -= offsetM;

                    var cut = Cut(points, sizeM);
                    if (cut.Length >= 3) rings.Add(cut);
                    else if (ring == from.RingOffsets[zone]) return -1;
                }

                return Add(parent, from.Kind[zone], settings, [.. rings]);
            }

            /// <summary>A ring cut to a frame from the origin to a size, one side of it at a time (Sutherland–Hodgman).</summary>
            static Vector2[] Cut(Vector2[] ring, Vector2 sizeM)
            {
                ring = Side(ring, Vector2.UnitX, 0f);
                ring = Side(ring, -Vector2.UnitX, -sizeM.X);
                ring = Side(ring, Vector2.UnitY, 0f);
                return Side(ring, -Vector2.UnitY, -sizeM.Y);

                // What of a ring stands where the place along an axis is at least a least.
                static Vector2[] Side(Vector2[] ring, Vector2 axis, float least)
                {
                    var kept = new List<Vector2>(ring.Length + 4);
                    for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++)
                    {
                        var (from, to) = (ring[before], ring[at]);
                        var (fromIn, toIn) = (Vector2.Dot(from, axis) - least, Vector2.Dot(to, axis) - least);
                        if ((fromIn >= 0f) != (toIn >= 0f)) kept.Add(Vector2.Lerp(from, to, fromIn / (fromIn - toIn)));
                        if (toIn >= 0f) kept.Add(to);
                    }

                    return [.. kept];
                }
            }

            public ZoneArrays Arrays() => new()
            {
                Parent = [.. _parent], Kind = [.. _kind], ParamOffsets = [.. _paramOffsets], ParamKey = [.. _paramKey],
                ParamValue = [.. _paramValue], RingOffsets = [.. _ringOffsets], PointOffsets = [.. _pointOffsets], PointM = [.. _pointM],
            };
        }
    }
}
