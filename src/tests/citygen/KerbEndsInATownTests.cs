using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>Where a whole town's kerbs end</b> (<see cref="KerbEnds"/>), which is what the walk is cut at and what
/// the traffic is held behind (TER-6, WLK-10). The rule about which roads are crossed once is asked of the
/// fixture's arithmetic in <c>KerbEndsTests</c>; what a town adds is the one thing a written-down case
/// cannot — <b>every shape a generator lays, read off a boundary that was built rather than measured</b>.
/// </summary>
/// <remarks>
/// <b>Asked of both of the suite's towns</b>: the fixture is small enough to read at a glance, and the city
/// carries the bends, the forks and the long joined kerbs that only a town laid at length has.
/// A reading that wanders does it where a kerb runs far enough for it to.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
[Collection(TownGeometryCollection.Name)]
public class KerbEndsInATownTests
{
    /// <summary>
    /// <b>A road's two kerb ends at one box stand at the same place along it</b>, to within how lopsided a
    /// mouth is (<see cref="LopsidedM"/>): a street gives the boundary up on each of its kerbs where the
    /// mouth of the box widens past it, and how far into that mouth each of them runs is a fact about the
    /// mouth rather than about either kerb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it refuses is an end read off a place the boundary was misread at.</b> The line the ends are
    /// read from is a built one — offset, merged, rounded and joined again — so it wanders off the exact
    /// half-width it was struck at, and a reading that asks for that offset to within a weld answers a
    /// street's own unbroken kerb as that street until the drift crosses the tolerance and as nothing at all
    /// after it. The end then stands wherever that happened to be, tens of metres up the road; being the
    /// further of the two it is the one a walk is cut at and a driver held behind
    /// (<see cref="KerbEnds.Further"/>), so the paint goes where the reading wandered.
    /// </para>
    /// <para>
    /// <b>The pair is weighed and never the town's worst end.</b> How far out of a box a mouth reaches is
    /// the box's own business — two arms parting at a shallow angle keep both kerbs a long way out and are
    /// right to — so what is asked is that a street's two kerbs agree with each other, which is the reading
    /// that cannot come out right by accident. <b>Against the nearest of the ends filed under that pair</b>,
    /// so a road carrying three of them is weighed at its widest spread.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void TheTwoEndsOfOneRoadAtOneBoxStandAtTheSamePlaceAlongIt(string map)
    {
        var ends = Ends(map);
        var nearest = new Dictionary<(int Road, int Junction), float>();
        foreach (var end in ends.Nearer)
        {
            var key = (end.Road, end.Junction);
            nearest[key] = nearest.TryGetValue(key, out var standing)
                ? MathF.Min(standing, end.OutM)
                : end.OutM;
        }

        var pairs = 0;
        var worstM = 0f;
        var worst = default(KerbEnds.Mark);
        foreach (var end in ends.Further)
        {
            if (!nearest.TryGetValue((end.Road, end.Junction), out var otherM)) continue;

            pairs++;
            if (end.OutM - otherM <= worstM) continue;

            worstM = end.OutM - otherM;
            worst = end;
        }

        // The staging and not the claim: a town where no street ended twice at one box would ask nothing.
        Assert.True(pairs > 0, $"no street of {map} ends on both of its kerbs at one box");
        Assert.True(
            worstM <= LopsidedM,
            $"road {worst.Road} of {map} ends {worstM:F2} m further out on one kerb than on the other at " +
            $"box {worst.Junction}, at {worst.AtM.X:F1},{worst.AtM.Y:F1}");
    }

    /// <summary>
    /// <b>A road ends twice at a box and not four times</b>: it has a kerb either side of it, so what the
    /// boundary can hand over at one box is each of those two once. <b>A kerb that gives the outline up and
    /// takes it straight back has not ended</b> — what the outline went round is something standing inside
    /// the road, a notch along its own kerb filled by the rounding (TER-3c.10) being the shape that does it — and
    /// counting those two places as ends stands a pair of them out where the bend is, which is as far from
    /// the box as the bend is and is where the paint then goes (<see cref="KerbEnds.Further"/>).
    /// </summary>
    /// <remarks>
    /// <b>Counted and not measured</b>, because the two stand together: a bend the outline steps round is
    /// entered and left within a stride, so both of the ends it makes carry nearly the same reading and a
    /// test weighing one of them against the other sees a street whose two kerbs agree. What gives them away
    /// is that there are two of them too many.
    /// </remarks>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void ARoadCarriesOneEndOnEachOfItsTwoKerbsAtOneBox(string map)
    {
        var ends = Ends(map);
        var standing = new Dictionary<(int Road, int Junction), int>();
        foreach (var end in ends.Further) Count(end);
        foreach (var end in ends.Nearer) Count(end);

        var worst = standing.MaxBy(one => one.Value);
        Assert.True(
            worst.Value <= 2,
            $"road {worst.Key.Road} of {map} carries {worst.Value} kerb ends at box {worst.Key.Junction}");

        void Count(KerbEnds.Mark end)
        {
            var key = (end.Road, end.Junction);
            standing[key] = standing.TryGetValue(key, out var was) ? was + 1 : 1;
        }
    }

    /// <summary>The ends of one of the suite's towns, read at the shipped figures.</summary>
    static KerbEnds Ends(string map)
    {
        var config = SimConfig.Shipped();
        return KerbEnds.Of(Towns.Of(map).Paving(config), config);
    }

    /// <summary>
    /// How far apart a street's two kerb ends at one box may stand along it: how lopsided the mouth of a box
    /// can be, which is one arm's band reaching further out past the street than the arm opposite it does.
    /// </summary>
    /// <remarks>
    /// <b>Measured on the two towns rather than derived</b>: the depth of a mouth is settled by every band
    /// that meets at the box and the radius its corners are rounded on, and a figure worked out here would
    /// be that arithmetic written a second time. The widest they lay is 7.3 m — a mouth is as lopsided as
    /// the arms that meet at it, and two of them parting at a shallow angle hold one kerb of a street out at
    /// the gore while its other gives the outline up at the mouth. <b>A defect clears it by a wide
    /// mark</b>: a reading that wandered off a street's own kerb stood the two of them 46 m apart on the
    /// fixture and 102 m apart on the city.
    /// </remarks>
    const float LopsidedM = 10f;
}
