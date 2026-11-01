// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class SorceressCombat
{
    public static bool Implemented(CombatAbility a) => a is CombatAbility.FireBolt or CombatAbility.Fireball
        or CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.GlacialSpike or CombatAbility.Nova
        or CombatAbility.FrostNova or CombatAbility.StaticField or CombatAbility.Warmth
        or CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor or CombatAbility.EnergyShield
        or CombatAbility.Telekinesis or CombatAbility.ThunderStorm or CombatAbility.Lightning or CombatAbility.ChainLightning or CombatAbility.Enchant or CombatAbility.ChargedBolt
        or CombatAbility.Blaze or CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.Blizzard or CombatAbility.FrozenOrb or CombatAbility.Hydra
        or CombatAbility.FireMastery or CombatAbility.LightningMastery or CombatAbility.ColdMastery or CombatAbility.Teleport;
    public static bool Handles(CombatAbility a) => Implemented(a) && a != CombatAbility.Teleport && ClassAbilities.Find(a) is { Passive: false };
    public static bool Available(PlayerMobile owner, CombatAbility a) => Handles(a)
        && ClassCreation.Profiles.TryGet(owner.Serial, out var profile) && profile.Class == DiabloClass.Sorceress && SkillMechanics.Rank(owner, a) > 0;
    public static int Passive(PlayerMobile owner, CombatAbility ability)
    {
        if (!ClassCreation.Profiles.TryGet(owner.Serial, out var profile) || profile.Class != DiabloClass.Sorceress
            || SkillMechanics.Rank(owner, ability) <= 0) { return 0; }
        var row = SkillMechanics.For(ability); int rank = SkillMechanics.Rank(owner, ability);
        return (int)new SkillValues(row, key => key == row.SourceKey ? rank : 0, _ => 0, profile.Level).Eval("passivecalc1");
    }
    public static bool ValidTarget(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if(ability==CombatAbility.Telekinesis){return TelekinesisWorld.Valid(owner,selected);}
        // Static's receiver needs a living world caster. Refuse before the
        // player's release payment, including direct server/API callers.
        if(ability is CombatAbility.StaticField or CombatAbility.ThunderStorm&&(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal)){return false;}
        if (ability == CombatAbility.Enchant)
        {
            if (selected == Serial.Zero) { return owner.Map != null && owner.Map != Map.Internal; }
            var actor = World.FindMobile(selected);
            return actor is { Alive: true, Deleted: false } && actor.Map == owner.Map && owner.InRange(actor, CombatProtocol.TeethRange)
                && owner.InLOS(actor) && (actor == owner || ClassCombat.Friendly(owner, actor) || ClassCombat.IsHostile(owner, actor));
        }
        if (ClassAbilities.SelfTarget(ability))
        { return ability is not (CombatAbility.ThunderStorm or CombatAbility.Blaze or CombatAbility.StaticField or CombatAbility.Nova or CombatAbility.FrostNova) || Server.Spells.SpellHelper.CheckTown(owner.Location, owner); }
        if (ability is CombatAbility.FireBolt or CombatAbility.Fireball or CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.GlacialSpike or CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.Blizzard or CombatAbility.FrozenOrb or CombatAbility.Hydra or CombatAbility.ChainLightning or CombatAbility.Lightning or CombatAbility.ChargedBolt && (!Server.Spells.SpellHelper.CheckTown(owner.Location, owner)
            || !Server.Spells.SpellHelper.CheckTown(point, owner))) { return false; }
        return owner.Map != null && owner.Map != Map.Internal && owner.InRange(point, CombatProtocol.TeethRange) && owner.InLOS(point)
            && point.X >= 0 && point.Y >= 0 && point.X < owner.Map.Width && point.Y < owner.Map.Height
            && (selected == Serial.Zero || ClassCombat.IsHostile(owner, World.FindMobile(selected)));
    }
    public static bool Begin(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point, long now)
        => SkillCombat.Begin(owner, ability, selected, point, now, ValidTarget);
    public static bool Release(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if (!Available(owner, ability) || !ValidTarget(owner, ability, selected, point) || !FireGround.Ready(owner, ability)) { return false; }
        var values = SkillMechanics.Values(owner, ability);
        if (ability is CombatAbility.FireBolt or CombatAbility.Fireball or CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.GlacialSpike or CombatAbility.Lightning) { return SorceressSpellMissiles.Cast(owner, point, values); }
        if (ability==CombatAbility.ChargedBolt){return SorceressSpellMissiles.CastCharged(owner,point,values);}
        if (ability==CombatAbility.ChainLightning){return ChainLightning.Release(owner,point,values);}
        if (ability==CombatAbility.ThunderStorm){return ThunderStorm.Release(owner,values);}
        if (SorceressSpellMissiles.Ring(ability)) { return SorceressSpellMissiles.CastRing(owner,values); }
        if(ability==CombatAbility.Telekinesis&&World.FindMobile(selected)!=null){return TelekinesisCombat.Release(owner,selected,values);}
        if (ability == CombatAbility.Hydra && !Hydras.CanPlace(owner, point)) { return false; }
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        FireGround.Delay(owner, ability);
        owner.PlaySound(0x1E5); // Placeholder cast sound.
        if (SorceressEffects.IsArmor(ability)) { SorceressEffects.CastArmor(owner, ability, values); return true; }
        if (ability == CombatAbility.EnergyShield) { SorceressEffects.CastShield(owner, values); return true; }
        if (ability == CombatAbility.Blaze) { FireGround.CastBlaze(owner, values); return true; }
        if (ability == CombatAbility.FireWall) { FireGround.CastWall(owner, point, values); return true; }
        if (ability == CombatAbility.Meteor) { FireGround.CastMeteor(owner, point, values); return true; }
        if (ability == CombatAbility.Blizzard) { Blizzards.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.FrozenOrb) { FrozenOrbs.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.Hydra) { Hydras.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.Enchant)
        {
            var target = World.FindMobile(selected);
            SorceressEffects.CastEnchant(owner, target == owner || ClassCombat.Friendly(owner, target) ? target : owner, values);
            return true;
        }
        if (ability == CombatAbility.Telekinesis)
        {
            if(World.FindItem(selected) is{} item){TelekinesisWorld.Operate(owner,item);return true;}
            throw new InvalidOperationException("Telekinesis target changed during synchronous release");
        }
        if (ability == CombatAbility.StaticField) { return StaticFields.Cast(owner,owner,values); }
        throw new NotSupportedException("Unmapped Sorceress release: "+ability);
    }
}
