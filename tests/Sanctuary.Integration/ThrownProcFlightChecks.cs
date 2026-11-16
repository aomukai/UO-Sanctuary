// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ThrownProcFlightChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ThrownProcFlightChecks));
    private sealed record Saved(uint Owner,uint Target,uint Primary,uint Secondary,uint Gloves,uint Helm,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int OwnerLife,int TargetLife,int Mana,int First,int Second,bool Initialized,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"thrown-proc-flight-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"thrown-proc-flight-reload-check":"thrown-proc-flight-check";ConsoleInputHandler.RegisterCommand(name,"Released proc-bearing missile and initialized/first-hit actor continuation",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature target)
    {
        Require(DoubleThrowChecks.Begin(p,target),"Learned saved throw entry");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Learned saved throw release");ClassCombat.Stop(p);return ThrownContactChecks.Flights(p).Single();
    }
    private static void Hold(PlayerMobile p,WeaponFlight flight)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(flight).ToArray()});}
    private static Saved Create(bool initialized,int generation,int row)
    {
        var p=ThrownContactChecks.Owner(row,out var primary,out var secondary,out var gloves,out var helm);ThrownContactChecks.Ready(p,primary,secondary);
        if(initialized)
        {
            var first=ThrownContactChecks.Target(p);
            using(PlayerItemProcWorld.Candidate(p)){Launch(p,first);ThrownContactChecks.Drain(p);}
            ThrownContactChecks.Clean(p,first);ThrownContactChecks.Ready(p,primary,secondary);
        }
        var target=ThrownContactChecks.Target(p);var profile=ThrownContactChecks.Own(p,primary);
        Require(profile.Owners.Length==(initialized?1:0),"Wrong pre-save actor initialization state");
        int life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);var world=NpcWorldAllocation.Capture();
        var flight=Launch(p,target);WeaponMissiles.Tick(flight.NextUtc,onlyOwner:p.Serial.Value);flight=ThrownContactChecks.Flights(p).Single();
        Require(flight.Frame>0&&(flight.X!=p.X||flight.Y!=p.Y)&&SkillHealth.Balance256(target)==life&&Json(ThrownContactChecks.Own(p,primary))==Json(profile)&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana-256,"Saved throw hit/procced early or repaid");
        flight=flight with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,flight);
        SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);var point=p.Location;p.Internalize();
        return new(p.Serial.Value,target.Serial.Value,primary.Serial.Value,secondary.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,
            Json(new[]{ClassGear.State(primary),ClassGear.State(secondary),ClassGear.State(gloves),ClassGear.State(helm)}),Json(ThrownContactChecks.Own(p,primary)),flight,
            SkillHealth.Balance256(p),life,NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(primary),NamedAmmunition.Remaining(secondary),initialized,generation);
    }
    private static void Resume(Saved s)
    {
        var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(PoisonDaggerContactChecks.Recipient)World.FindMobile((Serial)s.Target);
        var primary=(BaseWeapon)World.FindItem((Serial)s.Primary);var secondary=(BaseWeapon)World.FindItem((Serial)s.Secondary);var gloves=World.FindItem((Serial)s.Gloves);var helm=World.FindItem((Serial)s.Helm);
        Require(primary.Parent==p&&secondary.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(primary),ClassGear.State(secondary),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear
            &&Json(ThrownContactChecks.Own(p,primary))==s.Profile&&Json(ThrownContactChecks.Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.OwnerLife&&SkillHealth.Balance256(target)==s.TargetLife
            &&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(primary)==s.First&&NamedAmmunition.Remaining(secondary)==s.Second&&ClassCombat.For(p).DoubleThrow==null,"Saved proc flight/original/actor/resource state changed");
        // Restore native test-only resistance/frozen overrides after comparing the saved state.
        p.MoveToWorld(s.Point,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,100);}target.SetResistance(ResistanceType.Poison,0);target.SetResistance(ResistanceType.Fire,0);
        var profile=ThrownContactChecks.Own(p,primary);var actor=profile.Owners.SingleOrDefault();Require((actor!=null)==s.Initialized,"Saved actor init state changed");
        ulong world=NpcWorldAllocation.RequireStream().Value,expectedWorld=world;ulong expectedActor=actor?.Seed??SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));
        SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedWorld);int before=SkillHealth.Balance256(target);var visits=new List<PlayerProcWorldVisit>();var old=PlayerItemProcWorld.Observed;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(SkillHealth.Balance256(target)==before,"Restored missile proc ran after HP");visits.Add(v);}};
            using(PlayerItemProcWorld.Candidate(p))
            {
                Hold(p,s.Flight with{NextUtc=TimedEffects.Now});ThrownContactChecks.Drain(p);
                Require(visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.HitFlags==32&&visits[0].Packet.Poison256==153&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}
                    &&before-SkillHealth.Balance256(target)==153&&ThrownContactChecks.Own(p,primary).Owners.Single().Seed==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld
                    &&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(primary)==s.First&&NamedAmmunition.Remaining(secondary)==s.Second,"Restored throw lost proc/order/poison/streams or repaid resources");
                var spell=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);int afterParent=SkillHealth.Balance256(target);
                Require(spell.Rank==20&&spell.Minimum256==51072&&spell.Maximum256==57984,"Restored throw proc borrowed weapon damage");
                for(int i=0;i<80&&FireBoltChecks.State(spell.Id)is{} live;i++){SorceressSpellMissiles.Tick(live.NextUtc,true,live.Id);}
                Require(FireBoltChecks.State(spell.Id)==null&&SkillHealth.Balance256(target)<afterParent&&target.FindDamageEntryFor(p)?.Damager==p&&visits.Count==1
                    &&ThrownContactChecks.Own(p,primary).Owners.Single().Seed==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&NecromancerEffects.ManaBalance256(p)==s.Mana,
                    "Restored throw's emitted spell missed native damage/credit,recursed or charged again");
            }
        }
        finally{PlayerItemProcWorld.Observed=old;ThrownContactChecks.Clean(p,target);p.Delete();}
    }
    private static void Check(bool reload)
    {
        using var rolls=new ThrownContactChecks.Rolls();int generation=0;
        if(reload)
        {var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(saved.Length==2&&saved[0].Initialized==false&&saved[1].Initialized,"Missing saved actor contexts");generation=saved[0].Generation+1;Require(generation<=2&&saved.All(x=>x.Generation==generation-1),"Unexpected reload generation");foreach(var s in saved){Resume(s);}}
        File.WriteAllText(Manifest,Json(new[]{Create(false,generation,2500),Create(true,generation,2540)}));
    }
}
