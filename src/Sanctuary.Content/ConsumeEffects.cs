// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ConsumeEffect(uint Owner,uint OriginalPet,long Order,DateTime AppliedUtc,DateTime UntilUtc,ConsumeSuppliers Suppliers);
public sealed class ConsumeProfiles:GenericPersistence
{
    public ConsumeProfiles():base("SanctuaryConsume",250){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(ConsumeEffects.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Consume profile version");}ConsumeEffects.Restore(JsonSerializer.Deserialize<ConsumeEffect[]>(reader.ReadString()));}
    public override void PostDeserialize()=>ConsumeEffects.AfterLoad();
}

// Prepared owner state and logical source-pool slot. The original demon is
// retired,not parked for later resurrection. Normal learned admission uses the approved original/bound roster.
internal static class ConsumeEffects
{
    internal static bool Approved=>true;
    private static ImmutableDictionary<uint,ConsumeEffect> States=ImmutableDictionary<uint,ConsumeEffect>.Empty;
    public static void Configure()=>_=new ConsumeProfiles();
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static bool Owner(PlayerMobile p)=>p is{Alive:true,Deleted:false}
        &&ClassCreation.Profiles?.TryGet(p.Serial,out var profile)==true&&profile.Class==DiabloClass.Warlock;
    // Like Druid forms,keep an elapsed percentage visible until the removal
    // transaction captures its old maximum. The shared tick owns expiry.
    internal static ConsumeEffect State(Mobile actor)=>actor is PlayerMobile p&&Owner(p)?States.GetValueOrDefault(p.Serial.Value):null;
    internal static int LifePercent(PlayerMobile p)=>State(p)?.Suppliers.LifePercent??0;
    internal static int Velocity(PlayerMobile p)=>State(p)?.Suppliers.VelocityPercent??0;
    internal static int DamagePercent(PlayerMobile p)=>State(p)?.Suppliers.Bonus.DamagePercent??0;
    internal static int Crushing(PlayerMobile p)=>State(p)?.Suppliers.Bonus.CrushingChance??0;
    internal static int AllSkills(PlayerMobile p)=>State(p)?.Suppliers.Bonus.AllSkills??0;
    internal static int StatBonus(PlayerMobile p,string code)=>State(p)is{} s?code switch
    {
        "res-fire"=>s.Suppliers.Bonus.FireResist,"res-cold"=>s.Suppliers.Bonus.ColdResist,"res-ltng"=>s.Suppliers.Bonus.LightningResist,
        "res-pois"=>s.Suppliers.Bonus.PoisonResist,"res-mag"=>s.Suppliers.Bonus.MagicResist,"res-physical"=>s.Suppliers.Bonus.PhysicalResist,
        "res-fire-max"=>s.Suppliers.Bonus.MaxFireResist,"res-cold-max"=>s.Suppliers.Bonus.MaxColdResist,"res-ltng-max"=>s.Suppliers.Bonus.MaxLightningResist,
        "res-pois-max"=>s.Suppliers.Bonus.MaxPoisonResist,"res-mag-max"=>s.Suppliers.Bonus.MaxMagicResist,_=>0
    }:0;
    internal static int ElementBonus(PlayerMobile p,string code)=>State(p)is{} state?code switch
    {"extra-fire"=>state.Suppliers.Bonus.FireMastery,"pierce-fire"=>state.Suppliers.Bonus.FirePierce,
        "extra-mag"=>state.Suppliers.Bonus.MagicMastery,"pierce-mag"=>state.Suppliers.Bonus.MagicPierce,
        "pierce-cold"=>state.Suppliers.Bonus.ColdPierce,"pierce-ltng"=>state.Suppliers.Bonus.LightningPierce,_=>0}:0;
    internal static void Validate(ConsumeEffect state)
    {
        if(state==null){throw new InvalidDataException("Missing Consume state");}ConsumePrepared.Validate(state.Suppliers);ConsumePrepared.RequireRuntime(state.Suppliers);
        if(!((Serial)state.Owner).IsMobile||!((Serial)state.OriginalPet).IsMobile||state.Owner==state.OriginalPet||state.Order<=0
            ||state.AppliedUtc.Kind!=DateTimeKind.Utc||state.UntilUtc.Kind!=DateTimeKind.Utc
            ||state.AppliedUtc>DateTime.MaxValue.AddMilliseconds(-40L*state.Suppliers.DurationFrames)
            ||state.UntilUtc!=state.AppliedUtc.AddMilliseconds(40L*state.Suppliers.DurationFrames))
        {throw new InvalidDataException("Invalid Consume owner/slot/clock");}
    }
    internal static bool Clear(PlayerMobile owner)
    {
        if(owner==null||!States.ContainsKey(owner.Serial.Value)){return false;}
        var previous=States[owner.Serial.Value];
        var before=owner is{Alive:true,Deleted:false}?BattleOrders.Capture(owner):(BattleOrders.Pools?)null;
        States=States.Remove(owner.Serial.Value);
        if(before is{} pools){BattleOrders.Change(owner,pools);}else if(!owner.Deleted){ClassStats.Refresh(owner);}
        if(!owner.Deleted){owner.UpdateResistances();}
        ConsumeAuras.Changed(owner,previous,null);
        if(WarlockRitualVisuals.Clear(owner,SpellEffectGraphics.ConsumeWard))
        {WarlockRitualVisuals.Attached(owner,SpellEffectGraphics.ConsumeFade,SpellEffectSounds.ConsumeFade);}
        return true;
    }
    internal static void ApplyPrepared(PlayerMobile owner,ConsumeEffect state)
    {
        Validate(state);if(!Owner(owner)||owner.Serial.Value!=state.Owner){throw new NotSupportedException("Consume state lost its original living Warlock");}
        var previous=State(owner);
        var before=BattleOrders.Capture(owner);States=States.SetItem(state.Owner,state);BattleCommand.ReservePetOrder(state.Order);
        BattleOrders.Change(owner,before);
        owner.UpdateResistances();
        ConsumeAuras.Changed(owner,previous,state);
        WarlockRitualVisuals.Renew(owner,SpellEffectGraphics.ConsumeWard,TimedEffects.Now,state.UntilUtc);
    }
    private static void Moved(Mobile mobile)
    {if(!World.Loading&&mobile is PlayerMobile p){Sync(p,TimedEffects.Now);}}
    private static void Sync(PlayerMobile owner,DateTime at)
    {
        if(!States.TryGetValue(owner.Serial.Value,out var state)){return;}
        if(!Owner(owner)||state.UntilUtc<=at){Clear(owner);return;}
        if(owner.Map==null||owner.Map==Map.Internal){return;}
        var difficulty=SummonDifficulties.For(owner.Map);if(difficulty==state.Suppliers.Difficulty){return;}
        // The consumed slot follows the approved summon facet rule. Preserve
        // its captured ranks,hard synergy,original order and exact deadline.
        var saved=state.Suppliers;var values=new SkillValues(SkillMechanics.For(CombatAbility.Consume),_=>saved.Rank,_=>0,99);
        var replacement=state with{Suppliers=ConsumePrepared.Capture(values,saved.HardDeathMark,saved.Monster,difficulty)};
        ApplyPrepared(owner,replacement);
    }
    internal static void Prune(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Consume clock must be UTC");}
        foreach(var state in States.Values.ToArray())
        {
            if(World.FindMobile((Serial)state.Owner)is PlayerMobile owner)
            {
                Sync(owner,at);
                var current=State(owner);
                ConsumeAuras.Tick(owner,current,at);
                if(current!=null){WarlockRitualVisuals.Renew(owner,SpellEffectGraphics.ConsumeWard,at,current.UntilUtc);}
            }
            else{ConsumeAuras.Forget(state.Owner);States=States.Remove(state.Owner);}
        }
    }
    internal static void Enforce(PlayerMobile owner,BaseCreature[] bodies,int limit)
    {
        if(limit is<1 or>3){throw new NotSupportedException("Unexpected source demon-pool limit");}
        Sync(owner,TimedEffects.Now);
        var entries=bodies.Select(b=>(Order:BattleCommand.PetOrder(b),Tie:b.Serial.Value,Body:b)).ToList();
        if(State(owner)is{} state){entries.Add((state.Order,state.OriginalPet,null));}
        foreach(var entry in entries.OrderBy(x=>x.Order).ThenBy(x=>x.Tie).Take(Math.Max(0,entries.Count-limit)))
        {if(entry.Body is{} body){if(body is SkeletonGuardian original){DeathMarkWorld.Remove(original);}body.Delete();}else{Clear(owner);}}
    }
    internal static ConsumeEffect[] Snapshot()=>States.Values.OrderBy(s=>s.Owner).ToArray();
    internal static void Restore(ConsumeEffect[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Consume states");}
        // Native serials can be reused after retirement,including across saved
        // owners. The globally reserved creation order identifies the slot.
        var next=ImmutableDictionary<uint,ConsumeEffect>.Empty;var orders=new HashSet<long>();
        foreach(var state in saved)
        {Validate(state);if(next.ContainsKey(state.Owner)||!orders.Add(state.Order)){throw new InvalidDataException("Duplicate Consume owner/slot identity");}next=next.Add(state.Owner,state);}
        States=next;
    }
    internal static void AfterLoad()
    {Prune(TimedEffects.Now);foreach(var state in States.Values){BattleCommand.ReservePetOrder(state.Order);if(World.FindMobile((Serial)state.Owner)is PlayerMobile p){p.UpdateResistances();}}}
}
