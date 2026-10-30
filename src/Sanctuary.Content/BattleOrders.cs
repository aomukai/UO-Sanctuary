// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record BattleOrdersState(uint Recipient,uint Creator,uint Supplier,int Rank,int Percent,DateTime UntilUtc);
public sealed record OrdersResources(uint Recipient,int LifeMaximum,int ManaMaximum,int StaminaMaximum,int StaminaFraction=0,int? Regeneration256=null);
public sealed record OrdersSave(BattleOrdersState[] States,OrdersResources[] Resources);
public sealed class OrdersProfiles:GenericPersistence
{
    public OrdersProfiles():base("SanctuaryBattleOrders",141){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BattleOrders.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Battle Orders version");}
        BattleOrders.Restore(JsonSerializer.Deserialize<OrdersSave>(reader.ReadString()));
    }
    public override void PostDeserialize()=>BattleOrders.AfterLoad();
}
public static class BattleOrders
{
    internal readonly record struct Pools(int LifeMax,int ManaMax,int StaminaMax,int Life256,int Mana256,int Stamina256)
    {internal int ExactLifeMaximum256{get;init;}}
    private static readonly Dictionary<uint,BattleOrdersState> _states=new();
    private static readonly Dictionary<uint,OrdersResources> _resources=new();
    private static readonly List<uint> _keys=new();
    public static OrdersProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();
        ClassCombatHooks.CreatureResourceMaximum=CreatureMaximum;
    }
    private static int? CreatureMaximum(BaseCreature actor,int resource,int basis)
    {
        // Original NPCs acquire source life through their saved Revive plan,
        // not a replacement subclass. Resolve it before the loading fallback.
        if(resource==0&&ReviveLife.Maximum256(actor) is{} revived){return (revived+255)/256;}
        if(World.Loading){return 65000;}
        if(resource==0 && PaladinConversion.Maximum256(actor) is{} converted){return (converted+255)/256;}
        if(resource==0 && SkillHealth.SourceMaximum256(actor) is{} exact){return (exact+255)/256;}
        int percent=resource==0?LifePercent(actor):Percent(actor);if(percent<=0){return null;}
        long base256=(long)basis*256;
        if(resource==0 && actor is SkeletonGuardian summon && actor.HitsMaxSeed==summon.CombatProfile.Life)
        {base256=summon.SourceLife256+((long)basis-summon.CombatProfile.Life)*256;}
        return (int)Math.Clamp((base256+ClassResources.Percentage(base256,percent))/256,0,65000);
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick);
    private static bool Alive(Mobile actor)=>actor is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    // Expiry removes the state and scales pools atomically on the logical timer.
    // Dropping its percentage in a maximum getter first would let native clamps lose saved life.
    public static BattleOrdersState State(Mobile actor)=>Alive(actor)?_states.GetValueOrDefault(actor.Serial.Value):null;
    public static int Percent(Mobile actor)=>State(actor)?.Percent??0;
    internal static int LifePercent(Mobile actor)=>Percent(actor)+DruidOakAuras.Percent(actor);
    internal static int Stamina256(Mobile actor)=>actor.Stam*256+(actor.Stam<actor.StamMax?_resources.GetValueOrDefault(actor.Serial.Value)?.StaminaFraction??0:0);
    internal static void SetStamina256(Mobile actor,int amount256)
    {
        amount256=Math.Clamp(amount256,0,actor.StamMax*256);actor.Stam=amount256/256;
        Remember(actor,amount256%256,_resources.GetValueOrDefault(actor.Serial.Value)?.Regeneration256);
    }
    internal static Pools Capture(Mobile actor)=>new(actor.HitsMax,actor.ManaMax,actor.StamMax,SkillHealth.Balance256(actor),NecromancerEffects.ManaBalance256(actor),Stamina256(actor)){ExactLifeMaximum256=SkillHealth.Maximum256(actor)};
    internal static int Rescale256(int current,int previousMaximum,int nextMaximum)
    {
        if(current<=0 || previousMaximum<=0 || previousMaximum==nextMaximum){return current;}
        if(nextMaximum<=0){return 0;} // Native zero-mana species never gain mana from this percentage buff.
        return (int)Math.Clamp((long)(current/(double)Math.Max(256,previousMaximum)*nextMaximum),1,nextMaximum);
    }
    private static int? Regen(Mobile actor,int maximum,int? old)
    {
        if(actor is not SkeletonGuardian guardian){return old;}
        string source=guardian.SourceMonsterId;
        int rate=NecromancerSummons.Source(source,"DamageRegen",-1);
        if(actor is ISourceLifeMaximum){maximum=SkillHealth.Maximum256(actor)>>8;}
        return rate<0?old:(int)((long)rate*maximum>>4);
    }
    internal static int Regeneration256(SkeletonGuardian actor)=>_resources.GetValueOrDefault(actor.Serial.Value)?.Regeneration256??actor.SourceRegeneration256;
    private static void Remember(Mobile actor,int staminaFraction,int? regeneration)=>_resources[actor.Serial.Value]=new(actor.Serial.Value,actor.HitsMax,actor.ManaMax,actor.StamMax,staminaFraction,regeneration);
    internal static void Change(Mobile actor,Pools before,bool preserveSummonLifeRatio=false)
    {
        int life=actor.HitsMax,mana=actor.ManaMax,stamina=actor.StamMax;
        int? regen=_resources.GetValueOrDefault(actor.Serial.Value)?.Regeneration256;
        int priorLifeMaximum256=before.ExactLifeMaximum256>0?before.ExactLifeMaximum256:before.LifeMax*256;
        int nextLifeMaximum256=SkillHealth.Maximum256(actor);
        if(nextLifeMaximum256!=priorLifeMaximum256 || preserveSummonLifeRatio)
        {
            SkillHealth.SetBalance256(actor,preserveSummonLifeRatio
                ?SummonDifficulties.Life256(before.Life256,priorLifeMaximum256,nextLifeMaximum256)
                :Rescale256(before.Life256,priorLifeMaximum256,nextLifeMaximum256));
            if(nextLifeMaximum256!=priorLifeMaximum256){regen=Regen(actor,life,regen);}
        }
        if(mana!=before.ManaMax){NecromancerEffects.SetMana256(actor,Rescale256(before.Mana256,before.ManaMax*256,mana*256));}
        int stam256=before.Stamina256;
        if(stamina!=before.StaminaMax)
        {stam256=Rescale256(stam256,before.StaminaMax*256,stamina*256);actor.Stam=stam256/256;}
        Remember(actor,stam256%256,regen);
        actor.CheckStatTimers();actor.Delta(MobileDelta.Hits|MobileDelta.Mana|MobileDelta.Stam);
        if(actor is PlayerMobile player){ClassStats.Refresh(player);StatNetwork.Send(player);}
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.BattleOrders,selected,point,now,(p,_,_,_)=>BarbarianShout.ValidCast(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.BattleOrders) || !BarbarianShout.ValidCast(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.BattleOrders);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        BarbarianShout.Cast(owner,values,TimedEffects.Now,CombatAbility.BattleOrders);
        // Generic no-start-function source dispatcher consumes after do68/self stats.
        // Its rare post-replacement insufficient-mana result is ignored, as in that source path.
        // Modern behavior of this edge remains explicitly unverified; ordinary cost is the pinned7.
        NecromancerEffects.Pay(owner,values.Mana256);return true;
    }
    internal static void Apply(PlayerMobile supplier,Mobile recipient,int rank,DateTime at)
    {
        if(!Alive(recipient)){return;}
        var before=Capture(recipient);var values=SkillMechanics.Values(supplier,CombatAbility.BattleOrders,rank);
        uint creator=_states.TryGetValue(recipient.Serial.Value,out var prior)?prior.Creator:supplier.Serial.Value;
        _states[recipient.Serial.Value]=new(recipient.Serial.Value,creator,supplier.Serial.Value,rank,(int)values.Eval("aurastatcalc1"),at.AddMilliseconds(values.Eval("auralencalc")*40));
        Change(recipient,before);
    }
    public static void Clear(Mobile recipient)
    {
        if(recipient==null){return;}
        if(!Alive(recipient)){_states.Remove(recipient.Serial.Value);_resources.Remove(recipient.Serial.Value);return;}
        if(!_states.ContainsKey(recipient.Serial.Value)){return;}
        var before=Capture(recipient);_states.Remove(recipient.Serial.Value);Change(recipient,before);
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;_keys.Clear();_keys.AddRange(_states.Keys);
        foreach(uint key in _keys)
        {
            var actor=World.FindMobile((Serial)key);
            if(!Alive(actor)){_states.Remove(key);_resources.Remove(key);continue;}
            if(_states.TryGetValue(key,out var state) && state.UntilUtc<=now){Clear(actor);}
        }
        // Existing equipment/history rules retain their own pool policy. Merely
        // observing a changed maximum must not rescale that already-handled change again.
        _keys.Clear();_keys.AddRange(_resources.Keys);
        foreach(uint key in _keys)
        {
            var actor=World.FindMobile((Serial)key);
            if(!Alive(actor)){_resources.Remove(key);continue;}
            var prior=_resources[key];int? regen=prior.LifeMaximum!=actor.HitsMax?Regen(actor,actor.HitsMax,prior.Regeneration256):prior.Regeneration256;
            Remember(actor,Stamina256(actor)%256,regen);
        }
    }
    internal static void AfterLoad()
    {
        Tick();
        // Creature getters keep the loading ceiling until all saved species/buffs
        // are available; now enforce true maxima for both managed and ordinary NPCs.
        foreach(var actor in World.Mobiles.Values)
        {
            if(actor is BaseCreature{Deleted:false} creature)
            {creature.Hits=Math.Min(creature.Hits,creature.HitsMax);creature.Mana=Math.Min(creature.Mana,creature.ManaMax);creature.Stam=Math.Min(creature.Stam,creature.StamMax);}
        }
    }
    public static OrdersSave Snapshot()=>new(new List<BattleOrdersState>(_states.Values).ToArray(),new List<OrdersResources>(_resources.Values).ToArray());
    public static void Restore(OrdersSave saved)
    {
        if(saved?.States==null || saved.Resources==null){throw new InvalidDataException("Missing Battle Orders states");}
        _states.Clear();_resources.Clear();
        foreach(var state in saved.States)
        {if(state.Recipient==0 || state.Creator==0 || state.Supplier==0 || state.Rank<1 || state.Percent<=0 || !_states.TryAdd(state.Recipient,state)){throw new InvalidDataException("Invalid Battle Orders recipient");}}
        foreach(var ledger in saved.Resources)
        {
            // Source-life NPCs can exceed the native 65000 basis cap. Keep
            // their saved whole maximum within the supported FP256 domain.
            if(ledger.Recipient==0 || ledger.LifeMaximum is <0 or >int.MaxValue/256 || ledger.ManaMaximum is <0 or >65000 || ledger.StaminaMaximum is <0 or >65000
                || ledger.StaminaFraction is <0 or >255 || ledger.Regeneration256<0 || !_resources.TryAdd(ledger.Recipient,ledger))
            {throw new InvalidDataException("Invalid Battle Orders resource ledger");}
        }
    }
}
