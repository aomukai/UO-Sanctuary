namespace Sanctuary;
public enum CombatRequest : byte { Hello, Attack, Stop, Cast, HoldCast, ReleaseCast, MovementMode, HoldAttack, ReleaseAttack, ChannelStart, ChannelAim, ChannelEnd, CrossDoor }
public static class CombatProtocol
{
    public static string Label(CombatAbility ability) => ClassAbilities.Label(ability);
    public const ushort SubCommand = 0xF2;
    public const ushort AnimationSubCommand = 0xF4;
    public const byte Version = 7;
    // Additive v7 actions: source 0=left,1=right,2..13=quick slot.
    public const int ChannelSources = 14;
    public const int SightRange = 18, BasicRange = 1, TeethRange = 16, MaxHostiles = 64, MaxAllies = 64;
    public const int HoldHeartbeatMs = 200, HoldLeaseMs = 750;
    public const int CombatTickMs = 40, StateIntervalMs = 200;
    public const byte DoorDoubleClick = byte.MaxValue;

    internal static int PayloadLength(CombatRequest request) => request switch
    {
        CombatRequest.Hello or CombatRequest.Stop or CombatRequest.ReleaseCast or CombatRequest.ReleaseAttack => 0,
        CombatRequest.MovementMode or CombatRequest.ChannelEnd => 1,
        CombatRequest.Attack => 4,
        CombatRequest.CrossDoor => 5,
        CombatRequest.Cast or CombatRequest.HoldCast or CombatRequest.HoldAttack => 10,
        CombatRequest.ChannelStart or CombatRequest.ChannelAim => 11,
        _ => -1
    };
}

internal static class DoorwayArt
{
    private static readonly int[] _firstClosed = [0x675, 0x685, 0x695, 0x6A5, 0x6B5, 0x6C5, 0x6D5, 0x6E5,
        0x824, 0x839, 0x84C, 0x866, 0x1FED];
    private static readonly (int X, int Y)[] _openOffsets = [(-1, 1), (1, 1), (-1, 0), (1, -1),
        (1, 1), (1, -1), (0, 0), (0, -1)];

    internal static int Facing(int graphic, out bool open)
    {
        foreach (int first in _firstClosed)
        {
            int index = graphic - first;
            if ((uint)index < 16)
            {
                open = (index & 1) != 0;
                return index / 2;
            }
        }

        open = false;
        return -1;
    }

    internal static (int X, int Y) OpenOffset(int facing) => _openOffsets[facing];
}
