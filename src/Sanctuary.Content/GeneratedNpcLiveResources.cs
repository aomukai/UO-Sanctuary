// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record GeneratedNpcResourceState(int Version,uint Actor,uint Owner,EquipmentStatValue[] Basis,int DamageRegeneration,
    int RegenerationBase,uint[] ActiveItems,int LifePercent,int ManaPercent,int StaminaPercent,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]ValkyrieResourceBirth Valkyrie=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]int? StaminaPercentCache=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]GeneratedNpcEventState ItemEvents=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]ShadowResourceBirth Shadow=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]ShadowPassiveFrame Passives=null);

public sealed class GeneratedNpcResourceProfiles:GenericPersistence
{
    private GeneratedNpcResourceState[] Saved;
    public GeneratedNpcResourceProfiles():base("SanctuaryGeneratedNpcResources",240){}
    public override void Serialize(){Saved=GeneratedNpcLiveResources.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(Saved));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown generated NPC resource ledger");}
        GeneratedNpcLiveResources.Restore(JsonSerializer.Deserialize<GeneratedNpcResourceState[]>(reader.ReadString()));
    }
}

// Explicit resource-only recipient profile. No learned actor is enabled here.
// Derived graphs are rebuilt without resource callbacks on load/getters. The
// existing native FP256 ledgers own current pools; this saves source bases,
// regeneration and logical item attachment order, never replacement items.
internal static class GeneratedNpcLiveResources
{
    private static readonly Dictionary<uint,GeneratedNpcResourceState> States=new();
    private static readonly Dictionary<uint,GeneratedNpcStatGraph> Frames=new();
    public static void Configure()=>_ = new GeneratedNpcResourceProfiles();
    private static bool Live(Mobile actor)=>actor is BaseCreature{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Deleted:false}};
    internal static bool Handles(Mobile actor)=>actor is BaseCreature{Alive:true,Deleted:false}&&States.ContainsKey(actor.Serial.Value);
    private static GeneratedNpcResourceState Copy(GeneratedNpcResourceState state)=>state with{Basis=(EquipmentStatValue[])state.Basis.Clone(),ActiveItems=(uint[])state.ActiveItems.Clone(),
        ItemEvents=state.ItemEvents is{} events?events with{ActiveItems=(uint[])events.ActiveItems.Clone(),Events=events.Events with{Entries=(UnitEventEntry[])events.Events.Entries.Clone()}}:null,
        Passives=ShadowPassiveGraph.Copy(state.Passives)};
    private static int Raw(EquipmentStatValue[] fields,int stat)=>EquipmentStatValues.Raw(fields,EquipmentStatKey.Pack(stat));
    private static EquipmentStatValue[] Put(EquipmentStatValue[] fields,int stat,int value)=>fields.Where(v=>v.Packed!=EquipmentStatKey.Pack(stat))
        .Append(new(EquipmentStatKey.Pack(stat),value)).Where(v=>v.Value!=0).OrderBy(v=>v.Packed).ToArray();
    private static void Validate(GeneratedNpcResourceState state)
    {
        if(state is not{Version:0,Basis:not null,ActiveItems:not null}||!((Serial)state.Actor).IsMobile||!((Serial)state.Owner).IsMobile
            ||state.Actor==state.Owner||state.DamageRegeneration is <0 or >32767||state.Basis.Length>368
            ||state.Basis.Select(v=>v.Packed).Distinct().Count()!=state.Basis.Length||state.Basis.Any(v=>(v.Packed&65535)!=0||EquipmentStatValues.Definition(v.Stat)==null)
            ||state.ActiveItems.Length>10||state.ActiveItems.Any(id=>!((Serial)id).IsItem)||state.ActiveItems.Distinct().Count()!=state.ActiveItems.Length
            ||Raw(state.Basis,7)is <1 or >16640000||Raw(state.Basis,9)is <0 or >16640000||Raw(state.Basis,11)is <0 or >16640000||Raw(state.Basis,12)is <1 or >99
            ||state.Basis.Any(v=>v.Stat is 76 or 77 or 162)||state.LifePercent is <0 or >100000||state.ManaPercent is <0 or >100000||state.StaminaPercent is <0 or >100000)
        {throw new InvalidDataException("Invalid explicit generated NPC source resource profile");}
        if(state.Valkyrie!=null){ValkyrieEquipmentResources.Validate(state.Valkyrie);}
        if(state.Shadow!=null)
        {
            ShadowEquipmentResources.Validate(state.Shadow);
            if(state.Valkyrie!=null||state.ItemEvents!=null){throw new InvalidDataException("Shadow resource-only frame cannot claim another species or active item events");}
        }
        if(state.ItemEvents is{} events)
        {
            GeneratedNpcItemEvents.Validate(events);
            if(state.Valkyrie==null||events.Actor!=state.Actor||events.Owner!=state.Owner||!events.ActiveItems.SequenceEqual(state.ActiveItems))
            {throw new InvalidDataException("Original item events lost their source resource binding");}
        }
        if(state.Passives is{} passives)
        {
            if(state.Shadow==null){throw new InvalidDataException("NPC passive frame belongs to its original Shadow");}
            ShadowPassiveGraph.Validate(passives,state.ActiveItems);
        }
    }
    private static void Supported(IEnumerable<EquipmentStatValue> fields)
    {
        foreach(var field in fields.Where(v=>v.Value!=0))
        {
            var definition=EquipmentStatValues.Definition(field.Stat);var events=ItemEventDefinitions.Bindings(field.Stat);
            if(definition==null||definition.Operation is 6 or 7||field.Stat is 83 or 97 or 98 or 126 or 127 or 151 or 188 or 204
                ||events is{Length:>0}&&events[0].Event>0)
            {throw new NotSupportedException("Generated resource profile has an unsupported time/skill/event/aura/state/charge consumer: stat "+field.Stat);}
        }
    }
    private static GeneratedNpcStatGraph Build(BaseCreature actor,GeneratedNpcResourceState state,bool birth=false,MonsterDifficulty? preview=null,bool bindItemEvents=false)
    {
        if(Frames.TryGetValue(state.Actor,out var active)&&active.ItemEvents?.Dispatching==true)
        {throw new NotSupportedException("Rebuilding an active original event frame needs its live mutation service");}
        Validate(state);
        if(!Live(actor)||actor.Serial.Value!=state.Actor||actor.ControlMaster.Serial.Value!=state.Owner)
        {throw new NotSupportedException("Generated resource profile lost its original live actor/owner");}
        if(state.Valkyrie!=null)
        {
            if(actor is not AmazonValkyrieBody body){throw new NotSupportedException("Valkyrie resource context belongs to another native species");}
            ValkyrieEquipmentResources.Require(body,state,preview);
        }
        else if(state.Shadow!=null)
        {
            if(actor is not AssassinShadowBody body){throw new NotSupportedException("Shadow resources belong to another species");}
            ShadowEquipmentResources.Require(body,state,preview);
            if(state.Passives is{} passives&&passives.Book!=body.Prepared.Skills)
            {throw new NotSupportedException("Shadow passive cache lost its actual owned skill book");}
        }
        else
        {
            Supported(state.Basis);
            foreach(var item in GeneratedNpcEquipment.Items(actor))
            {var saved=item.Snapshot;Supported(saved.Graph.ItemBase.Concat(saved.Graph.Properties));}
        }
        var basis=Put(state.Basis,74,state.RegenerationBase);
        GeneratedNpcStatGraph graph=null;
        graph=new(actor,basis,_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>Clamp(graph),null,state.ActiveItems,g=>
        {
            if(state.Shadow is{} shadow)
            {
                // The initializer already installed the server callback.
                // Birth maximum-life writes therefore rescale HP/regen before
                // item replay. Reconstruction keeps the saved regen cache;
                // it has no native pool setters or repeated birth callbacks.
                if(birth){g.SetResourceCallbacks(state.DamageRegeneration);}
                var writes=ShadowEquipmentResources.ApplyBirth(g,shadow);
                if(g.Read(7)!=writes.MaximumLifeWrite256||g.Read(6)!=writes.CurrentLifeWrite256)
                {throw new NotSupportedException("Shadow birth stat writers changed its qualified resource domain");}
            }
            // Use source writers,not raw FullValues injection: operation1
            // retains skill_staminapercent in BaseValues only. Reconstruction
            // has no live setters or resource rescale effects.
            foreach(var field in new[]{(76,state.LifePercent),(77,state.ManaPercent),(162,state.StaminaPercent)})
            {g.Values.SetBase(g.Actor.Stats,EquipmentStatKey.Pack(field.Item1),field.Item2,g.Actor);}
            foreach(int stat in new[]{7,9,11}){g.Values.Recalculate(g.Actor.Stats,EquipmentStatKey.Pack(stat));}
            if(birth)
            {
                if(state.Valkyrie is{} source&&g.Read(7)!=source.LifeMaximum256)
                {throw new NotSupportedException("Valkyrie initial source maximum differs before original item replay");}
                if(g.Read(6)<=0||g.Read(6)>g.Read(7)||g.Read(8)<0||g.Read(8)>g.Read(9)||g.Read(10)<0||g.Read(10)>g.Read(11))
                {throw new NotSupportedException("Generated birth resources need explicit current balances within their source maxima");}
                g.SetResourceCallbacks(state.DamageRegeneration);
            }
        },bindItemEvents:bindItemEvents,eventHistory:state.ItemEvents);
        if(state.Passives!=null)
        {graph.Passives=ShadowPassiveGraph.Restore(graph,state.Passives,()=>BattleCommand.Bonus(actor));}
        // Source operation1 may retain an earlier FullValues entry while its
        // BaseValues percentage changes. Item/buff order therefore matters;
        // retain the operative cache across reconstruction,not just the base.
        if(state.StaminaPercentCache is{} cached){graph.Actor.Stats.FullValues=Put(graph.Actor.Stats.FullValues,162,cached);}
        CheckMaximums(graph);return graph;
    }
    private static void CheckMaximums(GeneratedNpcStatGraph graph)
    {
        if(graph.Read(7)is <1 or >16640000||graph.Read(9)is <0 or >16640000||graph.Read(11)is <0 or >16640000)
        {throw new NotSupportedException("Generated live resource maximum exceeds the verified native FP256 domain");}
    }
    private static GeneratedNpcStatGraph Current(BaseCreature actor)
    {
        var state=States[actor.Serial.Value];
        if(state.Valkyrie is{} source&&(actor is not AmazonValkyrieBody body||body.Prepared.Birth!=source.Birth||body.Prepared.Difficulty!=source.Difficulty))
        {throw new NotSupportedException("Valkyrie resource frame needs an explicit captured-basis transition");}
        if(state.Shadow is{} shadow&&(actor is not AssassinShadowBody recipient||recipient.Prepared.Capture!=shadow.Capture
            ||recipient.Prepared.Initialization!=shadow.Initialization||recipient.Prepared.Difficulty!=shadow.Difficulty
            ||state.Passives is{} passives&&recipient.Prepared.Skills!=passives.Book))
        {throw new NotSupportedException("Shadow resource frame needs an explicit captured-basis transition");}
        if(!Frames.TryGetValue(state.Actor,out var graph)){Frames[state.Actor]=graph=Build(actor,state);}
        _=graph.Read(7);return graph;
    }
    internal static int? Maximum256(Mobile actor,int resource)=>!World.Loading&&resource is >=0 and <=2&&Handles(actor)
        ?Current((BaseCreature)actor).Read(resource==0?7:resource==1?11:9):null;
    internal static int? RegenerationBase(Mobile actor)=>Handles(actor)?States[actor.Serial.Value].RegenerationBase:null;
    internal static int? ItemRegeneration(Mobile actor)=>Handles(actor)?checked(Current((BaseCreature)actor).Read(74)-States[actor.Serial.Value].RegenerationBase):null;
    internal static GeneratedNpcResourceState State(BaseCreature actor)=>States.GetValueOrDefault(actor.Serial.Value)is{} state?Copy(state):null;
    internal static void Forget(BaseCreature actor){States.Remove(actor.Serial.Value);Frames.Remove(actor.Serial.Value);}
    internal static GeneratedNpcStatGraph ShadowFrame(AssassinShadowBody actor,MonsterDifficulty? preview=null)
    {
        if(World.Loading||!Handles(actor)||States[actor.Serial.Value].Shadow==null){return null;}
        var old=States[actor.Serial.Value];
        if(preview==null||preview==old.Shadow.Difficulty){var current=Current(actor);Seed(actor,current);return current;}
        return Build(actor,old with{Shadow=old.Shadow with{Difficulty=preview.Value}},preview:preview);
    }
    internal static void RefreshShadowBasis(AssassinShadowBody actor)
    {
        if(!Handles(actor)||States[actor.Serial.Value].Shadow==null){return;}
        var old=States[actor.Serial.Value];var next=old with{Shadow=old.Shadow with{Difficulty=actor.Prepared.Difficulty}};
        var graph=Build(actor,next);States[next.Actor]=next;Frames[next.Actor]=graph;
    }
    internal static void ActivateShadow(AssassinShadowBody actor,ShadowResourceBirth source)
    {
        if(!Live(actor)||States.ContainsKey(actor.Serial.Value)||actor.Prepared?.Weapons==null||actor.Map!=Map.Internal)
        {throw new NotSupportedException("Shadow resources need their original parked initialized body/loadout");}
        var state=new GeneratedNpcResourceState(0,actor.Serial.Value,actor.ControlMaster.Serial.Value,ShadowEquipmentResources.Basis(source),
            NecromancerSummons.Source(actor.SourceMonsterId,"DamageRegen"),source.Initialization.RegenerationBase,
            ShadowLoadout.Originals(actor).Select(i=>i.Serial.Value).ToArray(),BattleOrders.LifePercent(actor),BattleOrders.Percent(actor),
            checked(BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor)),Shadow:source);
        var graph=Build(actor,state,true);
        if(actor.Prepared.Skills!=null)
        {graph.Passives=ShadowPassiveGraph.Initialize(graph,actor.Prepared.Skills,()=>BattleCommand.Bonus(actor));}
        Commit(actor,state,graph);
    }
    internal static GeneratedNpcStatGraph ValkyrieFrame(AmazonValkyrieBody actor,MonsterDifficulty? preview=null)
    {
        if(World.Loading||!Handles(actor)||States[actor.Serial.Value].Valkyrie==null){return null;}
        var old=States[actor.Serial.Value];
        if(preview==null||preview==old.Valkyrie.Difficulty){var current=Current(actor);Seed(actor,current);return current;}
        var source=old.Valkyrie with{Difficulty=preview.Value};
        return Build(actor,old with{Valkyrie=source,Basis=ValkyrieEquipmentResources.Basis(actor,source,preview.Value)},preview:preview);
    }
    internal static void RefreshValkyrieBasis(AmazonValkyrieBody actor)
    {
        if(!Handles(actor)||States[actor.Serial.Value].Valkyrie==null){return;}
        var old=States[actor.Serial.Value];var source=old.Valkyrie with{Difficulty=actor.Prepared.Difficulty};
        var next=old with{Valkyrie=source,Basis=ValkyrieEquipmentResources.Basis(actor,source,source.Difficulty)};
        var graph=Build(actor,next);States[next.Actor]=next;Frames[next.Actor]=graph;
    }
    internal static void ActivateValkyrie(AmazonValkyrieBody actor,ValkyrieResourceBirth source)
    {
        if(!Live(actor)||States.ContainsKey(actor.Serial.Value)||ReviveNativeActivation.State(actor)!=null||IronGolemItems.State(actor)!=null)
        {throw new NotSupportedException("Valkyrie source resources need an unbound original prepared recipient");}
        var state=new GeneratedNpcResourceState(0,actor.Serial.Value,actor.ControlMaster.Serial.Value,ValkyrieEquipmentResources.Basis(actor,source,source.Difficulty),
            NecromancerSummons.Source("valkyrie","DamageRegen"),source.RegenerationBase,ValkyrieEquipment.Originals(actor).Select(i=>i.Serial.Value).ToArray(),
            BattleOrders.LifePercent(actor),BattleOrders.Percent(actor),checked(BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor)),source);
        var graph=Build(actor,state,true);Commit(actor,state,graph);
    }
    internal static void ActivateItemEvents(AmazonValkyrieBody actor)
    {
        if(!Handles(actor)||States[actor.Serial.Value]is not{Valkyrie:not null,ItemEvents:null} old||NpcItemProcs.Get(actor)!=null)
        {throw new NotSupportedException("Original item registrations require their unbound full Valkyrie resource graph");}
        var graph=Build(actor,old,bindItemEvents:true);Seed(actor,graph);Commit(actor,old,graph);
    }
    internal static void Activate(BaseCreature actor,EquipmentStatValue[] basis,int damageRegeneration)
    {
        if(!Live(actor)||States.ContainsKey(actor.Serial.Value)||actor is ISourceLifeMaximum||ReviveNativeActivation.State(actor)!=null
            ||actor is SkeletonGuardian guardian&&IronGolemItems.State(guardian)!=null)
        {throw new NotSupportedException("Generated source resources require an unbound explicit prepared recipient");}
        var state=new GeneratedNpcResourceState(0,actor.Serial.Value,actor.ControlMaster.Serial.Value,(EquipmentStatValue[])basis.Clone(),damageRegeneration,Raw(basis,74),
            GeneratedNpcEquipment.Items(actor).Select(i=>i.Serial.Value).ToArray(),BattleOrders.LifePercent(actor),BattleOrders.Percent(actor),checked(BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor)));
        var graph=Build(actor,state,true);Commit(actor,state,graph);
    }
    private static void Seed(BaseCreature actor,GeneratedNpcStatGraph graph)
    {
        graph.SetResourceCallbacks(null);
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(6),SkillHealth.BalanceAtMaximum256(actor,graph.Read(7)));
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(8),Math.Min(graph.Read(9),NecromancerEffects.RawManaBalance256(actor)));
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(10),Math.Min(graph.Read(11),BattleOrders.RawStamina256(actor)));
    }
    private static void Clamp(GeneratedNpcStatGraph graph)
    {
        foreach(int maximum in new[]{7,9,11})
        {int current=graph.Read(maximum-1);if(current>graph.Read(maximum)){graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(maximum-1),graph.Read(maximum));}}
    }
    private static void Commit(BaseCreature actor,GeneratedNpcResourceState state,GeneratedNpcStatGraph graph)
    {
        CheckMaximums(graph);state=state with{RegenerationBase=EquipmentStatValues.Base(graph.Actor.Stats,EquipmentStatKey.Pack(74)),StaminaPercentCache=graph.Read(162)};
        int life=graph.Read(6),mana=graph.Read(8),stamina=graph.Read(10);
        state=state with{ItemEvents=graph.ItemEvents?.Capture(),Passives=graph.Passives?.Capture()};Validate(state);
        States[state.Actor]=state;Frames[state.Actor]=graph;
        SkillHealth.SetBalance256(actor,life);NecromancerEffects.SetMana256(actor,mana);BattleOrders.SetStamina256(actor,stamina);
        if(state.Valkyrie!=null){((AmazonValkyrieBody)actor).ApplyEquipmentResistances();}
        if(state.Shadow!=null){((AssassinShadowBody)actor).ApplyEquipmentResistances();}
        actor.CheckStatTimers();actor.Delta(MobileDelta.Hits|MobileDelta.Mana|MobileDelta.Stam);
    }
    internal static void ChangeBase(BaseCreature actor,int stat,int value)
    {
        _=Current(actor);var old=States[actor.Serial.Value];
        if(old.Valkyrie!=null||old.Shadow!=null){throw new NotSupportedException("Captured summon source bases change only through their explicit birth/difficulty owner");}
        if(stat is 6 or 8 or 10 or 74 or 76 or 77 or 162||EquipmentStatValues.Definition(stat)==null)
        {throw new NotSupportedException("Current pools,regeneration and skill percentages have dedicated source owners");}
        var next=old with{Basis=Put(old.Basis,stat,value)};Validate(next);Supported(next.Basis);
        // Trial graph keeps the committed native profile untouched on refusal.
        var trial=Build(actor,old);Seed(actor,trial);trial.SetResourceCallbacks(old.DamageRegeneration);
        trial.Values.SetBase(trial.Actor.Stats,EquipmentStatKey.Pack(stat),value,trial.Actor);Commit(actor,next,trial);
    }
    internal static void SetItemActive(BaseCreature actor,uint item,bool active)
    {
        _=Current(actor);var old=States[actor.Serial.Value];bool present=old.ActiveItems.Contains(item);
        if(present==active){return;}
        var trial=Build(actor,old);Seed(actor,trial);trial.SetResourceCallbacks(old.DamageRegeneration);
        if(active){trial.Update(item);}else{trial.Transferred(item);}
        var next=old with{ActiveItems=active?old.ActiveItems.Append(item).ToArray():old.ActiveItems.Where(id=>id!=item).ToArray()};
        Commit(actor,next,trial);
    }
    // Existing BO/Oak producers own the native percentage transition. Refresh
    // only derived maxima and the source monster regeneration branch here.
    internal static void ExternalChange(Mobile actor,BattleOrders.Pools before)
    {
        if(World.Loading||!Handles(actor)){return;}
        var old=States[actor.Serial.Value];var next=old with{LifePercent=BattleOrders.LifePercent(actor),ManaPercent=BattleOrders.Percent(actor),StaminaPercent=checked(BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor))};
        var current=Current((BaseCreature)actor);
        var graph=Build((BaseCreature)actor,old with{StaminaPercentCache=current.Read(162)});Seed((BaseCreature)actor,graph);graph.SetResourceCallbacks(old.DamageRegeneration);
        // Preserve source setter order after the original item graph. The
        // native buff owner subsequently commits these same pool transitions.
        foreach(var field in new[]{(77,next.ManaPercent),(76,next.LifePercent),(162,next.StaminaPercent)})
        {graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(field.Item1),field.Item2,graph.Actor);}
        CheckMaximums(graph);next=next with{RegenerationBase=EquipmentStatValues.Base(graph.Actor.Stats,EquipmentStatKey.Pack(74)),StaminaPercentCache=graph.Read(162)};Validate(next);
        next=next with{ItemEvents=graph.ItemEvents?.Capture(),Passives=graph.Passives?.Capture()};Validate(next);
        States[next.Actor]=next;Frames[next.Actor]=graph;
    }
    internal static void ChangeShadowSkills(AssassinShadowBody actor,string book)
    {
        _=Current(actor);var old=States[actor.Serial.Value];var nextBook=ShadowSkillBook.Restore(book).Save();
        if(old.Passives==null){actor.RestorePrepared(actor.Prepared with{Skills=nextBook});return;}
        var graph=Build(actor,old);Seed(actor,graph);graph.Passives.Refresh(nextBook);
        var next=old with{Passives=graph.Passives.Capture()};Validate(next);CheckMaximums(graph);
        // The admitted passive fields have no resource/value operations.
        // Both candidates validate before replacing either authoritative state.
        actor.RestorePrepared(actor.Prepared with{Skills=nextBook});States[next.Actor]=next;Frames[next.Actor]=graph;
    }
    internal static void RefreshShadowPassives(Mobile actor)
    {
        if(World.Loading||actor is not AssassinShadowBody body||!Handles(body)||States[body.Serial.Value].Passives==null){return;}
        var old=States[body.Serial.Value];_=Current(body);var graph=Build(body,old);Seed(body,graph);
        graph.Passives.Refresh(body.Prepared.Skills);var next=old with{Passives=graph.Passives.Capture()};Validate(next);CheckMaximums(graph);
        States[next.Actor]=next;Frames[next.Actor]=graph;
    }
    internal static bool RefreshVigor(Mobile actor)
    {
        if(!Handles(actor)){return false;}
        if(!World.Loading){var before=BattleOrders.Capture(actor);BattleOrders.Change(actor,before);}
        return true;
    }
    internal static GeneratedNpcResourceState[] Snapshot()
    {
        var result=new List<GeneratedNpcResourceState>();
        foreach(var state in States.Values)
        {if(World.FindMobile((Serial)state.Actor)is BaseCreature actor&&Handles(actor)){var graph=Current(actor);var saved=state with{ItemEvents=graph.ItemEvents?.Capture(),Passives=graph.Passives?.Capture()};Validate(saved);result.Add(Copy(saved));}}
        foreach(uint id in States.Keys.Except(result.Select(s=>s.Actor)).ToArray()){States.Remove(id);Frames.Remove(id);}
        return result.OrderBy(s=>s.Actor).ToArray();
    }
    internal static void Restore(GeneratedNpcResourceState[] states)
    {
        if(states==null||states.Length>100000||states.Select(s=>s.Actor).Distinct().Count()!=states.Length){throw new InvalidDataException("Invalid generated NPC resource ledger");}
        foreach(var state in states)
        {
            Validate(state);
            if(state.Valkyrie==null&&state.Shadow==null){Supported(state.Basis);}
            else if(!World.Loading)
            {
                var actor=World.FindMobile((Serial)state.Actor)as BaseCreature;
                if(state.Shadow!=null?actor is not AssassinShadowBody:actor is not AmazonValkyrieBody)
                {throw new InvalidDataException("Live source resource restore lost its original species");}
                _=Build(actor,state);
            }
        }
        States.Clear();Frames.Clear();foreach(var state in states){States.Add(state.Actor,Copy(state));}
    }
}
