namespace TrafficSimulation.Multiplayer.Client;

/// <summary>
/// <b>Where the server's clock stands, as a player can tell from what reaches it</b> (MP-7), and the instant the player
/// draws: a fixed delay behind that.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every arrival says the server was at least that far along</b>: a snapshot of a tick cannot arrive before the tick.
/// So the least-delayed of the recent arrivals — the largest server time less local time — is the estimate, and a late
/// one says nothing about the clock.
/// </para>
/// <para>
/// <b>The picture never jumps</b>: the instant drawn runs at real time, a share faster or slower while it closes on the
/// estimate, so a server that slows (SIM-9) is followed and one arrival early or late is not seen. Only a first arrival,
/// or one further off than the delay itself, sets it outright.
/// </para>
/// </remarks>
internal sealed class ServerClock(int samples, float delayS, float slewShare)
{
    readonly double[] _offsets = new double[samples];
    int _held;
    int _next;
    double _drawnS = double.NaN;
    double _lastLocalS;

    /// <summary>A snapshot of the server at <paramref name="serverS"/> arrived at the local <paramref name="localS"/>.</summary>
    public void Arrived(double serverS, double localS)
    {
        _offsets[_next] = serverS - localS;
        _next = (_next + 1) % _offsets.Length;
        _held = Math.Min(_held + 1, _offsets.Length);
    }

    /// <summary>The server's time now, as the arrivals tell it, or NaN before the first.</summary>
    public double ServerS(double localS)
    {
        if (_held == 0) return double.NaN;

        var offset = double.MinValue;
        for (var i = 0; i < _held; i++) offset = Math.Max(offset, _offsets[i]);
        return localS + offset;
    }

    /// <summary>
    /// <b>The instant to draw at, advanced to the local <paramref name="localS"/></b>: real time since the last frame,
    /// slewed by at most the share towards the estimate less the delay; NaN before the first arrival.
    /// </summary>
    public double Drawn(double localS)
    {
        var wantedS = ServerS(localS) - delayS;
        if (double.IsNaN(wantedS)) return double.NaN;

        var sinceS = localS - _lastLocalS;
        _lastLocalS = localS;
        if (double.IsNaN(_drawnS) || Math.Abs(wantedS - (_drawnS + sinceS)) > delayS)
        {
            _drawnS = wantedS;
            return _drawnS;
        }

        var slewS = sinceS * slewShare;
        _drawnS += sinceS + Math.Clamp(wantedS - (_drawnS + sinceS), -slewS, slewS);
        return _drawnS;
    }
}
