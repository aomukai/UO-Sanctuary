// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonAvoidance
{
    internal static Func<int,int> Roll=Utility.Random;
    public static int DodgeChance(PlayerMobile player)=>Chance(player,CombatAbility.Dodge);
    public static int AvoidChance(PlayerMobile player)=>Chance(player,CombatAbility.Avoid);
    public static int EvadeChance(PlayerMobile player)=>Chance(player,CombatAbility.Evade);
    private static int Chance(PlayerMobile player,CombatAbility ability)
    {
        if(player is not{Alive:true,Deleted:false} || ClassCreation.Profiles?.TryGet(player.Serial,out var profile)!=true
            || profile.Class!=DiabloClass.Amazon || SkillMechanics.Rank(player,ability)<=0){return 0;}
        return Chance(SkillMechanics.Values(player,ability));
    }
    private static int Chance(SkillValues values) => (int)values.Eval("passivecalc1");
    internal static int CompanionChance(CombatAbility ability, int level) => Chance(CompanionBuild.Values(ability, level));

    internal static bool Dodge(PlayerMobile player,bool projectile,long now)=>!projectile && Attempt(player,CombatAbility.Dodge,now);
    // Ordinary weapon hit checks have already attempted shield blocking.
    internal static bool Weapon(PlayerMobile player,bool projectile,long now)=>Attempt(player,projectile?CombatAbility.Avoid:CombatAbility.Dodge,now);
    internal static bool Weapon(Merydwen companion, bool projectile, long now)
    {
        if (!companion.Alive || companion.IsDeadPet || MercenaryCare.CompanionLevel(companion) is not { } level)
            return false;

        long sinceStep = now - companion.LastMoveTime;
        bool moving = companion.LastMoveTime != 0 && sinceStep >= 0 && sinceStep < companion.CurrentMoveSpeed * 1000;
        var ability = moving ? CombatAbility.Evade : projectile ? CombatAbility.Avoid : CombatAbility.Dodge;
        return TryRoll(companion, ability, CompanionChance(ability, level));
    }
    // Call once at a traced missile/area damage stage. Collision history,
    // return-fire and child/explosion creation are independent earlier events.
    // This has no shield request; ordinary physical weapons use Weapon instead.
    internal static bool Missile(Mobile defender) => defender switch
    {
        PlayerMobile player => Attempt(player, CombatAbility.Avoid, Core.TickCount),
        Merydwen companion => Weapon(companion, true, Core.TickCount),
        _ => false
    };
    private static bool Attempt(PlayerMobile player,CombatAbility ability,long now)
    {
        // Moving source modes use Evade instead. Sanctuary's accepted running
        // step rule takes precedence over source running avoidance.
        if(ClassMovement.IsRunning(player,now)){return false;}
        if(ClassMovement.IsMoving(player,now)){ability=CombatAbility.Evade;}
        return TryRoll(player, ability, Chance(player, ability));
    }
    private static bool TryRoll(Mobile defender, CombatAbility ability, int chance)
    {
        if(chance<=0 || Roll(100)>=chance){return false;}
        var (graphic, sound) = ability switch
        {
            CombatAbility.Dodge => (SpellEffectGraphics.DodgeFlash, SpellEffectSounds.Dodge),
            CombatAbility.Avoid => (SpellEffectGraphics.AvoidFlash, SpellEffectSounds.Avoid),
            _ => (SpellEffectGraphics.EvadeTrail, SpellEffectSounds.Evade)
        };
        // Present the reaction without changing movement, attack animation or recovery on the server.
        defender.FixedEffect(graphic, 5, SpellEffectGraphics.AmazonAvoidanceDuration, 0, 0);
        defender.PlaySound(sound);
        return true;
    }
}
