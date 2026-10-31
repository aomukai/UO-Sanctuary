// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record BattleCommandState(uint Recipient,uint Creator,uint Supplier,int Rank,int SkillBonus,DateTime UntilUtc);
public sealed record CommandPet(uint Pet,long Order);
public sealed record CommandSave(BattleCommandState[] States,CommandPet[] Pets);
public sealed class CommandProfiles:GenericPersistence
{
    public CommandProfiles():base("SanctuaryBattleCommand",142){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BattleCommand.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Battle Command version");}
        BattleCommand.Restore(JsonSerializer.Deserialize<CommandSave>(reader.ReadString()));
    }
    public override void PostDeserialize()=>BattleCommand.AfterLoad();
}
public static class BattleCommand
{
    private static readonly Dictionary<uint,BattleCommandState> _states=new();
    private static readonly Dictionary<uint,long> _pets=new();
    private static long _nextPet;
    public static CommandProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick);
    private static bool Alive(Mobile actor)=>actor is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    // Removal and passive resource callbacks happen together, before native clamps.
    public static BattleCommandState State(Mobile actor)=>Alive(actor)?_states.GetValueOrDefault(actor.Serial.Value):null;
    public static int Bonus(Mobile actor)=>State(actor)?.SkillBonus??0;
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.BattleCommand,selected,point,now,(p,_,_,_)=>BarbarianShout.ValidCast(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.BattleCommand) || !BarbarianShout.ValidCast(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.BattleCommand);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        BarbarianShout.Cast(owner,values,TimedEffects.Now,CombatAbility.BattleCommand);
        NecromancerEffects.Pay(owner,values.Mana256);return true;
    }
    private static void Refresh(Mobile actor,BattleOrders.Pools before)
    {
        BattleOrders.Change(actor,before);
        if(actor is PlayerMobile player)
        {
            // Existing passive consumers query effective ranks live. Refresh native
            // resistance/timers/network; existing spell and summon snapshots stay stored.
            BarbarianCombat.Refresh(player);EnforcePets(player);SkillBindingNetwork.Send(player);
        }
    }
    internal static void Apply(PlayerMobile supplier,Mobile recipient,int rank,DateTime at)
    {
        if(!Alive(recipient)){return;}
        var before=BattleOrders.Capture(recipient);var values=SkillMechanics.Values(supplier,CombatAbility.BattleCommand,rank);
        uint creator=_states.TryGetValue(recipient.Serial.Value,out var old)?old.Creator:supplier.Serial.Value;
        _states[recipient.Serial.Value]=new(recipient.Serial.Value,creator,supplier.Serial.Value,rank,(int)values.Eval("aurastatcalc1"),at.AddMilliseconds(values.Eval("auralencalc")*40));
        Refresh(recipient,before);
    }
    public static void Clear(Mobile actor)
    {
        if(actor==null){return;}
        if(!Alive(actor)){_states.Remove(actor.Serial.Value);return;}
        if(!_states.ContainsKey(actor.Serial.Value)){return;}
        var before=BattleOrders.Capture(actor);_states.Remove(actor.Serial.Value);Refresh(actor,before);
    }
    // New summons record successful creation order. Historical summons predating
    // this ledger get a deterministic serial fallback; their original FIFO is unknown.
    internal static void Track(BaseCreature pet)=>_pets[pet.Serial.Value]=++_nextPet;
    internal static long PetOrder(BaseCreature pet)=>_pets.GetValueOrDefault(pet.Serial.Value);
    private static void EnforcePets(PlayerMobile owner)
    {
        if(!ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Necromancer){return;}
        var groups=new Dictionary<CombatAbility,List<SkeletonGuardian>>();
        foreach(var follower in owner.AllFollowers??[])
        {
            if(follower is not SkeletonGuardian{Alive:true,Deleted:false} pet || pet.ControlMaster!=owner){continue;}
            CombatAbility ability=pet.GetType()==typeof(SkeletonGuardian)?CombatAbility.RaiseSkeleton:pet is NecromancerMinion minion?minion.Kind:CombatAbility.None;
            if(ability is not (CombatAbility.RaiseSkeleton or CombatAbility.RaiseSkeletalMage)){continue;}
            if(!groups.TryGetValue(ability,out var pets)){groups.Add(ability,pets=new());}pets.Add(pet);
        }
        NecromancerGolems.Enforce(owner);
        foreach(var pair in groups)
        {
            foreach(var pet in pair.Value.OrderBy(x=>x.Serial.Value)){if(!_pets.ContainsKey(pet.Serial.Value)){Track(pet);}}
            int rank=SkillMechanics.Rank(owner,pair.Key);
            // Source pet counter clamps formula to at least1 for a skill present
            // in the skill list. Ownership/history permission remains independent.
            int maximum=Math.Max(1,(int)SkillMechanics.Values(owner,pair.Key,rank).Eval("petmax"));
            var ordered=pair.Value.OrderBy(PetOrder).ThenBy(x=>x.Serial.Value).ToArray();
            for(int i=0;i<ordered.Length-maximum;i++){ordered[i].Delete();}
        }
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;
        foreach(var pair in _states.ToArray())
        {
            var actor=World.FindMobile((Serial)pair.Key);
            if(!Alive(actor)){_states.Remove(pair.Key);}
            else if(pair.Value.UntilUtc<=now){Clear(actor);}
        }
        foreach(uint id in _pets.Keys.ToArray()){if(World.FindMobile((Serial)id) is not BaseCreature{Deleted:false}){_pets.Remove(id);}}
    }
    internal static void AfterLoad()=>Tick();
    public static CommandSave Snapshot()=>new(_states.Values.ToArray(),_pets.Select(x=>new CommandPet(x.Key,x.Value)).ToArray());
    public static void Restore(CommandSave saved)
    {
        if(saved?.States==null || saved.Pets==null){throw new InvalidDataException("Missing Battle Command state");}
        _states.Clear();_pets.Clear();_nextPet=0;
        foreach(var state in saved.States)
        {if(state.Recipient==0 || state.Creator==0 || state.Supplier==0 || state.Rank<1 || state.SkillBonus<=0 || !_states.TryAdd(state.Recipient,state)){throw new InvalidDataException("Invalid Battle Command recipient");}}
        var orders=new HashSet<long>();
        foreach(var pet in saved.Pets)
        {if(pet.Pet==0 || pet.Order<=0 || !orders.Add(pet.Order) || !_pets.TryAdd(pet.Pet,pet.Order)){throw new InvalidDataException("Invalid Battle Command pet order");}_nextPet=Math.Max(_nextPet,pet.Order);}
    }
}
