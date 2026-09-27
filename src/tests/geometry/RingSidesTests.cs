using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>Which side of the rings a point stands on, as the fill reads them</b>: inside a ring walked with
/// positive area, outside a hole, and the same answer however the lattice is cut — a cell finer than a
/// piece, about as wide as a lane, and wider than the whole shape.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class RingSidesTests
{
    public static TheoryData<float> CellSizes => [0.5f, 4f, 40f];

    [Theory]
    [MemberData(nameof(CellSizes))]
    public void ADiscIsInsideItsCircleAndOutsideIt(float cellM)
    {
        const float radiusM = 7f;
        var centreM = new Vector2(103.3f, 57.9f);

        // One piece the whole way round, walked with its area positive: from the bottom of the circle,
        // heading along +x and bending towards +y.
        ArcSeg[][] rings = [[new ArcSeg(centreM - new Vector2(0f, radiusM), 0f, MathF.Tau * radiusM, 1f / radiusM)]];
        var sides = RingSides.Of(rings, cellM);

        Assert.Equal(1, sides.WindingAt(centreM));
        for (var step = 0; step < 360; step++)
        {
            var towardsM = Heading.Unit(step * MathF.Tau / 360f);
            var insideM = centreM + (towardsM * radiusM * 0.99f);
            var outsideM = centreM + (towardsM * radiusM * 1.01f);
            Assert.True(sides.WindingAt(insideM) == 1, $"{step}°: {insideM} winds {sides.WindingAt(insideM)}");
            Assert.True(sides.WindingAt(outsideM) == 0, $"{step}°: {outsideM} winds {sides.WindingAt(outsideM)}");
        }
    }

    [Theory]
    [MemberData(nameof(CellSizes))]
    public void AHoleWalkedTheOtherWayIsOutside(float cellM)
    {
        ArcSeg[][] rings = [Square(new Vector2(20f, 30f), 10f, outward: true), Square(new Vector2(23f, 33f), 4f, outward: false)];
        var sides = RingSides.Of(rings, cellM);

        Assert.True(sides.Encloses(new Vector2(21f, 31f)));
        Assert.True(sides.Encloses(new Vector2(29f, 39f)));
        Assert.False(sides.Encloses(new Vector2(25f, 35f)));
        Assert.False(sides.Encloses(new Vector2(19f, 35f)));
    }

    /// <summary>
    /// <b>A ring through the lattice's own corners is answered as any other</b>: the square's far corner
    /// stands on a corner of the lattice and two of its sides along the lattice's lines, and every other
    /// point asked about stands on a line too — the cases the half-open counting exists for.
    /// </summary>
    [Fact]
    public void ARingAlongTheLatticesOwnLinesIsAnsweredAsAnyOther()
    {
        // The lattice starts a cell and a half short of the square, so at two metres a cell its lines stand at
        // odd metres, and the square's seven-metre sides end on them.
        const float sideM = 7f;
        var sides = RingSides.Of([Square(Vector2.Zero, sideM, outward: true)], 2f);

        for (var x = -3f; x <= 10f; x += 0.5f)
        {
            for (var y = -3f; y <= 10f; y += 0.5f)
            {
                var onTheSquare = (x is 0f or sideM && y is >= 0f and <= sideM) || (y is 0f or sideM && x is >= 0f and <= sideM);
                if (onTheSquare) continue;

                // The winding and not the side: a crossing counted in the wrong direction is a winding of
                // minus one, which is outside as surely as nought is.
                var wanted = x is > 0f and < sideM && y is > 0f and < sideM ? 1 : 0;
                var winding = sides.WindingAt(new Vector2(x, y));
                Assert.True(winding == wanted, $"{x},{y} winds {winding} and should wind {wanted}");
            }
        }
    }

    /// <summary>
    /// <b>A hole no ring holds is left unfilled</b>, as the fill leaves it (<see cref="ShellFill"/>): the one
    /// case where a winding and a parity part, and the reason the answer is a winding.
    /// </summary>
    [Fact]
    public void AHoleNothingEnclosesIsOutside()
    {
        var sides = RingSides.Of([Square(new Vector2(20f, 30f), 10f, outward: false)], 4f);

        Assert.Equal(-1, sides.WindingAt(new Vector2(25f, 35f)));
        Assert.False(sides.Encloses(new Vector2(25f, 35f)));
    }

    /// <summary>
    /// <b>The town's own boundary answers what its fill covers</b> — asked of the rings the carriageway is
    /// filled from and weighed against those rings cut into chords and wound round by brute force, everywhere
    /// further from a chord than a chord strays from its arc.
    /// </summary>
    /// <remarks>
    /// Half the points stand a few centimetres either side of the boundary, because the cells a ring crosses
    /// are the whole of what the lattice can get wrong; the other half are scattered over the town.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CellSizes))]
    public void TheTownsBoundaryAnswersWhatItsFillCovers(float cellM)
    {
        const float sagM = 0.002f;
        const float clearM = 0.01f;
        var config = new SimConfig();
        var rings = Towns.Of(Towns.Fixture).Paving(config).Rings(config).Carriageway.Rings;
        var chords = ShellFill.Outline(rings, sagM);
        var sides = RingSides.Of(rings, cellM);

        var rng = new Random(1);
        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        var nearM = new List<Vector2>();
        foreach (var ring in chords)
        {
            for (var at = 0; at < ring.Length; at++)
            {
                leastM = Vector2.Min(leastM, ring[at]);
                mostM = Vector2.Max(mostM, ring[at]);
                if (rng.Next(8) != 0) continue;

                var alongM = ring[(at + 1) % ring.Length] - ring[at];
                if (alongM.LengthSquared() == 0f) continue;

                var acrossM = Heading.RightOf(Vector2.Normalize(alongM)) * 0.05f;
                var middleM = ring[at] + (alongM * 0.5f);
                nearM.Add(middleM + acrossM);
                nearM.Add(middleM - acrossM);
            }
        }

        var asked = 0;
        var inside = 0;
        var spanM = mostM - leastM;
        foreach (var pointM in nearM.Concat(Enumerable.Range(0, nearM.Count).Select(_ =>
                     leastM + (spanM * new Vector2(rng.NextSingle(), rng.NextSingle())))))
        {
            if (OffTheChordsM(chords, pointM) <= sagM + clearM) continue;

            var wanted = Winding(chords, pointM) > 0;
            Assert.True(wanted == sides.Encloses(pointM), $"{pointM.X},{pointM.Y}: the fill says inside is {wanted}");
            asked++;
            if (wanted) inside++;
        }

        Assert.True(inside > 100 && asked - inside > 100, $"{inside} of {asked} points were inside");
    }

    static ArcSeg[] Square(Vector2 cornerM, float sideM, bool outward)
    {
        var quarter = MathF.PI * 0.5f;
        if (outward)
        {
            return
            [
                new ArcSeg(cornerM, 0f, sideM, 0f),
                new ArcSeg(cornerM + new Vector2(sideM, 0f), quarter, sideM, 0f),
                new ArcSeg(cornerM + new Vector2(sideM, sideM), 2f * quarter, sideM, 0f),
                new ArcSeg(cornerM + new Vector2(0f, sideM), 3f * quarter, sideM, 0f),
            ];
        }

        return
        [
            new ArcSeg(cornerM, quarter, sideM, 0f),
            new ArcSeg(cornerM + new Vector2(0f, sideM), 0f, sideM, 0f),
            new ArcSeg(cornerM + new Vector2(sideM, sideM), 3f * quarter, sideM, 0f),
            new ArcSeg(cornerM + new Vector2(sideM, 0f), 2f * quarter, sideM, 0f),
        ];
    }

    /// <summary>The winding number of chord polygons round a point, by the upward and downward crossings of a ray.</summary>
    static int Winding(Vector2[][] polygons, Vector2 pointM)
    {
        var winding = 0;
        foreach (var polygon in polygons)
        {
            for (int at = 0, before = polygon.Length - 1; at < polygon.Length; before = at++)
            {
                var fromM = polygon[before];
                var toM = polygon[at];
                var left = (((double)toM.X - fromM.X) * ((double)pointM.Y - fromM.Y))
                    - (((double)pointM.X - fromM.X) * ((double)toM.Y - fromM.Y));
                if (fromM.Y <= pointM.Y)
                {
                    if (toM.Y > pointM.Y && left > 0d) winding++;
                }
                else if (toM.Y <= pointM.Y && left < 0d)
                {
                    winding--;
                }
            }
        }

        return winding;
    }

    static float OffTheChordsM(Vector2[][] polygons, Vector2 pointM)
    {
        var leastSq = float.MaxValue;
        foreach (var polygon in polygons)
        {
            for (int at = 0, before = polygon.Length - 1; at < polygon.Length; before = at++)
            {
                var fromM = polygon[before];
                var alongM = polygon[at] - fromM;
                var lengthSq = alongM.LengthSquared();
                var share = lengthSq > 0f ? Math.Clamp(Vector2.Dot(pointM - fromM, alongM) / lengthSq, 0f, 1f) : 0f;
                leastSq = MathF.Min(leastSq, Vector2.DistanceSquared(pointM, fromM + (alongM * share)));
            }
        }

        return MathF.Sqrt(leastSq);
    }
}
