// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;

namespace Sanctuary;

public sealed record ImpaleSlowState(uint Target,uint Source,int Rank,int Velocity,int Attack,int Other,DateTime UntilUtc);
public sealed class ImpaleSlowProfiles:GenericPersistence
{
    public ImpaleSlowProfiles():base("SanctuaryImpaleSlow",139){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(ImpaleSlow.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Impale slow version");}
        ImpaleSlow.Restore(JsonSerializer.Deserialize<ImpaleSlowState[]>(reader.ReadString()));
    }
}
public static class ImpaleSlow
{
    private static readonly Dictionary<uint,ImpaleSlowState> _states=new();
    private static readonly List<uint> _expired=new();
    private static readonly Dictionary<uint,DateTime> _visualRefresh=new();
    public static ImpaleSlowProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Prune);
    public static ImpaleSlowState Effect(Mobile target)=>target is{Alive:true,Deleted:false} && _states.TryGetValue(target.Serial.Value,out var state)
        && state.UntilUtc>TimedEffects.Now?state:null;
    public static int Velocity(Mobile target)=>Effect(target)?.Velocity??0;
    public static int Attack(Mobile target)=>Effect(target)?.Attack??0;
    // Stored independently; native nonattack animation mode adapters remain
    // explicit acceptance work, as for Holy Freeze's other_animrate field.
    public static int Other(Mobile target)=>Effect(target)?.Other??0;
    public static void Apply(Mobile source,Mobile target,SkillValues values)
    {
        if(source==null || target is not{Alive:true,Deleted:false}){return;}
        int Field(string field)=>(int)Math.Clamp(-values.Eval(field),0,100);
        // Generic do2 reuses/replaces the same target stat list without rank
        // priority. This state is independent of ordinary curses and cold.
        var now=TimedEffects.Now;
        var state=new ImpaleSlowState(target.Serial.Value,source.Serial.Value,values.Rank,Field("aurastatcalc1"),Field("aurastatcalc2"),Field("aurastatcalc3"),
            now.AddMilliseconds(Math.Max(1,values.Eval("auralencalc"))*40));
        _states[target.Serial.Value]=state;
        RefreshVisual(target,state.UntilUtc,now);
    }
    private static void RefreshVisual(Mobile target,DateTime expiry,DateTime now)
    {
        if(target.Map is not {} map || map==Map.Internal || _visualRefresh.GetValueOrDefault(target.Serial.Value)>now){return;}
        double remaining=(expiry-now).TotalMilliseconds;
        int duration=(int)Math.Ceiling(Math.Min(remaining,650)/50);
        target.FixedEffect(SpellEffectGraphics.ImpaleSlow,10,duration);
        _visualRefresh[target.Serial.Value]=now.AddMilliseconds(Math.Min(remaining,450));
    }
    private static void ClearVisual(uint id,Mobile target)
    {
        if(_visualRefresh.Remove(id) && target is {Deleted:false,Map:{} map} && map!=Map.Internal)
        {target.FixedEffect(SpellEffectGraphics.ImpaleSlow,10,0);}
    }
    public static void Clear(Mobile target)
    {
        if(target==null){return;}
        _states.Remove(target.Serial.Value);ClearVisual(target.Serial.Value,target);
    }
    private static void Prune()
    {
        var now=TimedEffects.Now;
        _expired.Clear();
        foreach(var (id,state) in _states)
        {
            if(state.UntilUtc<=now || World.FindMobile((Serial)id) is not{Alive:true,Deleted:false} target){_expired.Add(id);}
            else{RefreshVisual(target,state.UntilUtc,now);}
        }
        foreach(uint id in _expired){_states.Remove(id);ClearVisual(id,World.FindMobile((Serial)id));}
    }
    public static ImpaleSlowState[] Snapshot()=>new List<ImpaleSlowState>(_states.Values).ToArray();
    public static void Restore(ImpaleSlowState[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Impale slows");}var restored=new Dictionary<uint,ImpaleSlowState>();
        foreach(var state in saved)
        {
            if(state.Target==0 || state.Source==0 || state.Rank is <1 or >255 || state.Velocity is <0 or >100 || state.Attack is <0 or >100 || state.Other is <0 or >100
                || state.UntilUtc.Kind!=DateTimeKind.Utc || !restored.TryAdd(state.Target,state)){throw new InvalidDataException("Invalid Impale slow");}
        }
        foreach(uint id in _states.Keys){ClearVisual(id,World.FindMobile((Serial)id));}
        _states.Clear();foreach(var pair in restored){_states.Add(pair.Key,pair.Value);}
    }
}
