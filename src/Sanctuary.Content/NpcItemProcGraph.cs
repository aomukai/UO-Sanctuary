// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record NpcProcItem(uint Item,int Body,ItemPropertyCreationState State);
internal sealed record NpcProcState(uint Actor,uint Owner,EquipmentStatValue[] Innate,NpcProcItem[] Items,UnitEventState Events);

// An event-stat subgraph, not a complete item factory or replacement for the
// existing numeric actor graph. Only admitted proc carriers can enter here.
internal sealed class NpcItemProcGraph
{
    private sealed record Carrier(Item Item,int Body,ItemPropertyCreationContext Graph);
    private sealed record Frame(SkeletonGuardian Actor,BaseCreature Other,PreparedMonsterDamagePacket Packet,DateTime At,Func<int,int> Roll,byte Phase,NpcItemTarget Retained);
    private readonly uint ActorId,OwnerId;
    private readonly EquipmentStatUnit Unit;
    private readonly EquipmentStatValues Values;
    private readonly List<Carrier> Items=[];
    private SourceUnitEvents Events;
    private ItemEventBindings Bindings;
    private Frame Current;
    private int Mutation;
    private bool Dirty;
    private Action<NpcItemProcGraph> Changed;
    internal uint Actor=>ActorId;
    internal bool IncomingRequired=>Unit.Stats.FullValues.Any(v=>v.Stat==201&&v.Value>0);
    private SkeletonGuardian Body=>World.FindMobile((Serial)ActorId) is SkeletonGuardian{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Deleted:false} p} b&&p.Serial.Value==OwnerId
        ?b:throw new NotSupportedException("NPC proc graph lost its original living actor/owner");
    private static readonly EquipmentPropertyBase EmptyBasis=new(0,0,0,0,0,0,0,0);
    private NpcItemProcGraph(uint actor,uint owner,Action<NpcItemProcGraph> changed)
    {
        ActorId=actor;OwnerId=owner;Changed=changed;
        Unit=new(actor,1);Unit.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=1,Owner=Unit};
        // Wounds/crushing query time-dependent operations even when their
        // operands are absent. This closed subgraph admits no such operands;
        // null skips that zero contribution without inventing a world clock.
        Values=new(u=>Items.FirstOrDefault(i=>ReferenceEquals(i.Graph.Unit,u))?.Body??0,
            _=>null,_=>throw new NotSupportedException("Proc graph has no class-stat service"),OnValue);
        Events=new(true,Invoke);Bindings=new(Events,ItemEventDefinitions.Bindings);
    }
    internal static NpcItemProcGraph Create(SkeletonGuardian actor,Action<NpcItemProcGraph> changed)
    {
        if(actor is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}||NpcEquipmentRandom.State(actor)==null)
        {throw new NotSupportedException("NPC procs need their original living owner and initialized source RNG");}
        return new(actor.Serial.Value,owner.Serial.Value,changed);
    }
    private void OnValue(EquipmentStatNode node,EquipmentStatUnit source,uint packed,int oldValue,int value)
    {
        if(node!=Unit.Stats){return;}
        Bindings.Changed(packed,value);Dirty=true;Publish();
    }
    private void Publish(){if(Dirty&&Mutation==0&&Current==null&&!Events.Dispatching){Dirty=false;Changed?.Invoke(this);}}
    private void Mutate(Action action,bool structural=false)
    {Mutation++;try{action();Dirty|=structural;}finally{Mutation--;Publish();}}
    private static void Validate(ItemPropertyCreationState state)
    {
        if(state is not{ItemBase.Length:0,Properties:not null}||state.Ethereal||!state.HasProperties&&state.Properties.Length!=0
            ||state.Properties.Select(v=>v.Packed).Distinct().Count()!=state.Properties.Length)
        {throw new NotSupportedException("Proc carrier needs its explicit event-only base;full item initialization remains separate");}
        foreach(var value in state.Properties)
        {
            if(value.Stat is not(195 or 196 or 198 or 201))
            {throw new NotSupportedException("NPC proc carrier needs its other stat/incoming/death/level-up consumer:"+value.Stat);}
            int layer=(ushort)value.Packed,rank=layer&63;_=ItemSkillExecution.RequireCurse(layer>>6);
            if(rank==0){throw new NotSupportedException("NPC rank-zero curse proc needs its own adapter");}
        }
    }
    internal void Add(Item item,int body,ulong seed)
    {
        _=Body;
        if(item is not{Deleted:false}||body is <0 or >12||Items.Any(i=>i.Item.Serial==item.Serial)||!NpcItemProcs.Available(item.Serial.Value,ActorId))
        {throw new NotSupportedException("Proc carrier identity/body is absent or already bound");}
        Mutate(()=>
        {
            var graph=new ItemPropertyCreationContext(item,Values,EmptyBasis,false,false,false,seed,[]);
            Items.Add(new(item,body,graph));EquipmentStatGraph.Merge(Unit,graph.Unit,true,Values);
        },true);
    }
    internal void Apply(Item item,EquipmentPropertyPlan plan,EquipmentPropertyInput input,ItemSkillPropertyServices metadata)
    {
        var carrier=Items.SingleOrDefault(i=>i.Item.Serial==item.Serial)??throw new InvalidOperationException("Proc carrier is not attached");
        if(plan is not{Steps.Length:1}||plan.Steps[0].Function!=11||plan.Steps[0].Stat is not(195 or 196 or 198 or 201))
        {throw new NotSupportedException("NPC graph admits explicit supported proc writers only");}
        // Validate through the real writer before touching the live graph.
        var isolated=new EquipmentStatValues(_=>carrier.Body,_=>null,_=>null,(_,_,_,_,_)=>{});
        var copy=ItemPropertyCreationContext.Restore(item,isolated,EmptyBasis,false,false,false,carrier.Graph.Capture());
        copy.ApplySkill(plan,input,metadata);Validate(copy.Capture());
        Mutate(()=>carrier.Graph.ApplySkill(plan,input,metadata),true);
    }
    internal void Remove(uint item)
    {
        var carrier=Items.SingleOrDefault(i=>i.Item.Serial.Value==item);if(carrier==null){return;}
        Mutate(()=>{EquipmentStatGraph.Detach(carrier.Graph.Unit.Stats,Values);Items.Remove(carrier);},true);
    }
    internal void Refresh(NpcCombatEvents legacy)
    {
        var actor=Body;
        Mutate(()=>
        {
            foreach(var item in Items.Where(i=>i.Item.Deleted).Select(i=>i.Item.Serial.Value).ToArray()){Remove(item);}
            // Reverse initial registration retains established inherent order.
            var wanted=new HashSet<uint>();
            foreach(int stat in legacy.Subscriptions().Reverse())
            {
                if(stat is not(81 or 112 or 113 or 114 or 128 or 134 or 135 or 136 or 198)){continue;}
                uint key=EquipmentStatKey.Pack(stat,stat==198?(SkillMechanics.For(CombatAbility.LifeTap).SourceId<<6)|5:0);
                int value=stat==81?NpcItemKnockback.Bonus(actor):stat==112?NpcItemFear.Bonus(actor):stat==113?NpcItemBlind.Bonus(actor):stat==114?NpcEquipmentCombat.EventBonus(actor,"dmg-to-mana"):stat==128?NpcLightningReturn.Bonus(actor):stat==134?NpcItemFreeze.Bonus(actor):stat==135?NpcOpenWounds.Chance(actor):stat==136?NpcCrushing.Chance(actor):NpcHitSkills.BloodChance(actor);
                wanted.Add(key);Values.SetBase(Unit.Stats,key,value);
            }
            foreach(var old in Unit.Stats.BaseValues.Where(v=>!wanted.Contains(v.Packed)).ToArray()){Values.SetBase(Unit.Stats,old.Packed,0);}
        });
    }
    internal PreparedMonsterDamagePacket Dispatch(string phase,PreparedMonsterDamagePacket packet,SkeletonGuardian actor,BaseCreature other,DateTime at,Func<int,int> roll,NpcItemTarget retained=null)
    {
        if(actor.Serial.Value!=ActorId){throw new InvalidOperationException("NPC proc event belongs to another actor");}
        var previous=Current;Current=new(actor,other,packet,at,roll,ItemEventDefinitions.Code(phase),retained);
        try{Events.Trigger(Current.Phase);return Current.Packet;}
        finally{Current=previous;Publish();}
    }
    private int Invoke(int function,int phase,int guid1,int guid2)
    {
        var frame=Current??throw new InvalidOperationException("NPC callback has no event frame");
        if(function==7)
        {
            var result=NpcItemKnockback.Apply(frame.Actor,frame.Other,frame.Packet);
            Current=frame with{Packet=result.Packet};return result.Success?1:0;
        }
        if(function==8){return NpcItemFear.Apply(frame.Actor,frame.Other,frame.At)?1:0;}
        if(function==9){return NpcItemBlind.Apply(frame.Actor,frame.Other,phase==6,frame.At)?1:0;}
        if(function==10){return NpcLightningReturn.Apply(frame.Actor,frame.Other,frame.At)?1:0;}
        if(function==13){return 0;} // Damage-to-mana is player-only before all packet/stat/RNG services.
        if(function==14){return NpcItemFreeze.Apply(frame.Actor,frame.Other,phase==6,frame.At)?1:0;}
        if(function==15){NpcOpenWounds.Apply(frame.Actor,frame.Other,frame.At,phase==6,frame.Roll);return 1;}
        if(function==16){NpcCrushing.Apply(frame.Actor,frame.Other,phase==6,frame.Roll);return 1;}
        if(function is not(20 or 21)){throw new NotSupportedException("NPC callback has no admitted body:"+function);}
        return ItemProcCallbacks.Run(function,unchecked((uint)guid1),new ProcServices(this,frame));
    }
    private sealed class ProcServices(NpcItemProcGraph graph,Frame frame):ItemProcServices
    {
        internal override bool HasSource=>frame.Actor is{Deleted:false};
        internal override bool HasTarget=>frame.Retained!=null||frame.Other is{Deleted:false};
        internal override bool HasPacket=>frame.Packet!=null;
        internal override uint HitFlags=>frame.Packet.HitFlags;
        internal override ushort ResultFlags=>(ushort)frame.Packet.ResultFlags;
        internal override int LevelBits=>6;
        internal override int Chance(int stat,int layer)=>graph.Values.Query(graph.Unit.Stats,EquipmentStatKey.Pack(stat,layer));
        internal override uint Roll()=>NpcEquipmentRandom.Raw(frame.Actor);
        internal override ItemProcSkill Skill(int id)=>new(ItemSkillExecution.RequireCurse(id).ItemTargetDo);
        internal override (int X,int Y) FirstPathPoint()=>throw new NotSupportedException("NPC no-target proc needs its actual path/ground cast adapter");
        internal override void CastTarget(bool targetAsCaster,int skill,int level,int flag)
        {
            if(targetAsCaster){throw new NotSupportedException("NPC target-as-caster proc needs its context");}
            if(frame.Retained!=null)
            {
                NpcItemSpellCasts.CastCurse(frame.Actor,frame.Retained,ItemSkillExecution.CurseAbility(skill),level,frame.At,flag);return;
            }
            if(frame.Other is not{Alive:true,Deleted:false})
            {throw new NotSupportedException("NPC dead/removed-target proc needs its explicit retained-target area adapter");}
            NpcItemSpellCasts.CastCurse(frame.Actor,frame.Other,ItemSkillExecution.CurseAbility(skill),level,frame.At,flag);
        }
        internal override void CastPosition(int skill,int level,int x,int y,int flag)=>throw new NotSupportedException("NPC ground proc is not admitted");
    }
    internal NpcProcState Capture()
    {
        if(Mutation!=0||Current!=null){throw new NotSupportedException("Capture NPC proc state only between mutations/callbacks");}
        return new(ActorId,OwnerId,(EquipmentStatValue[])Unit.Stats.BaseValues.Clone(),Items.Select(i=>new NpcProcItem(i.Item.Serial.Value,i.Body,i.Graph.Capture())).ToArray(),Events.Capture());
    }
    internal static void ValidateSnapshot(NpcProcState saved)
    {
        int tap=(SkillMechanics.For(CombatAbility.LifeTap).SourceId<<6)|5;
        if(saved is not{Innate:not null,Items:not null,Events:{Entries:not null}}||!((Serial)saved.Actor).IsMobile||!((Serial)saved.Owner).IsMobile
            ||saved.Actor==saved.Owner||saved.Items.Any(i=>i==null||!((Serial)i.Item).IsItem||i.Body is <0 or >12)
            ||saved.Items.Select(i=>i.Item).Distinct().Count()!=saved.Items.Length
            ||saved.Innate.Select(v=>v.Packed).Distinct().Count()!=saved.Innate.Length
            ||saved.Innate.Any(v=>v.Stat is not(112 or 113 or 128)&&v.Value<0||v.Stat is not(81 or 112 or 113 or 114 or 128 or 134 or 135 or 136 or 198)||(ushort)v.Packed!=(v.Stat==198?tap:0)))
        {throw new InvalidDataException("Invalid saved NPC proc graph");}
        _=SourceUnitEvents.Restore(saved.Events,(_,_,_,_)=>throw new InvalidOperationException("Validation cannot dispatch"));
        foreach(var item in saved.Items){Validate(item.State);}
        var active=saved.Innate.Concat(saved.Items.SelectMany(i=>i.State.Properties)).GroupBy(v=>v.Packed)
            .Where(g=>g.Sum(v=>(long)v.Value)!=0).Select(g=>g.Key);
        var expected=active.SelectMany(key=>ItemEventDefinitions.Bindings((int)(key>>16))
            .Where(e=>e.Event>0).Select(e=>((byte)e.Event,unchecked((int)key),0,(int)e.Function,2u,unchecked((int)key)))).Order();
        var observed=saved.Events.Entries.Select(e=>(e.Event,e.Guid1,e.Guid2,e.Function,e.Queue,e.Key)).Order();
        if(!saved.Events.HasOwner||!observed.SequenceEqual(expected))
        {throw new InvalidDataException("Saved NPC subscription payloads do not match its event stats");}
    }
    internal static NpcItemProcGraph Restore(NpcProcState saved,Action<NpcItemProcGraph> changed)
    {
        ValidateSnapshot(saved);
        var graph=new NpcItemProcGraph(saved.Actor,saved.Owner,null);graph.Mutation++;
        foreach(var value in saved.Innate){graph.Values.SetBase(graph.Unit.Stats,value.Packed,value.Value);}
        foreach(var value in saved.Items)
        {
            if(value.Body is <0 or >12){throw new InvalidDataException("Invalid NPC proc carrier body");}
            Validate(value.State);
            if(World.FindItem((Serial)value.Item) is not{Deleted:false} item){continue;}
            var context=ItemPropertyCreationContext.Restore(item,graph.Values,EmptyBasis,false,false,false,value.State);
            graph.Items.Add(new(item,value.Body,context));EquipmentStatGraph.Merge(graph.Unit,context.Unit,true,graph.Values);
        }
        // Restore original history, not sorted-stat reconstruction order.
        var rebuilt=graph.Events.Capture();
        var liveKeys=graph.Unit.Stats.FullValues.Where(v=>v.Value!=0).Select(v=>unchecked((int)v.Packed)).ToHashSet();
        var retained=saved.Events with{Entries=saved.Events.Entries.Where(e=>liveKeys.Contains(e.Guid1)).ToArray()};
        var signature=static(UnitEventEntry e)=>(e.Event,e.Guid1,e.Guid2,e.Function,e.Queue,e.Key);
        if(!retained.HasOwner||retained.Entries.Any(e=>e.Queue!=2||e.Key!=e.Guid1||e.Guid2!=0)
            ||!retained.Entries.Select(signature).Order().SequenceEqual(rebuilt.Entries.Select(signature).Order()))
        {throw new InvalidDataException("Saved NPC subscriptions do not match the original stat graph");}
        graph.Events=SourceUnitEvents.Restore(retained,graph.Invoke);graph.Bindings=new(graph.Events,ItemEventDefinitions.Bindings);
        graph.Mutation--;graph.Dirty=false;graph.Changed=changed;return graph;
    }
}
