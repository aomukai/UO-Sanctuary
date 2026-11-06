namespace Sanctuary;
public enum CombatRequest : byte { Hello, Attack, Stop, Cast, HoldCast, ReleaseCast, MovementMode, HoldAttack, ReleaseAttack, ChannelStart, ChannelAim, ChannelEnd }
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
}
