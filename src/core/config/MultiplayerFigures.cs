namespace TrafficSimulation.Core.Config;

/// <summary>
/// What a town served to players is measured by (MP-1…): how often each is sent the ground round its car, how finely,
/// how far it may see, and how long a silent one keeps its seat. <b>Nothing a town does reads any of it</b>: a served
/// town ticks exactly as a watched one.
/// </summary>
internal sealed class MultiplayerFigures
{
    /// <summary>
    /// The most players one town seats at once, and so the most named hands it holds (CTL-5d). A bound and not a preference:
    /// the seats are one array laid with the town. The second seat a scripted driver takes is one of them.
    /// </summary>
    public int MostSeats { get; init; } = 16;

    /// <summary>How many ticks pass between two snapshots a player is sent — three at 60 Hz is twenty a second.</summary>
    public int TicksPerSnapshot { get; init; } = 3;

    /// <summary>
    /// The most movers one snapshot carries. A bound and not a preference: each player keeps the snapshots it may be
    /// sent a difference from, each this long. A square of a dense city holds a few hundred.
    /// </summary>
    public int MostMoversSent { get; init; } = 1024;

    /// <summary>
    /// How many snapshots a player and its server keep to name as a baseline, which is how stale one may be. More than
    /// <see cref="MostSnapshotsUnacknowledged"/>, so whatever a player acknowledges is still held on both sides.
    /// </summary>
    public int BaselinesKept { get; init; } = 16;

    /// <summary>
    /// <b>How many snapshots a player may be sent past the newest it has acknowledged</b> before it is sent nothing more:
    /// a page in a tab nobody shows, or one on a line slower than its feed, is not buried under snapshots it cannot read,
    /// and comes back to a difference rather than a whole area. A few round trips' worth of the feed.
    /// </summary>
    public int MostSnapshotsUnacknowledged { get; init; } = 8;

    /// <summary>The step a position is sent in.</summary>
    public float PositionQuantumM { get; init; } = 0.02f;

    /// <summary>The step a heading is sent in.</summary>
    public float AngleQuantumDeg { get; init; } = 0.1f;

    /// <summary>The step a road wheel's angle and a body's lean on its springs are sent in: a sliver of a body, not all of it.</summary>
    public float SmallAngleQuantumDeg { get; init; } = 0.5f;

    /// <summary>
    /// <b>The longest a player goes without sending its keys</b> while they and its newest snapshot stay as they were:
    /// well inside <see cref="HoldAfterSilenceS"/>, so a player holding still is never taken for one gone silent.
    /// </summary>
    public float InputKeepAliveS { get; init; } = 0.25f;

    /// <summary>
    /// <b>The widest a player may look</b>: the longer side of its view at the farthest it may zoom out. What it is
    /// sent is the ground this wide round its car and a margin past it (<see cref="SimConfig.PlayerAreaHalfSpanM"/>).
    /// </summary>
    public float WidestViewM { get; init; } = 160f;

    /// <summary>
    /// <b>How far from its car a player may drag its view</b> (MP-9). The area it is sent reaches this much further
    /// (<see cref="SimConfig.PlayerAreaHalfSpanM"/>), so a view dragged to the end of it is still full.
    /// </summary>
    public float MostPanM { get; init; } = 50f;

    /// <summary>How long a view let go of takes to come most of the way back onto its car.</summary>
    public float PanReturnS { get; init; } = 1f;

    /// <summary>
    /// <b>How long a player's camera takes to settle on where its car wants it</b> (MP-9): the time a critically damped
    /// spring closes most of a standing gap in. Long enough that a snapshot's step and a lead that swings round a corner
    /// are smoothed out of the picture, short enough that the car never leaves the middle third of it.
    /// </summary>
    public float CameraSettleS { get; init; } = 0.35f;

    /// <summary>The side of a player's map (MP-12), in interface pixels.</summary>
    public float MapSidePx { get; init; } = 200f;

    /// <summary>How much of the town a player's map shows across its side, round the player's car.</summary>
    public float MapSpanM { get; init; } = 600f;

    /// <summary>How far a player's map stands in from the screen's corner, in interface pixels.</summary>
    public float MapMarginPx { get; init; } = 16f;

    /// <summary>The narrowest a road is drawn on a player's map, in interface pixels, however narrow it is.</summary>
    public float MapRoadLeastPx { get; init; } = 2f;

    /// <summary>The most a road bends along one straight stroke of a player's map.</summary>
    public float MapChordTurnDeg { get; init; } = 10f;

    /// <summary>The length of a player's arrow on its map, nose to tail, in interface pixels.</summary>
    public float MapArrowPx { get; init; } = 14f;

    /// <summary>The frame round a player's map, and the dark edge round each arrow, in interface pixels.</summary>
    public float MapEdgePx { get; init; } = 2f;

    /// <summary>The height of another player's name beside its arrow on the map (MP-15), in interface pixels.</summary>
    public float MapNamePx { get; init; } = 10f;

    /// <summary>The height of another player's name over what it is drawn as in the town (MP-15), in interface pixels.</summary>
    public float NamePx { get; init; } = 13f;

    /// <summary>
    /// <b>How far over the middle of what a player is drawn as its name stands</b> (MP-15): this much of the town, which
    /// clears a car's half-length whichever way it faces, and <see cref="NameLiftPx"/> more at any zoom.
    /// </summary>
    public float NameLiftM { get; init; } = 2.6f;

    public float NameLiftPx { get; init; } = 6f;

    /// <summary>
    /// <b>The least saturation and brightness a player's colour is given</b> (MP-15), each a share of one: whatever it
    /// asked for is raised to these, keeping its hue, so no player is a grey or white a map's roads are drawn in.
    /// </summary>
    public float SeatLeastSaturation { get; init; } = 0.55f;

    public float SeatLeastValue { get; init; } = 0.7f;

    /// <summary>
    /// <b>How far behind the server a player draws the town</b> — the span of snapshots it holds to interpolate across.
    /// Three snapshots' worth survives two lost on a twenty-a-second feed.
    /// </summary>
    public float InterpolationDelayS { get; init; } = 0.15f;

    /// <summary>
    /// The longest a player draws past its newest snapshot, carrying each body on at its last speed, before the picture
    /// holds still and waits.
    /// </summary>
    public float LongestExtrapolationS { get; init; } = 0.25f;

    /// <summary>
    /// <b>How much faster or slower than real time a player's picture may run</b> while it closes on where its estimate of
    /// the server says it should be (MP-7): fast enough to follow a server that slows, slow enough that nobody sees it.
    /// </summary>
    public float PlayoutSlewShare { get; init; } = 0.05f;

    /// <summary>How long a player may send nothing before whatever it drives is held braked where it is.</summary>
    public float HoldAfterSilenceS { get; init; } = 1f;

    /// <summary>And how long before its seat is given up and its body taken offstage (MP-4).</summary>
    public float ReleaseAfterSilenceS { get; init; } = 30f;

    /// <summary>How long a player watches its body lie where it fell before it is stood up afresh as somebody new (MP-4).</summary>
    public float RespawnS { get; init; } = 8f;

    /// <summary>
    /// <b>How many bodies a served town lays for each seat</b> (MP-4): one to be, and the rest for the ones a player leaves
    /// lying in the road while the town's ambulances come for them. A bound and not a preference: they are laid with the
    /// town, offstage, and a seat whose every body is in the town's care waits for one.
    /// </summary>
    public int BodiesPerSeat { get; init; } = 3;

    /// <summary>How near a player on foot has to stand to a vehicle's body to get in it (MP-13), edge to edge.</summary>
    public float BoardReachM { get; init; } = 2f;

    /// <summary>
    /// The largest message a player may send, in bytes. An input is a few dozen; anything near this is not one, and is
    /// a connection closed rather than a buffer grown.
    /// </summary>
    public int LargestInboundBytes { get; init; } = 512;

    /// <summary>How long a served town keeps a browser's connection for its next request for a file of the page (MP-11).</summary>
    public float PageIdleS { get; init; } = 5f;

    /// <summary>The port a served town listens on.</summary>
    public int Port { get; init; } = 8740;
}
