// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record MartialCharge(uint Owner,CombatAbility Ability,int Count,int InitialRank,int AttackPercent,DateTime UntilUtc);
public sealed class MartialChargeProfiles:GenericPersistence
{
    private MartialCharge[] _snapshot;
    public MartialChargeProfiles():base("SanctuaryMartialCharges",159){}
    public override void Serialize(){_snapshot=AssassinCharges.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown martial charge version");}AssassinCharges.Restore(JsonSerializer.Deserialize<MartialCharge[]>(reader.ReadString()));}
    public override void PostDeserialize()=>AssassinCharges.Prune();
}
public static class AssassinCharges
{
    internal static bool Supports(CombatAbility ability)=>TigerStrikes.Handles(ability) || ability==CombatAbility.FistsOfFire;
    // Separate slots allow later charge families to share this lifecycle without
    // granting Tiger's physical bonus to their charge-building attacks.
    private static readonly Dictionary<(uint,CombatAbility),MartialCharge> States=new();
    public static MartialChargeProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Prune);
    internal static bool Owner(Mobile owner)=>owner is{Alive:true,Deleted:false}
        && ClassCreation.Profiles?.TryGet(owner.Serial,out var p)==true && p.Class==DiabloClass.Assassin;
    public static MartialCharge State(Mobile owner,CombatAbility ability=CombatAbility.TigerStrike)
        =>Owner(owner) && States.TryGetValue((owner.Serial.Value,ability),out var state) && state.UntilUtc>TimedEffects.Now?state:null;
    internal static MartialCharge Build(MartialCharge old,uint owner,SkillValues values,DateTime at)
    {
        if(!Supports(values.Ability) || values.Rank is <1 or >255){throw new NotSupportedException("Unmapped martial charge builder");}
        if(old!=null && (old.Owner!=owner || old.Ability!=values.Ability)){throw new InvalidDataException("Charge slot mismatch");}
        if(old?.UntilUtc<=at){old=null;}
        int count=old?.Count??0,added=count<3?(int)values.Eval("aurastatcalc2"):0;
        return new(owner,values.Ability,Math.Min(3,count+1),old?.InitialRank??values.Rank,(old?.AttackPercent??0)+added,
            at.AddMilliseconds(checked(values.Eval("auralencalc")*40)));
    }
    internal static void Gain(PlayerMobile owner,SkillValues values)
    {
        if(!Owner(owner)){return;}
        var state=Build(State(owner,values.Ability),owner.Serial.Value,values,TimedEffects.Now);States[(state.Owner,state.Ability)]=state;
        // Identified generic marker; final tigerstrike1/cobrastrike1 charge1/2/3 orbiting art/audio pending.
        owner.FixedEffect(0x375A,5,5,values.Ability==CombatAbility.FistsOfFire?0x489:values.Ability==CombatAbility.CobraStrike?0x26:0x481,0);StatNetwork.Send(owner);
    }
    internal static int TigerPercent(MartialCharge state,int currentRank)
    {
        if(state==null || state.Count<=0){return 0;}int rank=Math.Max(state.InitialRank,currentRank);if(rank<=0){return 0;}
        var row=SkillMechanics.For(CombatAbility.TigerStrike);var values=new SkillValues(row,_=>rank,_=>0,1);
        return checked(state.Count*(int)values.Eval("calc1"));
    }
    internal static MartialCharge[] Finisher(Mobile owner)=>new[]{State(owner,CombatAbility.TigerStrike),State(owner,CombatAbility.CobraStrike),State(owner,CombatAbility.FistsOfFire)}.Where(x=>x!=null).ToArray();
    internal static bool Any(Mobile owner)=>Finisher(owner).Length!=0;
    internal static PlayerWeaponDrain CobraDrain(MartialCharge state,int currentRank)
    {
        if(state==null || state.Count is <1 or >3){return default;}int rank=Math.Max(state.InitialRank,currentRank);if(rank<=0){return default;}
        var row=SkillMechanics.For(CombatAbility.CobraStrike);int percent=checked(row.Parameter(1)+(rank-1)*row.Parameter(2));
        if(state.Count==3){percent=checked(percent*2);}return new(percent,state.Count>=2?percent:0);
    }
    internal static MartialCharge Consumed(MartialCharge state,int count)
    {
        if(state==null || count<=0){return state;}
        int remaining=Math.Max(0,state.Count-count);
        // Modern table consumes one on a successful finisher. Retained deadline
        // and proportional AR are an explicitly provisional modern adapter;
        // the old finisher removed the entire list and cannot prove this update.
        return remaining==0?null:state with{Count=remaining,AttackPercent=state.AttackPercent*remaining/state.Count};
    }
    internal static void Consume(PlayerMobile owner,MartialCharge captured,int count)
    {
        if(captured==null || State(owner,captured.Ability)!=captured){return;}
        var next=Consumed(captured,count);var key=(captured.Owner,captured.Ability);
        if(next==null){States.Remove(key);}else{States[key]=next;}StatNetwork.Send(owner);
    }
    public static void Clear(Mobile owner)
    {if(owner==null){return;}foreach(var key in States.Keys.Where(x=>x.Item1==owner.Serial.Value).ToArray()){States.Remove(key);}}
    internal static void Prune()
    {foreach(var pair in States.ToArray()){if(pair.Value.UntilUtc<=TimedEffects.Now || !Owner(World.FindMobile((Serial)pair.Value.Owner))){States.Remove(pair.Key);}}}
    public static MartialCharge[] Snapshot(){Prune();return States.Values.OrderBy(x=>x.Owner).ThenBy(x=>x.Ability).ToArray();}
    public static void Restore(MartialCharge[] saved)
    {
        if(saved==null || saved.Length>1000000){throw new InvalidDataException("Missing martial charges");}
        var restored=new Dictionary<(uint,CombatAbility),MartialCharge>();
        foreach(var s in saved)
        {
            if(s==null || s.Owner==0 || !((Serial)s.Owner).IsMobile || !Supports(s.Ability) || s.Count is <1 or >3
                || s.InitialRank is <1 or >255 || s.AttackPercent!=s.Count*SkillMechanics.For(s.Ability).Parameter(4)
                || !restored.TryAdd((s.Owner,s.Ability),s)){throw new InvalidDataException("Invalid martial charge state");}
        }
        States.Clear();foreach(var p in restored){States.Add(p.Key,p.Value);}
    }
}
