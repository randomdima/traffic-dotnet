using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.CityGen.Map;

internal sealed partial record TownMap
{
    /// <summary>
    /// <b>This map cut down to a smaller frame</b>, its north-west corner at (<paramref name="leftM"/>,
    /// <paramref name="topM"/>) of this one's and every place moved by as much: what stands outside the new frame is
    /// left out, and a road or the coast running on past it keeps its line up to the first point past the
    /// frame, which is all <see cref="Survey.Of"/> reads of it — the engine cuts a road where it leaves the map and
    /// closes the sea along the map's edge. The frame keeps its projection and its margin.
    /// </summary>
    /// <remarks>
    /// <b>Whole metres</b>, so every place moves by whole steps and stays the one it was. A coast way that never
    /// reaches the frame is left out even where it joined two that do: the sea is closed along the frame's edge
    /// between where they leave it and enter it again. A water is kept whole where its outline's box meets the frame; a
    /// zone is cut to the frame and left out where nothing of it is inside, a zone whose parent is left out being its
    /// nearest kept ancestor's, and the whole map's zone is the new frame. What is set down is kept where its middle
    /// stands inside the frame.
    /// </remarks>
    public TownMap Cropped(int leftM, int topM, int widthM, int heightM)
    {
        var frame = new OsmFrame
        {
            Lat0Deg = Frame.Lat0Deg, Lon0Deg = Frame.Lon0Deg, WestM = Frame.WestM + leftM,
            SouthM = Frame.SouthM + (Frame.HeightM - topM - heightM), WidthM = widthM, HeightM = heightM, MarginM = Frame.MarginM,
        };
        if (!(widthM > 2 * frame.MarginM) || !(heightM > 2 * frame.MarginM)) throw new ArgumentException($"a frame of {widthM} by {heightM} m holds nothing inside its {frame.MarginM} m margin.");

        var placedM = new Vector2D[PointM.Length];
        for (var point = 0; point < placedM.Length; point++) placedM[point] = new Vector2D(PointM[point].X - leftM, PointM[point].Y - topM);

        var offsetM = new Vector2(leftM, topM);
        var sizeM = new Vector2(widthM, heightM);
        var whole = new Box(0, 0, widthM, heightM);
        var renumbered = new int[PointM.Length];
        Array.Fill(renumbered, -1);
        var kept = new List<Vector2D>();

        var roads = new List<TracedRoad>();
        foreach (var road in Roads)
        {
            if (Trimmed(road.Points, whole) is { } points) roads.Add(road with { Points = points });
        }

        var coast = new List<int[]>();
        foreach (var line in Coast)
        {
            if (Trimmed(line, whole) is { } points) coast.Add(points);
        }

        return this with
        {
            Frame = frame,
            PointM = [.. kept],
            Roads = [.. roads],
            Coast = [.. coast],
            Waters = CroppedWaters(),
            Courses = new CourseArrays
            {
                Kind = Courses.Kind, Across = Courses.Across, NearM = Courses.NearM, FarM = Courses.FarM, PointOffsets = Courses.PointOffsets,
                PointM = [.. Courses.PointM.Select(atM => atM - offsetM)],
            },
            Buildings = CroppedStood(),
            Props = CroppedProps(),
            Lots = CroppedLots(),
            Zones = CroppedZones(),
        };

        bool Inside(Vector2 atM) => atM.X >= 0f && atM.Y >= 0f && atM.X <= widthM && atM.Y <= heightM;

        bool Meets(ReadOnlySpan<Vector2> outline)
        {
            var (leastM, mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
            foreach (var atM in outline) (leastM, mostM) = (Vector2.Min(leastM, atM - offsetM), Vector2.Max(mostM, atM - offsetM));
            return !(mostM.X < 0 || mostM.Y < 0 || leastM.X > widthM || leastM.Y > heightM);
        }

        WaterArrays CroppedWaters()
        {
            var (kinds, pointOffsets, pointM) = (new List<WaterBody>(), new List<int> { 0 }, new List<Vector2>());
            for (var water = 0; water < Waters.Count; water++)
            {
                if (!Meets(Waters.OutlineOf(water))) continue;

                foreach (var atM in Waters.OutlineOf(water)) pointM.Add(atM - offsetM);
                pointOffsets.Add(pointM.Count);
                kinds.Add(Waters.Kind[water]);
            }

            return new WaterArrays { Kind = [.. kinds], PointOffsets = [.. pointOffsets], PointM = [.. pointM] };
        }

        StoodArrays CroppedStood()
        {
            var keep = Enumerable.Range(0, Buildings.Count).Where(at => Inside(Buildings.CentreM[at] - offsetM)).ToArray();
            return new StoodArrays
            {
                Look = [.. keep.Select(at => Buildings.Look[at])], CentreM = [.. keep.Select(at => Buildings.CentreM[at] - offsetM)],
                SizeM = [.. keep.Select(at => Buildings.SizeM[at])], HeadingRad = [.. keep.Select(at => Buildings.HeadingRad[at])],
            };
        }

        PropArrays CroppedProps()
        {
            var keep = Enumerable.Range(0, Props.Count).Where(at => Inside(Props.CentreM[at] - offsetM)).ToArray();
            return new PropArrays
            {
                Kind = [.. keep.Select(at => Props.Kind[at])], CentreM = [.. keep.Select(at => Props.CentreM[at] - offsetM)],
                BearingRad = [.. keep.Select(at => Props.BearingRad[at])],
            };
        }

        LotArrays CroppedLots()
        {
            var keep = Enumerable.Range(0, Lots.Count).Where(at => Inside(Lots.CentreM[at] - offsetM)).ToArray();
            return new LotArrays
            {
                CentreM = [.. keep.Select(at => Lots.CentreM[at] - offsetM)], SizeM = [.. keep.Select(at => Lots.SizeM[at])],
                HeadingRad = [.. keep.Select(at => Lots.HeadingRad[at])],
            };
        }

        ZoneArrays CroppedZones()
        {
            var zones = new ZoneArrays.Builder();
            var keptAs = new int[Zones.Count];
            const int root = ZoneArrays.Root;
            var settings = new (ZoneParam, float)[Zones.ParamOffsets[root + 1] - Zones.ParamOffsets[root]];
            for (var at = 0; at < settings.Length; at++) settings[at] = (Zones.ParamKey[Zones.ParamOffsets[root] + at], Zones.ParamValue[Zones.ParamOffsets[root] + at]);

            keptAs[root] = zones.Add(-1, Zones.Kind[root], settings, [WholeOutline(sizeM)]);
            for (var zone = root + 1; zone < Zones.Count; zone++)
            {
                var parent = keptAs[Zones.Parent[zone]];
                keptAs[zone] = zones.Add(Zones, zone, parent, offsetM, sizeM) is >= 0 and var added ? added : parent;
            }

            return zones.Arrays();
        }

        // From the first straight touching the box to the last, so the points past it at either end are one each.
        int[]? Trimmed(int[] points, Box box)
        {
            var (first, past) = (-1, -1);
            for (var at = 1; at < points.Length; at++)
            {
                if (!box.Touches(placedM[points[at - 1]], placedM[points[at]])) continue;

                if (first < 0) first = at - 1;
                past = at + 1;
            }

            if (first < 0) return null;

            var trimmed = points[first..past];
            for (var at = 0; at < trimmed.Length; at++)
            {
                ref var point = ref renumbered[trimmed[at]];
                if (point < 0)
                {
                    point = kept.Count;
                    kept.Add(placedM[trimmed[at]]);
                }

                trimmed[at] = point;
            }

            return trimmed;
        }
    }

    /// <summary>A rectangle in a map's own metres, and whether a straight touches it (Liang–Barsky).</summary>
    readonly record struct Box(double Left, double Top, double Right, double Bottom)
    {
        public bool Touches(Vector2D fromM, Vector2D toM)
        {
            var (enters, leaves) = (0.0, 1.0);
            var (acrossM, downM) = (toM.X - fromM.X, toM.Y - fromM.Y);
            ReadOnlySpan<(double Toward, double RoomM)> sides =
            [
                (-acrossM, fromM.X - Left), (acrossM, Right - fromM.X), (-downM, fromM.Y - Top), (downM, Bottom - fromM.Y),
            ];
            foreach (var (toward, roomM) in sides)
            {
                if (toward == 0.0)
                {
                    if (roomM < 0.0) return false;

                    continue;
                }

                var share = roomM / toward;
                if (toward < 0.0) enters = Math.Max(enters, share);
                else leaves = Math.Min(leaves, share);
            }

            return enters <= leaves;
        }
    }
}
