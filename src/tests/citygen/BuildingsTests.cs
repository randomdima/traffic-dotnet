using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>What a town's buildings owe the ground they stand on</b> (GEN-54, GEN-55): they are off the paving,
/// they are clear of each other, their doors are on the pavement, and each service stands at the end of the
/// yard that was cut for it.
/// </summary>
/// <remarks>
/// <b>Asked of <see cref="Towns.Built"/></b>, the one town the suite lays with buildings on it — every
/// other brief asks for none, because a town with buildings carries car parks and a car park's arm ends at
/// bays nothing lays yet.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
[Collection(TownGeometryCollection.Name)]
public class BuildingsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static CityPlan Plan => Towns.Built;

    /// <summary>The town's own boundary, which every distance below is measured off (TER-7b).</summary>
    static KerbLines Boundary => _boundary ??= KerbLines.Of(Plan, Config);

    static KerbLines? _boundary;

    /// <summary>
    /// <b>Nothing a building is made of stands on the town's paving</b> (GEN-54). Its front wall is on the
    /// walk's own kerb by construction; what this asks is the rest of it — that a deep one laid against one
    /// street does not reach back into the pavement of the next.
    /// </summary>
    /// <remarks>
    /// <b>Measured to the driven ground's boundary and not to the walk's outer face</b>: the face is that
    /// boundary with its pockets closed, so a strip of grass narrower than two walks reads as tens of
    /// metres from the face and a metre from a carriageway.
    /// </remarks>
    [Fact]
    public void NoBuildingStandsOnThePaving()
    {
        var plan = Plan;
        Assert.NotEmpty(plan.Buildings.CentreM);

        var stepM = Config.Terrain.GroundStepM;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var axis = Heading.Unit(plan.Buildings.HeadingRad[building]);
            var side = Heading.RightOf(axis);
            var halfM = plan.Buildings.SizeM[building] * 0.5f;
            var centreM = plan.Buildings.CentreM[building];

            for (var alongM = -halfM.X; alongM <= halfM.X; alongM += stepM)
            {
                for (var acrossM = -halfM.Y; acrossM <= halfM.Y; acrossM += stepM)
                {
                    var atM = centreM + (axis * alongM) + (side * acrossM);
                    Assert.True(
                        OffTheBoundaryM(atM) >= Config.WalkOuterM,
                        $"building {building} at {centreM} reaches {OffTheBoundaryM(atM):F2} m off the " +
                        $"boundary at {atM}, inside the {Config.WalkOuterM:F2} m the walk is struck at");
                }
            }
        }
    }

    /// <summary>
    /// <b>A building's way in is on the pavement it fronts</b> (GEN-2a, GEN-5): between the kerb the walk
    /// begins at and the face it ends at, so a body standing at a door is standing on ground the walk is
    /// held on and not in the road or on the grass behind it.
    /// </summary>
    [Fact]
    public void EveryWayInStandsOnThePavement()
    {
        var plan = Plan;
        Assert.NotEmpty(plan.Buildings.EntryPointM);

        for (var way = 0; way < plan.Buildings.EntryPointM.Length; way++)
        {
            var offM = OffTheBoundaryM(plan.Buildings.EntryPointM[way]);
            Assert.InRange(offM, Config.WalkInnerM, Config.WalkOuterM);
        }
    }

    /// <summary>
    /// <b>No two buildings stand in each other</b> (GEN-3). Each claims its own padding as it is stood, so
    /// what the footprints owe each other is the whole of that padding — asked here as the weaker fact the
    /// claim grid can be held to exactly: that no two of the rectangles share any ground at all.
    /// </summary>
    [Fact]
    public void NoTwoBuildingsStandInEachOther()
    {
        var plan = Plan;
        for (var one = 0; one < plan.Buildings.Count; one++)
        {
            for (var other = one + 1; other < plan.Buildings.Count; other++)
            {
                Assert.False(
                    Overlap(plan, one, other),
                    $"buildings {one} at {plan.Buildings.CentreM[one]} and {other} at " +
                    $"{plan.Buildings.CentreM[other]} stand in each other");
            }
        }
    }

    /// <summary>
    /// <b>A service building stands past the far end of its own yard</b> (GEN-55): the rank of bays cut for
    /// it lies between its door and the street, which is what makes the car park outside a hospital the
    /// hospital's.
    /// </summary>
    [Fact]
    public void EveryServiceStandsAtTheEndOfItsOwnYard()
    {
        var plan = Plan;
        var services = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            if (plan.Buildings.Use[building] == BuildingUse.Ordinary) continue;

            services++;
            var centreM = plan.Buildings.CentreM[building];
            var doorM = plan.Buildings.EntryPointM[plan.Buildings.EntryOffsets[building]] - centreM;

            // A bay's far end stands half a lane inside its own boundary, and the building's middle stands
            // a walk, a kerb and its own half-depth beyond that — plus wherever along the rank the face was
            // walked to.
            var reachM = Config.LaneWidthM + Config.WalkOuterM
                         + (plan.Buildings.SizeM[building].Y * 0.5f) + Config.CityGen.BuildingPitchM;

            Assert.True(
                StandsAtARank(plan, centreM, doorM, reachM),
                $"the {plan.Buildings.Use[building]} at {centreM} has no rank of bays between its door and " +
                $"the street within {reachM:F1} m");
        }

        Assert.Equal(
            Config.HospitalsFor(Towns.BuildingsBuilt) + Config.PoliceStationsFor(Towns.BuildingsBuilt)
            + Config.DepotsFor(Towns.BuildingsBuilt),
            services);
    }

    /// <summary>
    /// <b>No building stands on the rounding round the end of a car park, or down its side</b> (GEN-55).
    /// Where one fronts a rank of bays, the whole of its frontage stands on the flat — the line those bays
    /// end on — because a building perched on the rounding fronts the mouth of the car park at an angle.
    /// </summary>
    /// <remarks>
    /// <b>The rank is read off the bays and not off a count</b>: how far it reaches along the street is
    /// where its own arms end, which is the figure that decided where a building could go.
    /// </remarks>
    [Fact]
    public void NoBuildingFrontsTheRoundingOfACarPark()
    {
        var plan = Plan;
        var ranks = Ranks(plan);
        Assert.NotEmpty(ranks);

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var centreM = plan.Buildings.CentreM[building];
            var halfM = plan.Buildings.SizeM[building] * 0.5f;

            foreach (var (tipM, outward, halfAcrossM) in ranks)
            {
                var alongM = Vector2.Dot(centreM - tipM, Heading.RightOf(outward));
                var outM = Vector2.Dot(centreM - tipM, outward);

                // Only a building standing off this rank's own end owes it anything: one further along the
                // street, behind the tip or across the road is fronting something else.
                if (outM <= 0f || outM > Config.WalkOuterM + Config.CityGen.BuildingPitchM + halfM.Y) continue;
                if (MathF.Abs(alongM) > halfAcrossM + halfM.X) continue;

                Assert.True(
                    MathF.Abs(alongM) + halfM.X <= halfAcrossM,
                    $"building {building} at {centreM} stands {MathF.Abs(alongM):F1} m off the middle of a " +
                    $"rank reaching {halfAcrossM:F1} m, with {halfM.X:F1} m of frontage either side of it");
            }
        }
    }

    /// <summary>
    /// Every rank of bays the town's car parks were cut as (GEN-53), one per car park per side of its road:
    /// the middle of the line its bays end on, the way they point, and how far that line reaches either way.
    /// </summary>
    static List<(Vector2 TipM, Vector2 Outward, float HalfAcrossM)> Ranks(CityPlan plan)
    {
        var ranks = new List<(Vector2, Vector2, float)>();
        for (var park = 0; park + 1 < plan.CarParks.BayOffsets.Length; park++)
        {
            foreach (var right in (ReadOnlySpan<bool>)[true, false])
            {
                var endsM = new List<Vector2>();
                var outward = Vector2.Zero;
                var widthM = 0f;
                for (var bay = plan.CarParks.BayOffsets[park]; bay < plan.CarParks.BayOffsets[park + 1]; bay++)
                {
                    if (plan.CarParks.Right[bay] != right) continue;

                    var chain = plan.Roads.SegmentsOf(plan.CarParks.Road[bay]);
                    if (chain.Length == 0) continue;

                    endsM.Add(Spline.SampleAt(chain, Spline.TotalLengthM(chain)).PositionM);
                    outward += Spline.SampleAt(chain, 0f).Direction;
                    widthM = plan.Roads.WidthM[plan.CarParks.Road[bay]];
                }

                if (endsM.Count == 0 || outward.LengthSquared() <= 0f) continue;

                var tipM = Vector2.Zero;
                foreach (var endM in endsM) tipM += endM;
                tipM /= endsM.Count;

                outward = Vector2.Normalize(outward);
                var along = Heading.RightOf(outward);
                var halfAcrossM = 0f;
                foreach (var endM in endsM)
                {
                    halfAcrossM = MathF.Max(halfAcrossM, MathF.Abs(Vector2.Dot(endM - tipM, along)));
                }

                ranks.Add((tipM, outward, halfAcrossM + (widthM * 0.5f)));
            }
        }

        return ranks;
    }

    /// <summary>Whether any of the town's bays ends between this door and the street, within reach of it.</summary>
    static bool StandsAtARank(CityPlan plan, Vector2 centreM, Vector2 doorM, float reachM)
    {
        foreach (var bay in plan.CarParks.Road)
        {
            var chain = plan.Roads.SegmentsOf(bay);
            if (chain.Length == 0) continue;

            var tipM = Spline.SampleAt(chain, Spline.TotalLengthM(chain)).PositionM - centreM;
            if (Vector2.Dot(tipM, doorM) > 0f && tipM.Length() <= reachM) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether two buildings' footprints share any ground: the separating axis test, over the four
    /// directions two rectangles' own sides give.
    /// </summary>
    static bool Overlap(CityPlan plan, int one, int other)
    {
        var apartM = plan.Buildings.CentreM[other] - plan.Buildings.CentreM[one];
        var oneAxis = Heading.Unit(plan.Buildings.HeadingRad[one]);
        var otherAxis = Heading.Unit(plan.Buildings.HeadingRad[other]);

        return !Separates(oneAxis) && !Separates(Heading.RightOf(oneAxis))
               && !Separates(otherAxis) && !Separates(Heading.RightOf(otherAxis));

        bool Separates(Vector2 onto) =>
            MathF.Abs(Vector2.Dot(apartM, onto)) >= Spread(plan, one, onto) + Spread(plan, other, onto);
    }

    /// <summary>How far a building's own rectangle reaches along a direction, which is its own extent projected onto it.</summary>
    static float Spread(CityPlan plan, int building, Vector2 onto)
    {
        var axis = Heading.Unit(plan.Buildings.HeadingRad[building]);
        var side = Heading.RightOf(axis);
        var halfM = plan.Buildings.SizeM[building] * 0.5f;
        return (MathF.Abs(Vector2.Dot(axis, onto)) * halfM.X) + (MathF.Abs(Vector2.Dot(side, onto)) * halfM.Y);
    }

    /// <summary>How far a place stands off the driven ground's own boundary.</summary>
    static float OffTheBoundaryM(Vector2 pointM) =>
        Boundary.NearestTo(pointM, out var at) ? (at.PositionM - pointM).Length() : float.MaxValue;
}
