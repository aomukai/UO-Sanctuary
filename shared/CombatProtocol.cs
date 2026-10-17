namespace Sanctuary;
public enum CombatAbility : byte { None, BasicAttack, Teeth, RaiseSkeleton }
public enum CombatRequest : byte { Hello, Attack, Stop, Cast, HoldCast, ReleaseCast, MovementMode, HoldAttack, ReleaseAttack }
public static class CombatProtocol
{
    public static string Label(CombatAbility ability) => ability switch
    {
        CombatAbility.BasicAttack => "Attack",
        CombatAbility.RaiseSkeleton => "Raise Skeleton",
        _ => ability.ToString()
    };
    public const ushort SubCommand = 0xF2;
    public const ushort AnimationSubCommand = 0xF4;
    public const byte Version = 4;
    public const int SightRange = 18, BasicRange = 1, TeethRange = 16, MaxHostiles = 64;
    public const int HoldHeartbeatMs = 200, HoldLeaseMs = 750;
    public const int CombatTickMs = 40, StateIntervalMs = 200;
}
