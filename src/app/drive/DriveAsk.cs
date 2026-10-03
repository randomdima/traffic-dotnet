namespace TrafficSimulation.App.Drive;

/// <summary>
/// What a hand-driven run is of: the map, the script that drives it, and where the frames and the log
/// go. Everything else about the picture is the shot path's (<see cref="Shot.ShotRequest"/>).
/// </summary>
/// <param name="ViewM">How much town a frame spans, which the script may change as it goes.</param>
/// <param name="FrameWidthPx">
/// How wide the frames written for whoever is driving may be, or nought for the frame as it was drawn.
/// <b>It is about the file and not about the picture</b> (DRV-4): a watched run draws the town at whatever
/// the window is, and the driver reading the frames wants them small enough to arrive quickly.
/// </param>
internal readonly record struct DriveAsk(
    string Map,
    string Script,
    string FramesDir,
    string? Out,
    int WidthPx,
    int HeightPx,
    float ViewM,
    string[] Ui,
    bool Validate,
    int FrameWidthPx = 0);
