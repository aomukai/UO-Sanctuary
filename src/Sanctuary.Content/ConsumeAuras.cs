// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// One captured item-aura contribution per Consume state. This reuses selected
// aura delivery,but never occupies the selected-skill emitter or spends mana.
internal static class ConsumeAuras
{
    private sealed record Provider(long Order,CombatAbility Ability,int Rank,DateTime NextUtc);
    private static readonly Dictionary<uint,Provider> Providers=new();
    internal static void Forget(uint owner)=>Providers.Remove(owner);
    internal static CombatAbility Ability(string skill)=>skill switch
    {"Holy Fire"=>CombatAbility.HolyFire,"Meditation"=>CombatAbility.Meditation,"Salvation"=>CombatAbility.Salvation,_=>CombatAbility.None};
    internal static SkillValues Values(PlayerMobile owner,ConsumeEffect state)
    {
        var row=SkillMechanics.For(Ability(state.Suppliers.Bonus.AuraSkill));int rank=state.Suppliers.Bonus.AuraRank;
        // Item-aura rank is fixed. A Warlock has no hard Paladin synergies;
        // general +skills must not raise this contribution or grant Prayer.
        return new(row,key=>key==row.SourceKey?rank:0,_=>0,ClassCreation.Profiles.TryGet(owner.Serial,out var profile)?profile.Level:1);
    }
    internal static void Stop(PlayerMobile owner,long order)
    {Providers.Remove(owner.Serial.Value);PaladinAuras.ClearProvidedSelf(owner,order);}
    internal static void Changed(PlayerMobile owner,ConsumeEffect previous,ConsumeEffect next)
    {
        if(previous?.Order==next?.Order&&previous?.UntilUtc==next?.UntilUtc&&previous?.Suppliers.Bonus.AuraSkill==next?.Suppliers.Bonus.AuraSkill
            &&previous?.Suppliers.Bonus.AuraRank==next?.Suppliers.Bonus.AuraRank){return;}
        if(previous!=null){Stop(owner,previous.Order);}Tick(owner,next,TimedEffects.Now);
    }
    internal static void Tick(PlayerMobile owner,ConsumeEffect state,DateTime now)
    {
        if(now.Kind!=DateTimeKind.Utc){throw new ArgumentException("Consume aura requires UTC");}
        if(state==null||state.Suppliers.Bonus.AuraRank==0||state.UntilUtc<=now||owner is not{Alive:true,Deleted:false,NetState:{Running:true}}
            ||owner.Map==null||owner.Map==Map.Internal||ConsumeEffects.State(owner)!=state)
        {if(Providers.Remove(owner.Serial.Value,out var old)){PaladinAuras.ClearProvidedSelf(owner,old.Order);}else if(state!=null){PaladinAuras.ClearProvidedSelf(owner,state.Order);}return;}
        var ability=Ability(state.Suppliers.Bonus.AuraSkill);var values=Values(owner,state);int frames=Math.Max(5,checked((int)values.Eval("perdelay")));
        if(!Providers.TryGetValue(owner.Serial.Value,out var p)||p.Order!=state.Order||p.Ability!=ability||p.Rank!=values.Rank)
        {
            var first=SkillMechanics.For(ability).Number("immediate")!=0?state.AppliedUtc:state.AppliedUtc.AddMilliseconds(frames*40L);
            p=new(state.Order,ability,values.Rank,first>now?first:now);Providers[owner.Serial.Value]=p;
        }
        if(p.NextUtc>now){return;}
        PaladinAuras.PulseProvided(owner,ability,values,now,state.UntilUtc,state.Order);
        if(owner is{Alive:true,Deleted:false}&&ConsumeEffects.State(owner)==state){Providers[owner.Serial.Value]=p with{NextUtc=now.AddMilliseconds(frames*40L)};}
        else{Stop(owner,state.Order);}
    }
}
