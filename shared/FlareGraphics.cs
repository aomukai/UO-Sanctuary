namespace Sanctuary;

public static class FlareGraphics
{
    public const int FlightFrames = 16;
    public const int BurstFrames = 7;
    public const int FrameMilliseconds = 50;

    public const ushort RedFlight = 65000;
    public const ushort BlueFlight = RedFlight + FlightFrames;
    public const ushort RedBurst = BlueFlight + FlightFrames;
    public const ushort BlueBurst = RedBurst + BurstFrames;

    public static bool IsFlight(ushort graphic) => graphic is RedFlight or BlueFlight;
}
