// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BoundDemonAuraGrant(EliteAura Rolled,int SourceLevel,CombatAbility Ability,int Rank,DateTime BornUtc);

// Prepared native mapping for modifier30. Choice/level are birth snapshots;
// the exact current D2R producer/seed remains part of the full Bind proposal.
internal static class BoundDemonAuras
{
    private sealed record Provider(long Order,BoundDemonAuraGrant Grant,DateTime NextUtc);
    private static readonly Dictionary<uint,Provider> Providers=new();
    internal static Func<int,EliteAura> Choose=level=>EncounterProfiles.RollAura(level,Random.Shared);
    internal static CombatAbility Replacement(EliteAura aura)=>aura switch
    {
        EliteAura.Might=>CombatAbility.Concentration,EliteAura.HolyFire or EliteAura.HolyShock=>CombatAbility.Vigor,
        EliteAura.BlessedAim=>CombatAbility.Thorns,EliteAura.HolyFreeze=>CombatAbility.HolyFreeze,
        EliteAura.Conviction or EliteAura.Fanaticism=>CombatAbility.Fanaticism,
        _=>throw new NotSupportedException("No qualified bound aura replacement")
    };
    internal static BoundDemonAuraGrant CapturePrepared(BaseCreature body,BindDemonSuppliers birth,DateTime at)
    {
        if(birth.Modifier20==0){return null;}
        if(birth.Modifier20!=30||body is not{Alive:true,Deleted:false}||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Unqualified bound aura capture");}
        int level=CreatureProfiles.Level(body);var rolled=Choose(level);
        var result=new BoundDemonAuraGrant(rolled,level,Replacement(rolled),Math.Clamp(EliteAuras.SkillLevel(rolled,level),1,99),at);
        Validate(result,birth);return result;
    }
    internal static void Validate(BoundDemonAuraGrant grant,BindDemonSuppliers birth)
    {
        // Historical prepared hosts have no recorded choice. Never roll one
        // while loading them;missing data remains an explicit legacy boundary.
        if(grant==null){return;}
        if(birth.Modifier20!=30||grant.SourceLevel is<1 or>255||grant.BornUtc.Kind!=DateTimeKind.Utc||grant.BornUtc<=DateTime.UnixEpoch
            ||grant.Rank is<1 or>99||grant.Rolled==EliteAura.HolyShock&&grant.SourceLevel<20)
        {throw new InvalidDataException("Invalid captured bound aura context");}
        try
        {
            if(grant.Ability!=Replacement(grant.Rolled)||grant.Rank!=Math.Clamp(EliteAuras.SkillLevel(grant.Rolled,grant.SourceLevel),1,99))
            {throw new InvalidDataException("Bound aura differs from captured native mapping");}
        }
        catch(NotSupportedException e){throw new InvalidDataException("Unknown captured bound aura",e);}
    }
    internal static SkillValues Values(BoundDemonAuraGrant grant,BaseCreature body=null)
    {
        var row=SkillMechanics.For(grant.Ability);int rank=body==null?grant.Rank:InnateNpcSkills.Level(grant.Rank,BattleCommand.Bonus(body));
        return new(row,key=>key==row.SourceKey?rank:0,_=>0,grant.SourceLevel);
    }
    internal static bool Qualified(BaseCreature body,PlayerMobile owner,SkillValues values)=>body!=null&&owner!=null&&values!=null
        &&BoundDemonHosts.State(body)is{Aura:{}} state&&state.Owner==owner.Serial.Value&&BoundDemonHosts.CanAct(body)
        &&state.Aura.Ability==values.Ability&&InnateNpcSkills.Level(state.Aura.Rank,BattleCommand.Bonus(body))==values.Rank;
    internal static void Forget(uint actor)=>Providers.Remove(actor);
    internal static void Stop(BaseCreature body,long order)
    {if(Providers.TryGetValue(body.Serial.Value,out var p)&&p.Order==order){Providers.Remove(body.Serial.Value);}PaladinAuras.ClearProvidedSelf(body,order);}
    internal static void Tick(BaseCreature body,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Bound aura requires UTC");}
        var state=BoundDemonHosts.State(body);
        if(state?.Aura==null||!BoundDemonHosts.CanAct(body))
        {if(state!=null){Stop(body,state.Order);}else if(Providers.Remove(body.Serial.Value,out var old)){PaladinAuras.ClearProvidedSelf(body,old.Order);}return;}
        var grant=state.Aura;var values=Values(grant,body);int frames=Math.Max(5,checked((int)values.Eval("perdelay")));
        if(!Providers.TryGetValue(body.Serial.Value,out var p)||p.Order!=state.Order||p.Grant!=grant)
        {
            var first=SkillMechanics.For(grant.Ability).Number("immediate")!=0?grant.BornUtc:grant.BornUtc.AddMilliseconds(frames*40L);
            p=new(state.Order,grant,first>at?first:at);Providers[body.Serial.Value]=p;
        }
        if(p.NextUtc>at){return;}
        PaladinAuras.PulseBoundNpc(body,(PlayerMobile)body.ControlMaster,values,at,state.Order);
        if(BoundDemonHosts.State(body)==state&&BoundDemonHosts.CanAct(body)){Providers[body.Serial.Value]=p with{NextUtc=at.AddMilliseconds(frames*40L)};}
        else{Stop(body,state.Order);}
    }
}
