// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Shared AI67 owner/target services. Melee retains its native scan adapter;
// ranged callers explicitly supply their distinct primary query.
internal static class NecromancerPetTargets
{
    private static Mobile Find(int id)=>World.FindMobile((Serial)(uint)id);
    internal static Mobile Primary(BaseCreature body,uint previous,Func<Mobile,bool> eligible,Func<Mobile,int> distance)
    {
        var prior=World.FindMobile((Serial)previous);
        if(eligible(prior)&&distance(prior)<=24){return prior;}
        Mobile found=null;int nearest=25;
        foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(body.Location,14))
        {
            if(!eligible(candidate)){continue;}int value=distance(candidate);
            if(value<nearest||value==nearest&&found!=null&&candidate.Serial.Value<found.Serial.Value){found=candidate;nearest=value;}
        }
        return found;
    }
    internal static NecromancerPetAiServices Bind(BaseCreature body,PlayerMobile owner,Func<uint> previous,
        Func<Mobile,bool> eligible,Func<Mobile,int> distance,Func<Mobile,bool> melee,
        Action<NecromancerPetAiRequest> request,Func<int> percentage,
        Func<int,bool,int,int,bool> commonMove,Func<int,bool,int,int,bool> move)
    {
        return Queries(body,owner,eligible,distance,(ranged,_)=>
        {
            if(ranged){throw new NotSupportedException("Melee Necromancer pet has no ranged skill binding");}
            var target=Primary(body,previous(),eligible,distance);
            return new(target==null?0:(int)target.Serial.Value,target==null?0:distance(target),target!=null&&melee(target));
        },request,percentage,commonMove,move);
    }
    internal static NecromancerPetAiServices BindRanged(BaseCreature body,PlayerMobile owner,
        Func<Mobile,bool> eligible,Func<Mobile,int> distance,Func<int,NecromancerPetAiTarget> primary,
        Action<NecromancerPetAiRequest> request,Func<int> percentage,
        Func<int,bool,int,int,bool> commonMove,Func<int,bool,int,int,bool> move)
    {
        ArgumentNullException.ThrowIfNull(primary);
        return Queries(body,owner,eligible,distance,(ranged,search)=>ranged?primary(search)
            :throw new NotSupportedException("Ranged Necromancer pet has no melee primary binding"),request,percentage,commonMove,move);
    }
    private static NecromancerPetAiServices Queries(BaseCreature body,PlayerMobile owner,
        Func<Mobile,bool> eligible,Func<Mobile,int> distance,Func<bool,int,NecromancerPetAiTarget> primary,
        Action<NecromancerPetAiRequest> request,Func<int> percentage,
        Func<int,bool,int,int,bool> commonMove,Func<int,bool,int,int,bool> move)
    {
        return new(request,()=>(int)owner.Serial.Value,id=>Find(id) is PlayerMobile player?(int)player.Serial.Value:0,
            id=>Find(id) is{} target?distance(target):throw new NotSupportedException("Missing Necromancer pet distance target"),
            _=>
            {
                Mobile target=owner.Combatant;if(!eligible(target)){target=World.FindMobile(ClassCombat.For(owner).Target);}
                return new(eligible(target)?(int)target.Serial.Value:0,0,false);
            },
            primary,
            id=>!eligible(Find(id)),id=>Find(id) is not{} target||!body.InLOS(target),id=>eligible(Find(id)),
            percentage,commonMove,()=>SpellHelper.IsTown(body.Location,owner),move);
    }
}
