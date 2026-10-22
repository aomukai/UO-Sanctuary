// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianTaunt
{
    internal static bool Candidate(PlayerMobile owner,Mobile target)=>target is BaseCreature creature
        && SourceTerror.Supported(creature) && NecromancerEffects.AiCurse(creature)
        && ClassCombat.IsHostile(owner,target) && target.Map==owner.Map && owner.InLOS(target)
        && Server.Spells.SpellHelper.CheckTown(target.Location,owner);
    internal static Mobile Target(PlayerMobile owner,Serial selected)
    {
        var explicitTarget=World.FindMobile(selected);
        if(explicitTarget is BaseCreature creature && PaladinConversion.SourceRule(creature)==null)
        {owner.SendMessage("Taunt is not supported for this unmapped creature yet.");return null;}
        if(Candidate(owner,explicitTarget)){return explicitTarget;}
        Mobile best=null;long distance=long.MaxValue;bool tied=false;
        int radius=(int)SkillGeometry.Tiles(20);
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,radius))
        {
            if(!Candidate(owner,target)){continue;}
            long dx=(target.X-owner.X)*2L,dy=(target.Y-owner.Y)*2L,squared=dx*dx+dy*dy;
            if(squared>400){continue;}
            if(squared<distance){best=target;distance=squared;tied=false;}
            else if(squared==distance){tied=true;}
        }
        // Source equal-distance choice retains room scan order, which the native
        // spatial index does not reproduce. Keep this branch explicitly gated.
        if(tied){owner.SendMessage("Taunt's equally near automatic targets are not supported yet. Select a creature directly.");return null;}
        return best;
    }
    private static bool SupportedState(PlayerMobile owner,Mobile target)
    {
        if(NecromancerEffects.Curse(target)?.Ability==CombatAbility.Attract)
        {owner.SendMessage("Taunt's failed-curse AI transition on Attract is not supported yet.");return false;}
        if(NamedControl.Fear(target)){owner.SendMessage("Taunt's item-fear transition is not supported yet.");return false;}
        return true;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.Taunt,selected,point,now,
        (p,_,id,_)=>BarbarianShout.ValidCast(p) && Target(p,id) is{} target && SupportedState(p,target));
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.Taunt) || !BarbarianShout.ValidCast(owner)
            || Target(owner,selected) is not BaseCreature target || !SupportedState(owner,target)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Taunt);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        Apply(owner,target,values.Rank,TimedEffects.Now);NecromancerEffects.Pay(owner,values.Mana256);
        owner.PlaySound(0x1E5);target.FixedEffect(0x375A,10,15,0x481,0); // Identified UO sound/taunt overlay placeholders.
        owner.DoHarmful(target);return true;
    }
    internal static bool Apply(PlayerMobile owner,BaseCreature target,int rank,DateTime at)
    {
        if(!Candidate(owner,target) || rank is <1 or >48 || !SupportedState(owner,target)){return false;}
        var state=NecromancerEffects.Profiles.Find(target)??new();var old=NecromancerEffects.Curse(target);
        if(old is{Ability:CombatAbility.Taunt} && old.Rank>rank)
        {
            // Do71 ignores failed allocation: an already active Taunt keeps its
            // old stats/phase but its dynamic target and next AI event change.
            if(old.Terror is{Skill:CombatAbility.Taunt} active)
            {
                SourceTerror.Reset(target);
                NecromancerEffects.Profiles.Store(target,state with{Curse=old with{Terror=active with{Override=owner.Serial.Value,Focus=0,Combat=false,Goal=null,NextUtc=at.AddMilliseconds(40)}}});
                target.AIObject?.AITimer.Prod();
            }
            return false;
        }
        SourceTerror.Reset(target);
        var phase=new TerrorAi(CombatAbility.Taunt,rank,target.Map.MapID,false,owner.Serial.Value,0,at.AddMilliseconds(40));
        var curse=old is{Ability:CombatAbility.Taunt} && old.Rank==rank?old with{Terror=phase}
            :new NecromancerCurse(CombatAbility.Taunt,owner.Serial.Value,rank,DateTime.MaxValue,owner.X,owner.Y,Terror:phase);
        NecromancerEffects.Profiles.Store(target,state with{Curse=curse});target.AIObject?.AITimer.Prod();return true;
    }
}
