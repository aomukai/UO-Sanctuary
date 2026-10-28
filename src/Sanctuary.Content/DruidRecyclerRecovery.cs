// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Mandatory selection keeps Solar's old-engine/current-table conflict visible.
// Neither profile enables a learned talent. LegacySource is a differential
// control; CurrentTableSolarProvisional explicitly reads current NPC325calc2.
internal enum RecyclerRecoveryModel{LegacySource,CurrentTableSolarProvisional}
internal readonly record struct RecyclerGain(bool Changed,int After256);
internal sealed record RecyclerRecovery(long Id,uint Owner,uint Actor,CombatAbility Ability,int Rank,int Remaining,
    DateTime NextUtc,RecyclerRecoveryModel Model,bool Triggered=false);
internal sealed record RecyclerRecoverySave(RecyclerRecovery[] Effects,long NextId);
internal readonly record struct RecyclerCommit(bool Reserved,bool Allocated,long Effect);

public sealed class RecyclerRecoveryProfiles:GenericPersistence
{
    private RecyclerRecoverySave _snapshot;
    public RecyclerRecoveryProfiles():base("SanctuaryRecyclerRecovery",180){}
    public override void Serialize(){_snapshot=DruidRecyclerRecovery.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown recycler recovery version");}DruidRecyclerRecovery.Restore(JsonSerializer.Deserialize<RecyclerRecoverySave>(reader.ReadString()));}
}

// Original-player-owned delay survives NPC removal. First callback is on the
// next native40ms tick, a declared phase adapter rather than a D2R queue trace.
public static class DruidRecyclerRecovery
{
    private static ImmutableDictionary<long,RecyclerRecovery> _effects=ImmutableDictionary<long,RecyclerRecovery>.Empty;
    private static HashSet<long> _loaded=new();private static long _nextId;
    internal static Func<RecyclerRecovery,bool> Allocate=_=>true;
    public static RecyclerRecoveryProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static string Missile(CombatAbility ability)=>ability switch
    {CombatAbility.CarrionVine=>"recycler delay",CombatAbility.SolarCreeper=>"vine recycler delay",_=>throw new NotSupportedException("Not a recycler vine")};
    private static void Route(CombatAbility ability,RecyclerRecoveryModel model)
    {
        string key=Missile(ability);int callback=ability==CombatAbility.CarrionVine?29:33;
        if(!Enum.IsDefined(model)||NecromancerSummons.Source(key,"pSrvDoFunc")!=callback
            ||NecromancerSummons.Source(key,"Range")!=47||NecromancerSummons.Source(key,"Param1")!=45)
        {throw new NotSupportedException("Unsupported recycler recovery route");}
    }
    internal static RecyclerGain Gain(CombatAbility ability,int rank,int age,int current256,int maximum256,
        bool hasOwner,bool alive,RecyclerRecoveryModel model)
    {
        Route(ability,model);
        if(rank is <1 or >255||current256<0||maximum256<0){throw new ArgumentOutOfRangeException();}
        if(!hasOwner||!alive||age!=45||current256/256>=maximum256/256){return new(false,current256);}
        var values=DruidNpcSkills.Recycler(ability,rank);
        string formula=ability==CombatAbility.SolarCreeper&&model==RecyclerRecoveryModel.CurrentTableSolarProvisional?"calc2":"calc1";
        int whole=current256/256,max=maximum256/256;
        int after=checked(Math.Min(max,checked(whole+SourcePercentage.Calculate(max,checked((int)values.Eval(formula)),100)))*256);
        return new(true,after);
    }
    internal static RecyclerCommit Commit(DruidRecyclerBody actor,Corpse corpse,DateTime at,RecyclerRecoveryModel model)
    {
        if(actor is not{Alive:true,Deleted:false,Prepared:{} state}||actor.ControlMaster is not PlayerMobile owner
            ||state.Owner!=owner.Serial.Value||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-2)) {return default;}
        var birth=state.Snapshot.Birth;Route(birth.Ability,model);
        if(!CorpseSkillRules.UsableAt(owner,corpse,actor,double.PositiveInfinity)||!CorpseSkillRules.Reserve(corpse)){return default;}
        long id=checked(_nextId+1);var effect=new RecyclerRecovery(id,owner.Serial.Value,actor.Serial.Value,birth.Ability,birth.RecyclerRank,47,at.AddMilliseconds(40),model);
        // Source start63 reserves first and does not undo that reservation if
        // allocation fails. Keep the loot container intact; it is not do137's
        // immediate Dire Wolf consume transaction.
        if(!Allocate(effect)){return new(true,false,0);}
        Validate(effect,id);_effects=_effects.Add(id,effect);_nextId=id;
        return new(true,true,id);
    }
    internal static RecyclerRecoverySave Snapshot()=>new(_effects.Values.OrderBy(x=>x.Id).ToArray(),_nextId);
    internal static RecyclerRecovery Get(long id)=>_effects.GetValueOrDefault(id);
    internal static void Cancel(Mobile owner)
    {if(owner!=null){foreach(long id in _effects.Values.Where(x=>x.Owner==owner.Serial.Value).Select(x=>x.Id).ToArray()){Remove(id);}}}
    private static void Remove(long id){_effects=_effects.Remove(id);_loaded.Remove(id);}
    internal static void Tick(DateTime at,bool? actions=null,uint? ownerScope=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Recycler recovery clock must be UTC");}
        foreach(var initial in _effects.Values.Where(x=>x.NextUtc<=at&&(!ownerScope.HasValue||x.Owner==ownerScope.Value)).OrderBy(x=>x.Id).ToArray())
        {
            if(!_effects.TryGetValue(initial.Id,out var effect)){continue;}
            var owner=World.FindMobile((Serial)effect.Owner) as PlayerMobile;
            bool active=owner is{Alive:true,Deleted:false}&&(actions??owner.NetState is{Running:true});
            if(_loaded.Remove(effect.Id)||!active)
            {
                long skip=Math.Min(effect.Remaining,(at-effect.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1);
                effect=effect with{Remaining=effect.Remaining-(int)skip,NextUtc=effect.NextUtc.AddTicks(skip*TimeSpan.FromMilliseconds(40).Ticks)};
                if(effect.Remaining==0){Remove(effect.Id);}else{_effects=_effects.SetItem(effect.Id,effect);}continue;
            }
            while(effect.NextUtc<=at)
            {
                int age=47-effect.Remaining;
                if(age==45&&!effect.Triggered)
                {
                    effect=effect with{Triggered=true};_effects=_effects.SetItem(effect.Id,effect);
                    bool mana=effect.Ability==CombatAbility.SolarCreeper;
                    var gain=Gain(effect.Ability,effect.Rank,age,mana?NecromancerEffects.ManaBalance256(owner):SkillHealth.Balance256(owner),
                        mana?owner.ManaMax*256:SkillHealth.Maximum256(owner),true,owner.Alive,effect.Model);
                    if(gain.Changed)
                    {
                        if(mana){NecromancerEffects.SetMana256(owner,gain.After256);}else{SkillHealth.SetBalance256(owner,gain.After256);}
                        owner.FixedEffect(0x376A,10,12); // Identified steal-life/mana overlay placeholder.
                    }
                    if(!_effects.TryGetValue(effect.Id,out var live)||!ReferenceEquals(live,effect)){break;}
                }
                effect=effect with{Remaining=effect.Remaining-1,NextUtc=effect.NextUtc.AddMilliseconds(40)};
                if(effect.Remaining==0){Remove(effect.Id);break;}
                _effects=_effects.SetItem(effect.Id,effect);
            }
        }
    }
    private static void Validate(RecyclerRecovery s,long next)
    {
        if(s==null||s.Id<=0||s.Id>next||!((Serial)s.Owner).IsMobile||!((Serial)s.Actor).IsMobile
            ||s.Rank is <1 or >255||s.Remaining is <1 or >47||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc>DateTime.MaxValue.AddSeconds(-2)
            ||s.Triggered&&s.Remaining>2){throw new InvalidDataException("Invalid recycler recovery snapshot");}
        try{Route(s.Ability,s.Model);}catch(Exception e)when(e is ArgumentException or NotSupportedException){throw new InvalidDataException("Unsupported recovery profile",e);}
    }
    internal static void Restore(RecyclerRecoverySave saved)
    {
        if(saved?.Effects==null||saved.NextId<0||saved.Effects.Length>100000){throw new InvalidDataException("Missing recycler recovery state");}
        var next=ImmutableDictionary<long,RecyclerRecovery>.Empty;
        foreach(var effect in saved.Effects){Validate(effect,saved.NextId);if(next.ContainsKey(effect.Id)){throw new InvalidDataException("Duplicate recovery identity");}next=next.Add(effect.Id,effect);}
        _effects=next;_nextId=saved.NextId;_loaded=new(next.Keys);
    }
}
