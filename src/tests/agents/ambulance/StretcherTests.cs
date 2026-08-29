using TrafficSimulation.World.Containment;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// AMB-6 and AMB-8: <b>the seat that is not the wheel</b>. A casualty aboard must not be able to displace
/// a driver (CAR-2), must not be able to be aboard twice, and must not be able to fall out of the town
/// between a car and a door.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class StretcherTests
{
    const int Ambulance = 0;

    const int Crew = 0;

    const int Casualty = 1;

    static Containers OneAmbulanceAndOneHospital(int capacity = 1) =>
        new([capacity], cars: 2, personIsIn: new Contained[4]);

    /// <summary>The crew keeps the wheel and the casualty takes the stretcher: two occupants, one driver.</summary>
    [Fact]
    public void ACasualtyAboardIsNotTheDriver()
    {
        var containers = OneAmbulanceAndOneHospital();

        Assert.True(containers.TryBoard(Ambulance, Crew));
        Assert.True(containers.TryLoad(Ambulance, Casualty));

        Assert.Equal(Crew, containers.DriverOf(Ambulance));
        Assert.Equal(Casualty, containers.PassengerOf(Ambulance));
        Assert.False(containers.IsFree(Ambulance));
    }

    /// <summary>One casualty to an ambulance, asked the same atomic way the wheel is.</summary>
    [Fact]
    public void ASecondCasualtyIsRefused()
    {
        var containers = OneAmbulanceAndOneHospital();
        containers.TryLoad(Ambulance, Casualty);

        Assert.False(containers.TryLoad(Ambulance, 2));
        Assert.Equal(Casualty, containers.PassengerOf(Ambulance));
    }

    /// <summary>
    /// <b>The hand-over is one question</b> (OBJ-5). Taken off the car first and then refused at the door,
    /// the casualty would be inside nothing at all while still having no body in the world.
    /// </summary>
    [Fact]
    public void AFullHospitalRefusesAndLeavesTheCasualtyAboard()
    {
        var containers = OneAmbulanceAndOneHospital(capacity: 1);
        containers.TryLoad(Ambulance, Casualty);
        containers.TryAdmit(building: 0, person: 3);

        Assert.False(containers.TryTransfer(Ambulance, Casualty, building: 0));

        Assert.Equal(Casualty, containers.PassengerOf(Ambulance));
        Assert.Equal(ContainerKind.Car, containers.WhereIs(Casualty).Kind);
    }

    /// <summary>And admitted, they are in the building and off the car, in the same tick.</summary>
    [Fact]
    public void ADeliveredCasualtyIsInsideTheHospitalAndOffTheStretcher()
    {
        var containers = OneAmbulanceAndOneHospital(capacity: 1);
        containers.TryLoad(Ambulance, Casualty);

        Assert.True(containers.TryTransfer(Ambulance, Casualty, building: 0));

        Assert.Equal(Containers.NoDriver, containers.PassengerOf(Ambulance));
        Assert.Equal(new Contained(ContainerKind.Building, 0), containers.WhereIs(Casualty));
        Assert.Equal(1, containers.OccupantsOf(0));
    }

    /// <summary>A wreck empties the stretcher into the road (AMB-7), which is the other way off it.</summary>
    [Fact]
    public void UnloadingPutsTheCasualtyBackInTheTown()
    {
        var containers = OneAmbulanceAndOneHospital();
        containers.TryLoad(Ambulance, Casualty);

        containers.Unload(Ambulance, Casualty);

        Assert.Equal(Containers.NoDriver, containers.PassengerOf(Ambulance));
        Assert.False(containers.IsContained(Casualty));
    }
}
