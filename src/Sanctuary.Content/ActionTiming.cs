using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public enum AttackFamily { Unarmed, Thrust, Swing, TwoHandedSwing, TwoHandedThrust, Bow, Crossbow, Throw }
public readonly record struct WeaponTiming(AttackFamily Family, int Modifier);
public readonly record struct ActionTiming(int Frames, int EffectFrame)
{
    public const int FrameMs = 40;
    public int DurationMs => Frames * FrameMs;
    public int EffectMs => EffectFrame * FrameMs;
}
public static class CombatTiming
{
    public static bool WeaponAttack(CombatAbility ability)=>ability is CombatAbility.ShockWeb or CombatAbility.FireBlast or CombatAbility.LightningFury or CombatAbility.PlagueJavelin or CombatAbility.LightningBolt or CombatAbility.PoisonJavelin or CombatAbility.ExplodingArrow or CombatAbility.IceArrow or CombatAbility.GuidedArrow or CombatAbility.FreezingArrow or CombatAbility.Strafe or CombatAbility.ImmolationArrow or CombatAbility.ColdArrow or CombatAbility.FireArrow or CombatAbility.MultipleShot or CombatAbility.MagicArrow or CombatAbility.BasicAttack or CombatAbility.Fend or CombatAbility.Impale or CombatAbility.ChargedStrike or CombatAbility.PowerStrike or CombatAbility.Jab or CombatAbility.PoisonDagger or CombatAbility.Vengeance or CombatAbility.Sacrifice or CombatAbility.Smite or CombatAbility.Zeal or CombatAbility.Charge or CombatAbility.Conversion or CombatAbility.Bash or CombatAbility.Stun or CombatAbility.Concentrate or CombatAbility.Berserk or CombatAbility.DoubleThrow;
    // Each row is a class; columns follow AttackFamily. These are project balance profiles.
    private static readonly int[][] _attacks =
    [
        [14, 13, 15, 18, 16, 13, 20, 14], // Amazon
        [16, 15, 18, 19, 21, 19, 23, 18], // Sorceress
        [16, 14, 17, 20, 20, 18, 22, 17], // Necromancer
        [14, 14, 14, 18, 17, 17, 21, 16], // Paladin
        [13, 13, 14, 17, 17, 17, 20, 13], // Barbarian
        [15, 15, 17, 16, 19, 18, 22, 17], // Druid
        [13, 12, 14, 18, 16, 17, 21, 13], // Assassin
        [16, 14, 17, 20, 20, 18, 22, 17]  // Warlock: provisional UO weapon animation profile
    ];
    private static readonly int[] _casts = [19, 13, 15, 15, 13, 18, 16, 16];
    private static readonly Dictionary<string, int> _modifiers = new(StringComparer.Ordinal)
    {
        ["Dagger"] = -20, ["SkinningKnife"] = -10, ["ButcherKnife"] = -10, ["Cleaver"] = 0,
        ["Katana"] = -10, ["Cutlass"] = -10, ["Scimitar"] = -10, ["Broadsword"] = 0,
        ["Longsword"] = 0, ["VikingSword"] = 10, ["BoneHarvester"] = -10, ["CrescentBlade"] = -10,
        ["Halberd"] = 0, ["Bardiche"] = 10, ["Scythe"] = -10,
        ["Hatchet"] = -10, ["Axe"] = 10, ["BattleAxe"] = 10, ["DoubleAxe"] = 10,
        ["LargeBattleAxe"] = 10, ["TwoHandedAxe"] = 10, ["ExecutionersAxe"] = 0, ["WarAxe"] = 0,
        ["Pickaxe"] = 10, ["Mace"] = 0, ["WarMace"] = 10, ["Maul"] = 10,
        ["WarHammer"] = 20, ["HammerPick"] = 10, ["Scepter"] = 0, ["Club"] = 10,
        ["Spear"] = 0, ["ShortSpear"] = -10, ["Pitchfork"] = 0, ["WarFork"] = -10, ["Lance"] = 20,
        ["QuarterStaff"] = -10, ["BlackStaff"] = 0, ["GnarledStaff"] = 10, ["ShepherdsCrook"] = 10,
        ["Bow"] = 0, ["Crossbow"] = 0, ["HeavyCrossbow"] = 10, ["RepeatingCrossbow"] = -40,
        ["CompositeBow"] = 10, ["ElvenCompositeLongbow"] = 0, ["MagicalShortbow"] = -10, ["Yumi"] = 10,
        ["Wakizashi"] = -20, ["NoDachi"] = 10, ["Daisho"] = 0, ["Tekagi"] = -20,
        ["Sai"] = -20, ["Kama"] = -10, ["Tessen"] = -10, ["Nunchaku"] = -10,
        ["Bokuto"] = 0, ["Lajatang"] = 0, ["Tetsubo"] = 20, ["Fists"] = 0
    };
    public static BaseWeapon Equipped(PlayerMobile from)
    {
        var weapon = WeaponHands.Selected(from) ?? from.FindItemOnLayer(Layer.OneHanded) as BaseWeapon
            ?? from.FindItemOnLayer(Layer.TwoHanded) as BaseWeapon;
        return ClassGear.Broken(weapon) ? null : weapon;
    }
    public static WeaponTiming Weapon(BaseWeapon weapon)
    {
        if (weapon == null || ClassGear.Broken(weapon) || weapon.Type == WeaponType.Fists)
        {
            return new WeaponTiming(AttackFamily.Unarmed, 0);
        }
        var family = WeaponHands.Melee(weapon)?(WeaponHands.TwoHanded(weapon)?AttackFamily.TwoHandedThrust:AttackFamily.Thrust):Animation(weapon) switch
        {
            WeaponAnimation.Pierce1H => AttackFamily.Thrust,
            WeaponAnimation.Pierce2H => AttackFamily.TwoHandedThrust,
            WeaponAnimation.Bash2H or WeaponAnimation.Slash2H => AttackFamily.TwoHandedSwing,
            WeaponAnimation.ShootBow => AttackFamily.Bow,
            WeaponAnimation.ShootXBow => AttackFamily.Crossbow,
            WeaponAnimation.Throwing => AttackFamily.Throw,
            _ => AttackFamily.Swing
        };
        if (ClassGear.Base(weapon) is { } converted)
        {
            return new WeaponTiming(family, converted.Speed);
        }
        for (var type = weapon.GetType(); type != null; type = type.BaseType)
        {
            if (_modifiers.TryGetValue(type.Name, out var modifier))
            {
                return new WeaponTiming(family, modifier);
            }
        }
        // Unlisted expansion/artifact bases receive a bounded rating from their base speed.
        var fallback = weapon.MlSpeed switch { > 0 and <= 2 => -20, <= 2.5f and > 0 => -10, <= 3.5f => 0, <= 4.25f => 10, _ => 20 };
        return new WeaponTiming(family, fallback);
    }
    internal static WeaponAnimation Animation(BaseWeapon weapon)
    {
        var animation=weapon?.Animation??WeaponAnimation.Wrestle;
        return weapon!=null && ClassGear.Base(weapon) is{OneOrTwoHanded:true} && !WeaponHands.TwoHanded(weapon)?animation switch
        {WeaponAnimation.Slash2H=>WeaponAnimation.Slash1H,WeaponAnimation.Bash2H=>WeaponAnimation.Bash1H,WeaponAnimation.Pierce2H=>WeaponAnimation.Pierce1H,_=>animation}:animation;
    }
    public static int Ias(PlayerMobile from) => Math.Clamp(AosAttributes.GetValue(from, AosAttribute.WeaponSpeed), 0, 10000);
    public static int Fcr(PlayerMobile from) => Math.Clamp(AosAttributes.GetValue(from, AosAttribute.CastSpeed), 0, 10000);
    public static ActionTiming For(PlayerMobile from, CombatAbility ability, BaseWeapon weapon = null)
    {
        if(ability is CombatAbility.FireBlast or CombatAbility.ShockWeb){return FireBlasts.Timing(from);}
        if(AmazonJavelins.Handles(ability)){return AmazonJavelins.Timing(WeaponSequenceAdvance(from));}
        if(AmazonBow.Handles(ability) || ability==CombatAbility.Strafe){return AmazonBow.Timing(from);}
        if(ability==CombatAbility.Zeal){return PaladinZeal.Timing(from);}
        if(ability==CombatAbility.Jab){return AmazonJab.Timing(from);}
        if(ability==CombatAbility.Impale){return AmazonImpale.Timing(from);}
        if(ability==CombatAbility.Fend){return AmazonFend.Timing(from);}
        if(ability is CombatAbility.PowerStrike or CombatAbility.ChargedStrike){return AmazonMelee.Timing(from);}
        ClassCreation.Profiles.TryGet(from.Serial, out var profile);
        return Calculate(profile?.Class ?? DiabloClass.Necromancer, ability, Weapon(weapon ?? Equipped(from)), Ias(from), Fcr(from),
            WeaponAttack(ability) ? GearEffects.AttackSlow(from) : 0, PaladinOffense.AttackRate(from)+AssassinBuffs.AttackRate(from));
    }
    internal static int SequenceRateAdjustment(CombatAbility ability)=>ability switch
    {
        CombatAbility.Jab or CombatAbility.DoubleThrow=>-30,
        // Released2.4/current Param9 and the pinned original calculator place
        // Impale's addition in this same layer, cancelling its sequence penalty.
        CombatAbility.Impale=>-30+SkillMechanics.For(ability).Number("Param9"),
        _=>0
    };
    internal static int FrameAdvance(WeaponTiming weapon,int speed,int slow,int skillRate,int rateAdjustment=0,int baseSpeed=256)
    {
        int bonus=Math.Clamp(speed,0,10000),effective=120*bonus/(120+bonus);
        return baseSpeed*Math.Clamp(100+effective+skillRate-weapon.Modifier-Math.Clamp(slow,0,85)+rateAdjustment,15,175)/100;
    }
    internal static int WeaponSequencePoint(PlayerMobile owner,int point,CombatAbility ability)
    {
        int advance=WeaponSequenceAdvance(owner,ability);
        return (point*256+advance-1)/advance;
    }
    internal static int WeaponSequenceAdvance(PlayerMobile owner,CombatAbility ability=CombatAbility.BasicAttack)=>FrameAdvance(Weapon(Equipped(owner)),Ias(owner),GearEffects.AttackSlow(owner),PaladinOffense.AttackRate(owner)+AssassinBuffs.AttackRate(owner),SequenceRateAdjustment(ability));
    public static ActionTiming Calculate(DiabloClass characterClass, CombatAbility ability, WeaponTiming weapon, int ias = 0, int fcr = 0, int chill = 0, int skillAttackRate = 0)
    {
        if(ability is CombatAbility.FireBlast or CombatAbility.ShockWeb){return FireBlasts.Timing(FrameAdvance(weapon,ias,chill,skillAttackRate,baseSpeed:128));}
        int row = Math.Clamp((int)characterClass - 101, 0, 7);
        bool cast = !WeaponAttack(ability);
        int advance=FrameAdvance(cast?new(weapon.Family,0):weapon,cast?fcr:ias,chill,cast?0:skillAttackRate,cast?0:SequenceRateAdjustment(ability));
        if(AmazonJavelins.Handles(ability)){return AmazonJavelins.Timing(advance);}
        if(AmazonBow.Handles(ability) || ability==CombatAbility.Strafe){return AmazonBow.Timing(weapon.Family,advance);}
        if(ability==CombatAbility.Jab)
        {
            var schedule=AmazonJab.Schedule(weapon.Family==AttackFamily.TwoHandedThrust?"2HT":"1HT",advance);
            return new(schedule.End,schedule.Events[0]);
        }
        if(ability==CombatAbility.Impale){return AmazonImpale.Timing(weapon.Family==AttackFamily.TwoHandedThrust?"2HT":"1HT",advance);}
        if(ability==CombatAbility.Fend){return AmazonFend.Timing(weapon.Family==AttackFamily.TwoHandedThrust?"2HT":"1HT",advance);}
        if(ability is CombatAbility.PowerStrike or CombatAbility.ChargedStrike){return AmazonMelee.Timing(weapon.Family==AttackFamily.TwoHandedThrust?"2HT":"1HT",advance);}
        if(ability==CombatAbility.DoubleThrow){return new((12*256+advance-1)/advance,(5*256+advance-1)/advance);}
        if(ability==CombatAbility.Charge){return new ActionTiming(15,1);}
        if(ability==CombatAbility.Leap){return new ActionTiming(14,5);}
        if(ability==CombatAbility.Whirlwind){return new ActionTiming(8,3);}
        if(ability==CombatAbility.Zeal){return PaladinZeal.Timing(PaladinZeal.FamilyCode(weapon.Family),advance);}
        if(ability==CombatAbility.Smite)
        {
            // Legacy PAS1 HTH/1HS/1HT: twelve frames, speed256, event7, no starting-frame bonus.
            int duration=Math.Max(1,(12*256+advance-1)/advance-1);
            return new ActionTiming(duration,Math.Clamp((7*256+advance-1)/advance,1,duration));
        }
        if (ability == CombatAbility.Inferno)
        {
            // Source SQ6 has 15 points and its event at point10; channel/re-entry is a separate consumer.
            return new ActionTiming((15 * 256 + advance - 1) / advance, (10 * 256 + advance - 1) / advance);
        }
        if (ability is CombatAbility.Lightning or CombatAbility.ChainLightning)
        {
            // Sequence 12 ends at point 19, emits at point 7, and has no ordinary cast minus-one.
            return new ActionTiming((19 * 256 + advance - 1) / advance, (7 * 256 + advance - 1) / advance);
        }
        int baseFrames = cast ? _casts[row] : _attacks[row][(int)weapon.Family];
        int frames = Math.Max(4, ((baseFrames + 1) * 256 + advance - 1) / advance - 1);
        int effect = Math.Clamp((frames * (cast ? 45 : 50) + 99) / 100, 1, frames - 1);
        return new ActionTiming(frames, effect);
    }
    public static string Label(int frames) => frames switch
    {
        <= 12 => "Very Fast", <= 15 => "Fast", <= 18 => "Normal", <= 22 => "Slow", _ => "Very Slow"
    };
    public static int Range(PlayerMobile from, CombatAbility ability) => ability is CombatAbility.FireBlast or CombatAbility.ShockWeb?CombatProtocol.TeethRange:AmazonBow.Handles(ability) || ability is CombatAbility.LightningFury or CombatAbility.PlagueJavelin or CombatAbility.LightningBolt or CombatAbility.PoisonJavelin or CombatAbility.Strafe?CombatProtocol.SightRange:ability is CombatAbility.Fend or CombatAbility.Impale or CombatAbility.Jab or CombatAbility.PowerStrike or CombatAbility.ChargedStrike?AmazonJab.Range(from):ability==CombatAbility.Whirlwind?CombatProtocol.SightRange:ability==CombatAbility.Leap?Math.Max(1,(int)Math.Ceiling(SkillGeometry.Tiles((int)SkillMechanics.Values(from,ability).Eval("aurarangecalc")))):ability==CombatAbility.Charge?(int)SkillGeometry.Tiles(100):ability==CombatAbility.Smite?PaladinSmite.Range(from):WeaponAttack(ability)?WeaponRange(Equipped(from)):(NecromancerCombat.Handles(ability) || SorceressCombat.Handles(ability) || PaladinCombat.Handles(ability) && !PaladinCombat.IsAura(ability)) ? CombatProtocol.TeethRange : ability == CombatAbility.Teleport ? Teleportation.Range : ability == CombatAbility.RaiseSkeleton ? SkeletonPets.Range : ability == CombatAbility.Teeth ? CombatProtocol.TeethRange
        : WeaponRange(Equipped(from));
    private static int WeaponRange(BaseWeapon weapon)
    {
        if (ClassGear.Broken(weapon)) { return 1; }
        // Provisional class-combat comparison; native UO reach and other bases stay intact.
        int range = Math.Max(weapon?.MaxRange ?? 1, weapon is Halberd ? 2 : 1);
        return Math.Clamp(range, 1, CombatProtocol.SightRange);
    }
    public static bool AddSpeedProperties(BaseWeapon weapon, IPropertyList list)
    {
        var basis = Weapon(weapon);
        if (weapon.Parent is PlayerMobile player && ClassCombat.Enabled(player))
        {
            ClassCreation.Profiles.TryGet(player.Serial, out var profile);
            var timing = For(player, CombatAbility.BasicAttack, weapon);
            list.Add(1061167, $"{Label(timing.Frames)} ({DiabloClasses.DisplayName(profile.Class, player.Female)}, {timing.DurationMs / 1000.0:0.00}s)");
            if (weapon is Halberd)
            {
                list.Add($"{"Class melee reach: "}{WeaponRange(weapon)}{" tiles"}");
            }
        }
        else
        {
            var bonus = Math.Clamp(weapon.Attributes.WeaponSpeed, 0, 10000);
            var rating = basis.Modifier - 120 * bonus / (120 + bonus);
            var label = rating switch { <= -20 => "Very Fast", <= -10 => "Fast", <= 0 => "Normal", <= 10 => "Slow", _ => "Very Slow" };
            list.Add(1061167, $"{label} (base)");
        }
        list.Add($"{"Base speed modifier: "}{basis.Modifier:+0;-0;0}");
        return true;
    }
}
