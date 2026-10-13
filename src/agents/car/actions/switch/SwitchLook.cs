using TrafficSimulation.Agents.Car.Control;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>What one decision of a car meaning to move across comes to</b> (<see cref="Switching.Consider"/>), before any of it is
/// taken (<see cref="Switching.Take"/>): how it came out, and the figures it leaves the car with.
/// </summary>
/// <param name="Inward">Which way it means to move across: toward the line its two ways meet on, or toward the kerb.</param>
/// <param name="Turned">Whether it turned to look the other way, which lets a step asked toward the first go.</param>
/// <param name="AsideM">How far across the lane beside stands from where it is — what it indicates toward (CAR-14.7).</param>
/// <param name="LooksInS">How long until it looks for the room again.</param>
/// <param name="FromRestM">How far ahead of where it stands a step from rest begins and lands, and the pass's spare.</param>
/// <param name="RoomToM">Where along the line the ground its step holds past it ends, at the rear axle.</param>
/// <param name="Step">The step asked for, where it asks one.</param>
internal readonly record struct SwitchLook(
    SwitchOutcome Outcome, bool Inward, bool Turned, float AsideM, float LooksInS, float FromRestM, float RoomToM, Overtake Step);

/// <summary>How a decision of a car meaning to move across came out (<see cref="SwitchLook"/>).</summary>
internal enum SwitchOutcome : uint
{
    /// <summary>On its step already: nothing to decide.</summary>
    Begun,

    /// <summary>No longer meaning to move across, or its line stopping for a bay: handed back to following.</summary>
    LetsGo,

    /// <summary>Not looking again yet, or a step already asked.</summary>
    Waits,

    /// <summary>Looked, and found no step it can drive, no room to sweep one in, or its ground held.</summary>
    Looked,

    /// <summary>Looked, and found a step with its ground free: the step asked for.</summary>
    Asks,
}
