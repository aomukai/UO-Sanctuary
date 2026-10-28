// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal abstract class MonsterUninterruptibleServices
{
    internal abstract void Uninterruptible(bool enabled);
    internal abstract bool HasGame();
    internal abstract bool DeathDelayed();
    internal abstract void ClearDeathDelay();
    internal abstract bool ResolveLastAttacker();
    internal abstract void ReturnFreshDeath();
    internal abstract void KillWithoutAttacker();
    internal abstract void DeleteQueuedThink();
}
internal static class MonsterUninterruptible
{
    // Complete NPC branch of sub_6FCBDE90. Mode completion supplies this
    // signal; neither a timeout nor a replay of the original damage is used.
    internal static void Change(bool enabled,MonsterUninterruptibleServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Uninterruptible(enabled);
        if(!services.HasGame()){return;}
        if(!enabled&&services.DeathDelayed())
        {
            services.ClearDeathDelay();
            if(services.ResolveLastAttacker()){services.ReturnFreshDeath();}
            else{services.KillWithoutAttacker();}
        }
        services.DeleteQueuedThink();
    }
}

public sealed record MonsterModeFlags(uint Actor,bool Uninterruptible,bool DeathDelay,uint LastAttacker);
public sealed class MonsterModeProfiles:GenericPersistence
{
    public MonsterModeProfiles():base("SanctuaryMonsterModeFlags",187){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(MonsterUninterruptibles.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown monster mode flags format");}
        MonsterUninterruptibles.Restore(JsonSerializer.Deserialize<MonsterModeFlags[]>(reader.ReadString()));
    }
}
// Real actor consumers must supply current rooms, rows, mode state and the
// correct think queue. No source scheduling or death-delay timeout is inferred.
internal abstract class MonsterModeCompletion
{
    internal abstract bool HasGame(BaseCreature actor);
    internal abstract MonsterReturnServices ReturnServices(BaseCreature actor,Mobile attacker);
    internal abstract void DeleteQueuedThink(BaseCreature actor);
    internal virtual bool Ubers=>true;
}
public static class MonsterUninterruptibles
{
    private static readonly Dictionary<uint,MonsterModeFlags> States=new();
    public static MonsterModeProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal static MonsterModeFlags State(BaseCreature actor)=>actor is{Deleted:false}?States.GetValueOrDefault(actor.Serial.Value):null;
    internal static bool Active(BaseCreature actor)=>State(actor)?.Uninterruptible==true;
    internal static bool Delayed(BaseCreature actor)=>State(actor)?.DeathDelay==true;
    private static MonsterModeFlags Existing(BaseCreature actor)
    {
        if(actor is not{Deleted:false}){throw new ArgumentException("A registered NPC mode actor is required");}
        return State(actor)??new(actor.Serial.Value,false,false,0);
    }
    private static void Put(MonsterModeFlags state)
    {
        if(!state.Uninterruptible&&!state.DeathDelay&&state.LastAttacker==0){States.Remove(state.Actor);}
        else{States[state.Actor]=state;}
    }
    internal static void RecordLastAttacker(BaseCreature actor,Mobile attacker)
        =>Put(Existing(actor) with{LastAttacker=attacker?.Serial.Value??0});
    internal static void MarkDeathDelay(BaseCreature actor)=>Put(Existing(actor) with{DeathDelay=true});
    internal static void Clear(BaseCreature actor){if(actor!=null){States.Remove(actor.Serial.Value);}}
    internal static void Change(BaseCreature actor,bool enabled,MonsterModeCompletion completion)
    {
        Existing(actor);ArgumentNullException.ThrowIfNull(completion);
        MonsterUninterruptible.Change(enabled,new Completion(actor,completion));
        if(actor.Deleted){Clear(actor);}
    }
    private sealed class Completion:MonsterUninterruptibleServices
    {
        private readonly BaseCreature Actor;private readonly MonsterModeCompletion Context;private Mobile Attacker;
        internal Completion(BaseCreature actor,MonsterModeCompletion context){Actor=actor;Context=context;}
        internal override void Uninterruptible(bool enabled)=>Put(Existing(Actor) with{Uninterruptible=enabled});
        internal override bool HasGame()=>Context.HasGame(Actor);
        internal override bool DeathDelayed()=>Delayed(Actor);
        internal override void ClearDeathDelay()=>Put(Existing(Actor) with{DeathDelay=false});
        internal override bool ResolveLastAttacker()
        {
            uint id=State(Actor)?.LastAttacker??0;Attacker=id==0?null:World.FindMobile((Serial)id);
            return Attacker is{Deleted:false};
        }
        internal override void ReturnFreshDeath()
        {
            // Fresh packet: no successful-hit bit,damage,stun,typed channels or
            // old scoped callback. The actual return policy rechecks its gates.
            var packet=new PreparedMonsterDamagePacket(0,0,0,0,2);
            PreparedMonsterReturn.Dispatch(ref packet,Context.ReturnServices(Actor,Attacker),Context.Ubers);
        }
        internal override void KillWithoutAttacker()
        {
            if(Actor is{Alive:true,Deleted:false}){Actor.LastKiller=null;Actor.Kill();}
        }
        internal override void DeleteQueuedThink()=>Context.DeleteQueuedThink(Actor);
    }
    // Serialization workers only read/filter;they do not mutate live state.
    public static MonsterModeFlags[] Snapshot()=>States.Values.Where(s=>World.FindMobile((Serial)s.Actor) is BaseCreature{Deleted:false}).OrderBy(s=>s.Actor).ToArray();
    public static void Restore(MonsterModeFlags[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing monster mode flags");}
        var states=new Dictionary<uint,MonsterModeFlags>();
        foreach(var s in saved)
        {
            if(s==null||!((Serial)s.Actor).IsMobile||(s.LastAttacker!=0&&!((Serial)s.LastAttacker).IsMobile)
                ||(!s.Uninterruptible&&!s.DeathDelay&&s.LastAttacker==0)||!states.TryAdd(s.Actor,s))
            {throw new InvalidDataException("Invalid or duplicate monster mode flags");}
        }
        States.Clear();foreach(var s in states){States.Add(s.Key,s.Value);}
    }
}
