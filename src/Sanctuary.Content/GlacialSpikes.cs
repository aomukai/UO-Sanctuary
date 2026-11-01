// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class GlacialSpikes
{
    internal static int FreezeFrames(SkillValues values)=>checked((int)values.Eval("auralencalc"));
    internal static double Radius(SkillValues values)=>SkillGeometry.Tiles(values.Eval("aurarangecalc"));
    internal static MageMissilePacket Packet(NecromancerMinion actor)
    {
        var values=MagePetBuffs.Values(CombatAbility.GlacialSpike,10,actor.Level);
        var row=new MissileValues("glacialspike",values);
        if(row.Number("pSrvDoFunc")!=1||row.Number("pSrvHitFunc")!=13||row.Number("Range")!=40||row.Number("Vel")!=16
            ||row.Number("MaxVel")!=16||row.Number("Size")!=1||row.Number("CanSlow")!=1||row.Number("sHitPar1")!=0||row.Number("sHitPar2")!=0)
        {throw new NotSupportedException("Unmapped Glacial Spike flight/area route");}
        // Centered native source-step initialization is shared with the
        // existing InitSteps1 spell paths; exact factory/path flags remain open.
        return new("glacialspike","cold",10,values.Damage256(false),values.Damage256(true),FreezeFrames(values)){Spell=CombatAbility.GlacialSpike};
    }
    internal static void NpcExplosion(NecromancerMinion actor,PlayerMobile owner,MageMissileState state,Point3D center)
    {
        ulong seed=state.Seed;
        int raw=WeaponMissileDamage.RollValue(state.Packet.Minimum256,state.Packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        // Source hit13 prepares damage once before copying it per recipient.
        // Defense/critical/mitigation/status operate on each independent copy.
        var packet=state.Packet with{Minimum256=raw,Maximum256=raw};
        foreach(var target in SkillCombat.Targets(actor,owner,center,Radius(MagePetBuffs.Values(CombatAbility.GlacialSpike,10,actor.Level))))
        {
            if(!actor.Alive||actor.Deleted){break;}
            if(target is not{Alive:true,Deleted:false}||!actor.CanBeHarmful(target,false)){continue;}
            NecromancerMageMissiles.ImpactSpell(actor,target,packet,state.Owner,seed,state.NextUtc);
        }
        Effects.SendLocationEffect(center,actor.Map,0x36E4,8,10,0x480,0); // Identified explosion placeholder.
    }
    internal static void PlayerExplosion(PlayerMobile owner,Point3D center,SkillValues values)
    {
        int raw=WeaponMissileDamage.RollValue(values.Damage256(false),values.Damage256(true),0,Utility.Random);
        PlayerRecipients(owner,center,Radius(values),target=>
            SkillCombat.ResolveHitParts(owner,target,new(0,0,raw/256.0,0,0),true,spell:true,sourceFreezeFrames:FreezeFrames(values)));
    }
    internal static void PlayerExplosion(PlayerMobile owner,Point3D center,SorceressSpellFlight flight,out ulong seed)
    {
        int raw=SorceressSpellMissiles.Roll(flight,out seed);
        // Hit13 reads the retained skill/rank but evaluates aura range/length
        // on the current owner. Damage remains the missile's captured packet.
        var values=SkillMechanics.Values(owner,CombatAbility.GlacialSpike,flight.Rank);
        int frames=FreezeFrames(values);var mods=flight.Modifiers with{Potion=CombatPotions.BonusAt(owner,"magical-penetration",flight.NextUtc)};
        var packet=DruidMoltenBoulder.Potion(mods,new(0,0,raw/256.0,0,0));
        PlayerRecipients(owner,center,Radius(values),target=>
        {
            owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,packet);
            FreezeStates.Apply(owner,target,frames,TimedEffects.TickAt(flight.NextUtc));
            int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
            if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,owner,loss);}CombatDamage.Finish(target,owner);
        });
    }
    private static void PlayerRecipients(PlayerMobile owner,Point3D center,double radius,Action<Mobile> impact)
    {
        foreach(var target in SkillCombat.Targets(owner,center,radius))
        {
            if(target is not{Alive:true,Deleted:false}||ClassAvoidance.Missile(target)){continue;}
            impact(target);
        }
        Effects.SendLocationEffect(center,owner.Map,0x36E4,8,10,0x480,0); // Identified explosion placeholder.
    }
}
