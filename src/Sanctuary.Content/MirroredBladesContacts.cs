// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Optional original-flight metadata;absent on all historical weapon saves.
public sealed record MirroredMissileSource(int Rank,int Ordinal,bool Basic,int Kind,ulong Seed);
internal static class MirroredBladesContacts
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Random Effects=Random.Shared;
    internal static MirroredBladeAttack Attack(int rank,int ordinal,bool basic=false)
    {
        var values=MirroredBladesPrepared.Capture(new(SkillMechanics.For(CombatAbility.MirroredBlades),k=>k=="Mirrored Blades"?rank:0,_=>0,99));
        var attacks=MirroredBladesPrepared.Attacks(values);
        if(ordinal<0||ordinal>=attacks.Length||basic&&ordinal!=0){throw new InvalidDataException("Invalid Mirrored attack ordinal");}
        return attacks[ordinal];
    }
    internal static int? Divisor(MirroredBladeAttack attack,bool ranged,Mobile target)
    {
        if(attack.Ordinal==0){return null;}
        // Explicit pending target mapping:use player divisor for controlled
        // actors;source ranged already includes its ranged factor.
        if(target is PlayerMobile or BaseCreature{Controlled:true}){return attack.RawPlayerDivisor;}
        return checked((ranged?attack.RawRangedDivisor:attack.RawMeleeDivisor)*(NamedCombat.Elite(target)?2:1));
    }
    internal static bool Valid(WeaponFlight f)
    {
        if(f?.Mirrored is not{} m||f.Ability!=CombatAbility.MirroredBlades||m.Rank is <1 or >255||m.Seed==0||m.Kind is <1 or >3
            ||f.SkillRank!=m.Rank||f.Frames!=50||f.Activation!=0||f.NextDelay!=0||f.Flags!=32||f.Guided!=null||f.HostileSpell||f.Npc!=null
            ||f.Slow!=null||f.MechanicalProfile!=null||f.SourceSteps!=null||f.Damage?.Elements==null||!f.Damage.SourcePoisonFrames
            ||f.Damage.PhysicalToMagicPercent!=0||f.Damage.Conversion!=null||f.Damage.LightningJavelinPercent!=null||f.Damage.FreezeColdLength
            ||f.Missile!=(m.Kind==1?"arrow":m.Kind==2?"bolt":"javelin")){return false;}
        try
        {
            _=Attack(m.Rank,m.Ordinal,m.Basic);
            int expected=m.Basic?0:100+15*(m.Rank-1);return f.Damage.SkillAttackPercent==expected;
        }
        catch(ArgumentException){return false;}
        catch(NotSupportedException){return false;}
        catch(InvalidDataException){return false;}
    }
    internal static PreparedWeaponImpact Impact(PlayerMobile owner,BaseCreature target,MirroredWeaponRoll rolled)
    {
        var parts=SkillCombat.MitigatePrepared(owner,target,rolled.Damage,out int absorbed);
        var poison=PoisonJavelins.PreparePoison(owner,target,rolled.PoisonRate256,rolled.PoisonFrames);
        return new(rolled.Damage,parts,absorbed){LifeDamage256=checked(NamedElements.To256(parts.Total)+poison.Rate256),AllocatedPoison=poison};
    }
    internal static PlayerWeaponContactPrepared Melee(PlayerMobile owner,BaseCreature target,MirroredWeaponPacket packet,Func<bool> inRange,ref ulong seed,bool basic)
    {
        var e=packet.Damage.Elements;ulong local=seed;
        PreparedWeaponImpact Allocate()=>Impact(owner,target,MirroredBladesPackets.Roll(packet,target,ref local));
        int frames=e.ColdMin>0?ColdStates.LengthFrames(e.ColdFrames,NamedElements.Resistance(owner,target,"cold",target.ColdResistance),
            ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target)):0;
        var prepared=PlayerWeaponContact.PrepareOrdinary(owner,target,basic?0:packet.Input.Skill.AttackPercent,0,inRange,HitRoll,()=>0,_=>0,_=>0,Effects,
            allowNative:true,captured:Allocate,capturedColdFrames:frames,capturedDrain:new(e.LifeDrain,e.ManaDrain),
            crushingDivisor:Divisor(packet.Attack,false,target),suppressWear:packet.Attack.Ordinal!=0);
        seed=local;return prepared with{RangeCheck=_=>inRange()};
    }
    internal static MirroredMissileSource Missile(PlayerMobile owner,BaseCreature target,WeaponFlight flight,out bool damaged)
    {
        damaged = false;
        if(!Valid(flight)){throw new NotSupportedException("Invalid Mirrored projectile context");}
        TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,target);WeaponStartEffects.Require(owner);
        var m=flight.Mirrored;ulong seed=m.Seed;
        // Distinct ordinary accuracy per missile;no Echoing automatic hit rule.
        if(ClassStats.CheckHit(owner,target,flight.Damage.SkillAttackPercent,HitRoll,projectile:true)!=true){return m;}
        WeaponStartEffects.SuccessfulHitRoll(owner,target);
        var roll=MirroredBladesPackets.RollDamage(flight.Damage,Attack(m.Rank,m.Ordinal,m.Basic),target,ref seed);
        if(!SourceBlocking.TryBlock(target,roll.Damage.Physical>0,out bool blocked)){throw new NotSupportedException("Mirrored projectile recipient block is unmapped");}
        if(blocked){return m with{Seed=seed};}
        owner.DoHarmful(target,true);WeaponStartEffects.PrepareDamage(owner,target);
        var parts=SkillCombat.MitigatePrepared(owner,target,roll.Damage,out int absorbed);
        var point = target.Location;
        var map = target.Map;
        int before = m.Basic ? 0 : SkillHealth.Balance256(target);
        NamedCombat.Crushing(owner,target,true,Effects,Divisor(Attack(m.Rank,m.Ordinal,m.Basic),true,target));
        PreparedWeaponEvents.BeforeReturn(owner,target,NamedElements.To256(parts.Physical),true,absorbed,
            clipped=>WeaponLeech.Apply(owner,target,clipped,flight.Damage.Elements));
        if(roll.Damage.Cold>0){ColdStates.Apply(owner,target,roll.ColdFrames,TimedEffects.TickAt(flight.NextUtc));}
        int loss=SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));CreatureHitRecovery.Apply(target,owner,loss);
        if(target is{Alive:true,Deleted:false})
        {
            if(roll.PoisonRate256>0){PoisonJavelins.Poison(owner,target,roll.PoisonRate256,roll.PoisonFrames,flight.NextUtc);}
            NecromancerEffects.WeaponHit(owner,target,Math.Min(loss/256,(int)parts.Physical),true,lifeTap:false,ironMaiden:false);
            NamedCombat.AfterStrike(owner,target,true,Effects,setupApplied:true);
        }
        GemCombat.WeaponHit(owner,target,loss/256,true);CombatDamage.Finish(target,owner);
        damaged = !m.Basic && (loss > 0 || target.Deleted || SkillHealth.Balance256(target) < before);
        if (damaged && !target.Hidden) { MirroredBladesVisuals.Hit(map, point); }
        return m with{Seed=seed};
    }
}
