// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record MageDeadEmitter(uint Actor,uint Owner,int Map,DateTime EnteredUtc);

// Keep the original dead emitter, as the existing Fire Golem retirement does.
// State belongs to the existing Mage missile JSON profile and40ms controller;
// each released controller retains its own expiry. No replacement actor/timer.
internal static class MageProjectileRetirement
{
    private static Dictionary<uint,MageDeadEmitter> Rows=new();
    private static readonly HashSet<uint> Finalizing=[];
    internal static bool Retiring(NecromancerMinion actor)=>actor is{IsMage:true}&&Rows.ContainsKey(actor.Serial.Value);
    internal static bool DefersDelete(NecromancerMinion actor)=>Retiring(actor)&&!Finalizing.Contains(actor.Serial.Value);
    internal static PlayerMobile Owner(NecromancerMinion actor)=>actor?.ControlMaster as PlayerMobile
        ??(actor!=null&&Rows.TryGetValue(actor.Serial.Value,out var row)?World.FindMobile((Serial)row.Owner) as PlayerMobile:null);
    private static bool Pending(uint actor)=>NecromancerMageMissiles.Pending(actor)||ChainLightning.PendingMage(actor)||FireGround.PendingMage(actor);
    internal static bool Begin(NecromancerMinion actor)
    {
        if(actor is not{IsMage:true,Deleted:false,ControlMaster:PlayerMobile owner}||actor.Map==null||actor.Map==Map.Internal||!Pending(actor.Serial.Value)){return false;}
        Rows[actor.Serial.Value]=new(actor.Serial.Value,owner.Serial.Value,actor.Map.MapID,TimedEffects.Now);
        actor.RetireNativeMageActions();actor.Frozen=true;SkillHealth.SetBalance256(actor,0);return true;
    }
    internal static bool Present(NecromancerMinion actor)=>actor is{Deleted:false,IsMage:true}&&(actor.Alive||Retiring(actor));
    internal static bool Fighting(NecromancerMinion actor,PlayerMobile owner,int map,bool? actions)
    {
        if(!Retiring(actor)){return NecromancerMageMissiles.Fighting(actor,owner,map,actions);}
        return actor is{Deleted:false,IsMage:true}&&Rows[actor.Serial.Value] is{} row&&owner is{Alive:true,Deleted:false}&&row.Owner==owner.Serial.Value&&row.Map==map
            &&actor.ControlMaster==owner&&actor.Map?.MapID==map&&owner.Map==actor.Map&&actor.Permitted(owner)
            &&!MinionSafeAreas.Contains(owner)&&!SpellHelper.IsTown(owner.Location,owner)&&!SpellHelper.IsTown(actor.Location,owner)
            &&(actions??owner.NetState is{Running:true});
    }
    internal static bool CanAffect(NecromancerMinion actor,Mobile target)=>Retiring(actor)
        ?Owner(actor)is{Alive:true,Deleted:false} owner&&owner.Map==actor.Map&&ClassCombat.IsHostile(owner,target)&&owner.CanBeHarmful(target,false)
        :actor?.CanBeHarmful(target,false)==true;
    internal static void Forget(NecromancerMinion actor){Rows.Remove(actor.Serial.Value);Finalizing.Remove(actor.Serial.Value);}
    internal static void Cleanup()
    {
        foreach(uint id in Rows.Keys.ToArray())
        {
            if(World.FindMobile((Serial)id)is not NecromancerMinion{IsMage:true} actor||actor.Deleted){Rows.Remove(id);continue;}
            if(Pending(id)){continue;}Finalizing.Add(id);
            try{actor.Delete();}finally{Rows.Remove(id);Finalizing.Remove(id);}
        }
    }
    internal static MageDeadEmitter[] Snapshot()=>Rows.Count==0?null:Rows.Values.OrderBy(r=>r.Actor).ToArray();
    internal static MageDeadEmitter[] Validate(MageDeadEmitter[] rows)
    {
        rows??=[];var ids=new HashSet<uint>();
        foreach(var r in rows)
        {if(r==null||!((Serial)r.Actor).IsMobile||!((Serial)r.Owner).IsMobile||r.Actor==r.Owner||!ids.Add(r.Actor)
            ||r.Map<0||r.Map>=Map.Maps.Length||Map.Maps[r.Map]==null||Map.Maps[r.Map]==Map.Internal||r.EnteredUtc.Kind!=DateTimeKind.Utc||r.EnteredUtc==DateTime.MinValue)
            {throw new InvalidDataException("Invalid retired Mage emitter");}}
        return rows;
    }
    internal static void Restore(MageDeadEmitter[] rows)=>Rows=rows.ToDictionary(r=>r.Actor);
    internal static void Loaded()
    {
        foreach(var r in Rows.Values)
        {if(World.FindMobile((Serial)r.Actor)is NecromancerMinion{IsMage:true,Deleted:false} actor){actor.RetireNativeMageActions();actor.Frozen=true;SkillHealth.SetBalance256(actor,0);}}
        Cleanup();
    }
}

public partial class NecromancerMinion
{
    public override bool Alive=>!MageProjectileRetirement.Retiring(this)&&base.Alive;
    public override void OnDeath(Container corpse)
    {if(IsMage){MageProjectileRetirement.Begin(this);}base.OnDeath(corpse);}
    public override void Delete()
    {if(!MageProjectileRetirement.DefersDelete(this)){base.Delete();}}
}
