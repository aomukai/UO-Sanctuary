// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal readonly record struct ApocalypseImpactResult(bool Applied,int LifeLost,int Damage256);

// Prepared ordinary-NPC fire-component receiver. Current450090 applies its
// fire-resist state only around this hit,then unlinks it immediately. The
// packet already contains caller-captured damage/mastery;do not enhance twice.
internal static class ApocalypseImpact
{
    internal static int Reduction(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var s=SkillMechanics.For(CombatAbility.Apocalypse);var v=new SkillValues(s,k=>k==s.SourceKey?rank:0,_=>0,1);
        if(s.SourceId!=401||s.Number("srvdofunc")!=123||s.Text("auratargetstate")!="apocalypse"||s.Text("aurastat1")!="fireresist")
        {throw new NotSupportedException("Unqualified Apocalypse resistance binding");}
        return checked((int)v.Eval("aurastatcalc1"));
    }
    internal static ApocalypseImpactResult ReleasePrepared(PlayerMobile owner,BaseCreature target,int enhancedFire256,int rank,bool allocation=true,int? pierceOverride=null)
    {
        int delta=Reduction(rank);
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||target is not{Alive:true,Deleted:false}||!ClassCombat.IsHostile(owner,target)||enhancedFire256<0){return default;}
        if(!allocation){return default;} // Current callback refuses damage when its temporary state allocation fails.
        TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,target);
        // Base fire already includes its explosion-captured enhancement. Reuse
        // the existing potion additions and mitigate each combined element
        // once;only fire receives this skill's temporary resistance penalty.
        var raw=DruidMoltenBoulder.Potion(DruidFirestorm.Modifiers(owner),new GearDamage(0,enhancedFire256/256.0,0,0,0));
        var parts=SkillCombat.MitigatePrepared(owner,target,raw,out int absorbed,
            pierceOverride:element=>element=="fire"?pierceOverride:null,skillResistanceDelta:element=>element=="fire"?delta:0);
        int damage=NamedElements.To256(parts.Total);
        owner.DoHarmful(target,true);
        if(absorbed>0){SkillHealth.Heal(target,absorbed);}
        int lost=SkillHealth.Damage(owner,target,damage);
        if(!target.Deleted){target.FixedEffect(0x36D4,10,10);} // Identified Apocalypse recipient placeholder.
        return new(true,lost,damage);
    }
}
