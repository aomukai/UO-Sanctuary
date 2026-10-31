// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class NecromancerCombat
{
    public static bool Implemented(CombatAbility ability) => NecromancerEffects.IsCurse(ability)
        || ability is CombatAbility.Teeth or CombatAbility.RaiseSkeleton or CombatAbility.SkeletonMastery
        or CombatAbility.BoneArmor or CombatAbility.PoisonDagger or CombatAbility.CorpseExplosion
        or CombatAbility.BoneSpear or CombatAbility.BoneSpirit or CombatAbility.PoisonExplosion or CombatAbility.PoisonNova
        or CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.IronGolem or CombatAbility.FireGolem
        or CombatAbility.BoneWall or CombatAbility.BonePrison
        or CombatAbility.GolemMastery or CombatAbility.SummonResist or CombatAbility.RaiseSkeletalMage or CombatAbility.Revive;
    public static bool Handles(CombatAbility ability) => ClassAbilities.Find(ability)?.Class == DiabloClass.Necromancer
        && ability != CombatAbility.RaiseSkeleton && Implemented(ability);
    public static bool CorpseTarget(CombatAbility ability) => ability is CombatAbility.CorpseExplosion
        or CombatAbility.PoisonExplosion or CombatAbility.RaiseSkeletalMage or CombatAbility.Revive;
    public static bool Available(PlayerMobile owner, CombatAbility ability) => Implemented(ability)
        && ClassAbilities.Find(ability) is { Passive: false, Class: DiabloClass.Necromancer }
        && ClassCreation.Profiles.TryGet(owner.Serial, out var profile) && profile.Class == DiabloClass.Necromancer
        && SkillMechanics.Rank(owner, ability) > 0;
    public static bool ValidTarget(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if(ability==CombatAbility.IronGolem){return IronGolemCasting.Plan(owner,World.FindItem(selected),out _,out _);}
        if (owner.Map == null || owner.Map == Map.Internal || !owner.InRange(point, CombatProtocol.TeethRange)
            || point.X < 0 || point.Y < 0 || point.X >= owner.Map.Width || point.Y >= owner.Map.Height
            || !owner.InLOS(point)) { return false; }
        if (CorpseTarget(ability))
        {
            var corpse=World.FindItem(selected) as Corpse;
            if(ability==CombatAbility.Revive){return ReviveCasts.Plan(owner,corpse,out _,out _,out _);}
            return ability==CombatAbility.CorpseExplosion
                ?CorpseExplosionCasts.Supported(corpse) && CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
                :SkeletonPets.Usable(owner,corpse);
        }
        if (ability == CombatAbility.Attract && selected == Serial.Zero) { return false; }
        if (ability == CombatAbility.BoneWall) { return BoneBarriers.Space(owner, point); }
        if (ability == CombatAbility.BonePrison)
        { return selected != Serial.Zero && ClassCombat.IsHostile(owner, World.FindMobile(selected)) && Server.Spells.SpellHelper.CheckTown(point, owner); }
        if (ability == CombatAbility.PoisonDagger)
        {
            var weapon = CombatTiming.Equipped(owner);
            return weapon != null && ClassGear.Base(weapon) is { } basis && EquipmentBalance.IsType(basis.Type, "knif")
                && ClassCombat.IsHostile(owner, World.FindMobile(selected)) && owner.InRange(point, CombatTiming.Range(owner, CombatAbility.BasicAttack));
        }
        return selected == Serial.Zero || World.FindMobile(selected) is { } target && ClassCombat.IsHostile(owner, target);
    }
    public static bool Begin(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point, long now)
    {
        if(ability==CombatAbility.PoisonDagger){return PoisonDaggerCombat.Begin(owner,selected,point,now);}
        if(ability==CombatAbility.IronGolem)
        {if(!IronGolemCasting.Plan(owner,World.FindItem(selected),out _,out var itemIssue)){owner.SendMessage(0x22,itemIssue??"Select a supported metal item.");return false;}point=World.FindItem(selected).Location;}
        if(ability==CombatAbility.Revive&&!ReviveCasts.Plan(owner,World.FindItem(selected) as Corpse,out _,out _,out var issue))
        {owner.SendMessage(0x22,issue);return false;}
        if(ability==CombatAbility.RaiseSkeletalMage)
        {try{NecromancerMageBirth.Require(owner);}catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}}
        if (ability is CombatAbility.BoneArmor or CombatAbility.PoisonNova) { selected = Serial.Zero; point = owner.Location; }
        var session = ClassCombat.For(owner);
        if (!Available(owner, ability) || !owner.Alive || !AlliedOverlap.Allowed(owner) || owner.Frozen || owner.Paralyzed
            || owner.Spell != null || HitRecovery.Active(owner, now) || session.PendingAbility != CombatAbility.None
            || now - session.NextAction < 0 || !ValidTarget(owner, ability, selected, point)
            || !NecromancerEffects.CanPay(owner, SkillMechanics.Values(owner, ability).Mana256)) { return false; }
        session.Timing = CombatTiming.For(owner, ability);
        session.PendingAbility = ability; session.PendingTarget = selected; session.PendingPoint = point;
        session.ActionOrigin = owner.Location; session.ActionMap = owner.Map;
        session.PendingWeapon = CombatTiming.Equipped(owner)?.Serial ?? Serial.Zero;
        session.NextAction = now + session.Timing.DurationMs; session.EffectAt = now + session.Timing.EffectMs;
        session.Animation = ability == CombatAbility.PoisonDagger ? (byte)10 : (byte)16;
        if (point.X != owner.X || point.Y != owner.Y) { owner.Direction = owner.GetDirectionTo(point); }
        owner.RevealingAction(); ClassCombat.SendAnimation(owner, session.Animation, session.Timing.DurationMs);
        owner.Animate(session.Animation, 5, 1, true, false, 0); ClassCombat.SendState(owner, session); return true;
    }
    public static bool Release(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if(ability==CombatAbility.PoisonDagger){return PoisonDaggerCombat.Release(owner,selected,Core.TickCount);}
        if(ability==CombatAbility.PoisonNova){selected=Serial.Zero;point=owner.Location;}
        if (!Available(owner, ability) || !ValidTarget(owner, ability, selected, point)) { return false; }
        var values = SkillMechanics.Values(owner, ability);
        if (!NecromancerEffects.CanPay(owner, values.Mana256)) { return false; }
        if(ability==CombatAbility.AmplifyDamage){return AmplifyDamageCasts.Release(owner,point,values);}
        if(ability==CombatAbility.Revive){return ReviveCasts.Release(owner,World.FindItem(selected) as Corpse);}
        if(ability==CombatAbility.IronGolem){return IronGolemCasting.Release(owner,World.FindItem(selected),TimedEffects.Now,out _);}
        if(ability==CombatAbility.PoisonExplosion){return NecromancerSpellMissiles.ReleaseClouds(owner,World.FindItem(selected) as Corpse,values);}
        if(ability is CombatAbility.BoneSpear or CombatAbility.Teeth or CombatAbility.BoneSpirit or CombatAbility.PoisonNova){return NecromancerSpellMissiles.Release(owner,point,values,ability==CombatAbility.BoneSpirit?World.FindMobile(selected):null);}
        if (ability is CombatAbility.BoneWall or CombatAbility.BonePrison)
        {
            if (!BoneBarriers.Create(owner, ability, point, values)) { return false; }
            return NecromancerEffects.Pay(owner, values.Mana256);
        }
        if (ability is CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.FireGolem or CombatAbility.RaiseSkeletalMage)
        {
            if (!NecromancerSummons.Create(owner, ability, selected, point)) { return false; }
            return NecromancerEffects.Pay(owner, values.Mana256);
        }
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        owner.PlaySound(0x1E5); // Existing UO cast sound, pending skill-specific asset pass.
        if (NecromancerEffects.IsCurse(ability))
        {
            double radius = Math.Max(0.5, SkillGeometry.Tiles(values.Eval("aurarangecalc")));
            if (ability == CombatAbility.Attract)
            { return NecromancerEffects.Apply(owner, World.FindMobile(selected), ability, values.Rank); }
            foreach (var target in SkillCombat.Targets(owner, point, radius)) { NecromancerEffects.Apply(owner, target, ability, values.Rank); }
            return true;
        }
        if (ability == CombatAbility.BoneArmor) { NecromancerEffects.BoneArmor(owner, values); return true; }
        if (ability == CombatAbility.CorpseExplosion)
        {
            return CorpseExplosionCasts.Release(owner,(Corpse)World.FindItem(selected),values);
        }
        throw new InvalidOperationException("Necromancer skill advertised without a release handler.");
    }
    internal static void Consume(Corpse corpse)
    { corpse.SanctuaryConsumed = true; corpse.Animated = true; ClassLoot.RemoveHarvestedIfEmpty(corpse); }
}
