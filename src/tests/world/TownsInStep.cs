using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests.World;

/// <summary>Two towns run side by side, held to each other.</summary>
internal static class TownsInStep
{
    /// <summary>
    /// Every car and walker of one town against the other's, to the bit: where each stands, moves and faces, and its hold —
    /// and where its world read each car along its line.
    /// </summary>
    public static void Compare(TownWorld one, TownWorld other, int tick, List<string> unlike)
    {
        var (cars, otherCars) = (one.Cars, other.Cars);
        for (var car = 0; car < cars.Count; car++)
        {
            var pose = (cars.PositionM[car], cars.VelocityMps[car], cars.HeadingRad[car], cars.TiltRad[car], one.PlanHoldOf(car));
            var otherPose = (
                otherCars.PositionM[car], otherCars.VelocityMps[car], otherCars.HeadingRad[car], otherCars.TiltRad[car],
                other.PlanHoldOf(car));
            if (!pose.Equals(otherPose)) unlike.Add($"tick {tick} car {car}: {pose} against {otherPose}");

            var (read, otherRead) = (one.World.View.Cars[car], other.World.View.Cars[car]);
            var reading = (read.ProgressM, read.OffLineM, read.FollowSerial);
            var otherReading = (otherRead.ProgressM, otherRead.OffLineM, otherRead.FollowSerial);
            if (!reading.Equals(otherReading)) unlike.Add($"tick {tick} car {car} read at {reading} against {otherReading}");
        }

        var (people, otherPeople) = (one.People, other.People);
        for (var person = 0; person < people.Count; person++)
        {
            var body = (people.PositionM[person], people.VelocityMps[person], people.HeadingRad[person], one.WalkHold(person));
            var otherBody = (otherPeople.PositionM[person], otherPeople.VelocityMps[person], otherPeople.HeadingRad[person], other.WalkHold(person));
            if (!body.Equals(otherBody)) unlike.Add($"tick {tick} walker {person}: {body} against {otherBody}");
        }

        var wagons = one.World.View.Wagons;
        var otherWagons = other.World.View.Wagons;
        for (var wagon = 0; wagon < wagons.Length; wagon++)
        {
            var (row, otherRow) = (wagons[wagon], otherWagons[wagon]);
            var stands = (row.PositionM, row.HeadingRad, row.VelocityMps, row.YawRateRadPerS, row.Flags);
            var otherStands = (otherRow.PositionM, otherRow.HeadingRad, otherRow.VelocityMps, otherRow.YawRateRadPerS, otherRow.Flags);
            if (!stands.Equals(otherStands)) unlike.Add($"tick {tick} rail vehicle {wagon}: {stands} against {otherStands}");
        }
    }
}
