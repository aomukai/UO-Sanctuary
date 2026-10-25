// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ThunderReleaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ThunderReleaseChecks));
    private sealed class FixedRandom:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed record Saved(uint Owner,uint Target,Point3D Origin,ThunderSave Flights,MartialCharge[] Charges,uint Gear,int Mana256,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"thunder-release-fixtures.json");
    private static bool Creating;
    private static void Rates(){var hits=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=p=>Creating || p.Name=="ThunderReleaseFixture"?TimeSpan.FromHours(1):hits?.Invoke(p)??Mobile.DefaultHitsRate;var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating || p.Name=="ThunderReleaseFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure(){Rates();Register("thunder-release-check",Check);Register("thunder-release-reload-check",Reload);Register("thunder-natural-check",Natural);}
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Thunder inline and real saved nova/bolt release",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=TigerStrikes.HitRoll;var elemental=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;var block=SourceBlocking.BlockRoll;var roll=MartialLightning.DamageRoll;var seed=MartialLightning.SeedLow;
        try{TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom();SourceBlocking.BlockRoll=_=>99;MartialLightning.DamageRoll=_=>0;MartialLightning.SeedLow=()=>7;action();}
        finally{TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=elemental;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;SourceBlocking.BlockRoll=block;MartialLightning.DamageRoll=roll;MartialLightning.SeedLow=seed;}
    }
    private static SkillValues Values(int rank,int synergy=0)=>new(SkillMechanics.For(CombatAbility.ClawsOfThunder),k=>k=="Claws of Thunder"?rank:0,k=>k=="Royal Strike"?synergy:0,99);
    private static PlayerMobile Fresh(int firstRow=3050)
    {Creating=true;try{var p=BladeBodyChecks.Fresh(7100,20,firstRow);p.Name="ThunderReleaseFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);Require(p.EquipItem(claw),"Claw");ClassGear.Refresh(p);return p;}finally{Creating=false;}}
    private static BaseCreature Target(PlayerMobile p,int offset=1)
    {var t=new CobraPassiveTarget{Name="ThunderReleaseTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.AIObject?.AITimer.Stop();t.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);t.SetHits(50000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static MartialCharge Charge(PlayerMobile p,int count=3,CombatAbility ability=CombatAbility.ClawsOfThunder)=>new(p.Serial.Value,ability,count,SkillMechanics.Rank(p,ability),count*50,TimedEffects.Now.AddHours(1));
    private static void Hold(PlayerMobile p,params MartialCharge[] charges)=>AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Concat(charges).ToArray());
    private static ThunderSave Own(PlayerMobile p)
    {var s=MartialLightning.Snapshot();var flights=s.Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();var casts=flights.Select(x=>x.Cast).ToHashSet();return s with{Flights=flights,Leases=s.Leases.Where(x=>casts.Contains(x.Cast)).ToArray()};}
    private static void Clear(PlayerMobile p)
    {var all=MartialLightning.Snapshot();var casts=all.Flights.Where(x=>x.Owner==p.Serial.Value).Select(x=>x.Cast).ToHashSet();MartialLightning.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).ToArray(),Leases=all.Leases.Where(x=>!casts.Contains(x.Cast)).ToArray()});}
    private static void TickOwner(PlayerMobile p,DateTime at,bool active=true)
    {
        var all=MartialLightning.Snapshot();var own=Own(p);var foreignCasts=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Select(x=>x.Cast).ToHashSet();ThunderSave after=own;
        MartialLightning.Restore(own);try{MartialLightning.Tick(at,active);after=MartialLightning.Snapshot();}
        finally{MartialLightning.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(after.Flights).OrderBy(x=>x.Id).ToArray(),Leases=all.Leases.Where(x=>foreignCasts.Contains(x.Cast)).Concat(after.Leases).OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray()});}
    }
    private static void Differential()
    {
        using var acceleration=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"missile-acceleration-source-probe.json")));int velocities=0;
        foreach(var r in acceleration.RootElement.EnumerateArray())
        {int V(int i)=>r[i].GetInt32();var v=MartialLightning.VelocityStep(V(0),V(1),V(2),V(3),V(4),V(5),V(6));Require(v==(V(7),V(8),V(9),V(10),V(11)),"Complete source acceleration/vector");velocities++;}
        Require(velocities==1800,"Acceleration source case count");
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"martial-lightning-source-probe.json")));int patterns=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(V(1)==0 || V(2)!=63 || V(4)!=0){continue;}
            ulong seed=SourceRandom.InitLow(V(3));int child=unchecked((int)SourceRandom.Roll(ref seed));var native=MartialLightning.Pattern(V(0),child);var expected=row[8].EnumerateArray().ToArray();Require(native.Length==expected.Length,"Source child count");
            for(int i=0;i<native.Length;i++)
            {var actual=native[i];var r=expected[i];Require(actual.X==r[2].GetInt32() && actual.Y==r[3].GetInt32() && actual.Bolt==(r[4].GetInt32()==558) && actual.Flags==r[7].GetInt32() && actual.Velocity==r[8].GetInt32() && actual.Seed==r[12].GetUInt32() && actual.Frames==r[14].GetInt32() && actual.TargetX==r[19].GetInt32(),"Full source nova/bolt seed/init arguments");}patterns++;
        }
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"thunder-damage-source-probe.json")));int packets=0;
        foreach(var row in damage.RootElement.EnumerateArray())
        {int V(int i)=>row[i].GetInt32();var packet=MartialLightning.Capture(Values(V(1),V(2)),3,V(3),0,_=>0);Require((V(0)==0?packet.NovaMin256:packet.BoltMin256)==V(5) && (V(0)==0?packet.NovaMax256:packet.BoltMax256)==V(6),"Full source child endpoints/mastery");packets++;}
        Require(patterns==18 && packets==576,"Thunder source/native matrix counts");
        for(int rank=1;rank<=255;rank++)
        {var values=Values(rank,20);var p=MartialLightning.Capture(values,3,25,0,_=>0);Require(p.Inline256==values.Damage256(false)*125L/100 && p.Rank==rank && p.Count==3,"Pinned rank/synergy/mastery capture");}
    }
    private static void Missiles(PlayerMobile p,BaseCreature t)
    {
        Clear(p);var at=TimedEffects.Now;int draws=0;MartialLightning.SeedLow=()=>{draws++;return 7;};var packet=new ThunderPacket(2,1,256,512,512,512,512,0);int hp=SkillHealth.Balance256(t);
        long first=MartialLightning.AfterParent(p,t.Location,p.Map,packet,at);Require(first>0 && Own(p).Flights.Length==64 && draws==0,"Nova count/owner seed");TickOwner(p,at.AddMilliseconds(40));Require(SkillHealth.Balance256(t)==hp-512 && !MartialLightning.Ready(first,t.Serial.Value,at.AddMilliseconds(199)) && MartialLightning.Ready(first,t.Serial.Value,at.AddMilliseconds(200)),"Shared4frame hit window or boundary");
        hp=SkillHealth.Balance256(t);long second=MartialLightning.AfterParent(p,t.Location,p.Map,packet,at);TickOwner(p,at.AddMilliseconds(40));Require(second!=first && SkillHealth.Balance256(t)==hp-512,"Separate trigger suppressed or same trigger duplicated");Clear(p);
        long cast=MartialLightning.AfterParent(p,t.Location,p.Map,packet with{Count=3,NovaMax256=1024},at);var flights=Own(p).Flights;Require(flights.Length==80 && flights.Count(x=>x.Bolt)==16 && draws==1 && flights.Where(x=>x.Bolt).All(x=>x.Frames==77 && x.Path.Length==39),"Bolt emission/child path/owner draws");
        var nova=flights.First(x=>!x.Bolt);var bolt=flights.First(x=>x.Bolt);var firstHit=MartialLightning.Impact(p,t,nova);var other=Target(p,2);var nextHit=MartialLightning.Impact(p,other,nova with{Seed=firstHit.Seed});Require(nextHit.Seed!=firstHit.Seed,"Persistent nova restarted its damage RNG");
        Require(!MartialLightning.Ready(cast,t.Serial.Value,at.AddMilliseconds(40)) && MartialLightning.Recipient(p,p.Map,bolt,new(t.X*2,t.Y*2))==null,"Nova and bolts did not share trigger hit window");other.Delete();Clear(p);
        hp=SkillHealth.Balance256(t);MartialLightning.AfterParent(p,t.Location,p.Map,packet with{Count=3},at);TickOwner(p,at.AddSeconds(4),false);Require(Own(p).Flights.Length==0 && SkillHealth.Balance256(t)==hp,"Offline missiles fought or lifetime stopped");
        MartialLightning.AfterParent(p,t.Location,p.Map,packet,TimedEffects.Now);var origin=p.Location;p.MoveToWorld(origin,Map.Felucca);TickOwner(p,TimedEffects.Now.AddSeconds(1));Require(Own(p).Flights.Length==0,"Facet change retained player projectile");p.MoveToWorld(origin,Map.Trammel);
        MartialLightning.AfterParent(p,t.Location,p.Map,packet,TimedEffects.Now);var valid=MartialLightning.Snapshot();bool refused=false;
        try{MartialLightning.Restore(valid with{Flights=valid.Flights.Select(x=>x.Owner==p.Serial.Value?x with{Frames=99}:x).ToArray()});}catch(InvalidDataException){refused=true;}finally{MartialLightning.Restore(valid);}Require(refused,"Corrupt lifetime accepted");Clear(p);
    }
    private static Item Parent(PlayerMobile p,BaseCreature t)
    {
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Griffon's Eye").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual Griffon");ClassGear.Refresh(p);Hold(p,Charge(p));var state=AssassinCharges.State(p,CombatAbility.ClawsOfThunder);var packet=MartialLightning.Capture(p,state);Require(packet.Rank==SkillMechanics.Rank(p,CombatAbility.ClawsOfThunder) && NamedElements.MasteryPercent(p,"ltng")>0 && packet.Pierce>0,"Actual current gear rank/mastery/pierce");
        t.SetResistance(ResistanceType.Physical,100);t.SetResistance(ResistanceType.Energy,50);int hp=SkillHealth.Balance256(t);var expected=NamedElements.Resolve256(p,t,packet.Inline256,"ltng",enhance:false);Require(TigerStrikes.BasicStrike(p,t),"Ordinary Thunder finisher");CombatDamage.Finish(t,p);
        Require(hp-SkillHealth.Balance256(t)==expected.Damage256 && AssassinCharges.State(p,CombatAbility.ClawsOfThunder)?.Count==2 && Own(p).Flights.Length==80,"Inline damage/mastery duplication/consume/child factory");Clear(p);t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Energy,0);
        Hold(p,Charge(p));state=AssassinCharges.State(p,CombatAbility.ClawsOfThunder);TigerStrikes.HitRoll=()=>1;Require(!TigerStrikes.BasicStrike(p,t) && AssassinCharges.State(p,CombatAbility.ClawsOfThunder)==state && Own(p).Flights.Length==0,"Miss consumed/released");TigerStrikes.HitRoll=()=>0;
        Hold(p,Charge(p),Charge(p,2,CombatAbility.TigerStrike),Charge(p,3,CombatAbility.CobraStrike),Charge(p,3,CombatAbility.FistsOfFire));Require(TigerStrikes.BasicStrike(p,t),"Stacked finisher");CombatDamage.Finish(t,p);Require(AssassinCharges.Finisher(p).Length==4 && AssassinCharges.State(p)?.Count==1 && AssassinCharges.State(p,CombatAbility.ClawsOfThunder)?.Count==2 && AssassinCharges.State(p,CombatAbility.FistsOfFire)?.Count==2 && AssassinCharges.State(p,CombatAbility.CobraStrike)?.Count==2,"Independent stacked family consumption");Clear(p);FireGround.Cancel(p);
        var dying=Target(p);Hold(p,Charge(p));SkillHealth.SetBalance256(dying,1);Require(TigerStrikes.BasicStrike(p,dying),"Lethal parent hit");CombatDamage.Finish(dying,p);Require(!dying.Alive && Own(p).Flights.Length==80,"Parent death suppressed children");dying.Corpse?.Delete();dying.Delete();Clear(p);return helm;
    }
    private static void Natural()
    {
        var p=Fresh(3300);var primary=Target(p);var secondary=Target(p,3);
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        Hold(p,Charge(p));SkillHealth.SetBalance256(primary,1);int hp=SkillHealth.Balance256(secondary),mana=NecromancerEffects.ManaBalance256(p);
        Require(TigerStrikes.BasicStrike(p,primary),"Natural lethal ordinary finisher");CombatDamage.Finish(primary,p);Require(!primary.Alive && Own(p).Flights.Length==80,"Natural parent/child factory");
        long deadline=Core.TickCount+6000;bool damaged=false;
        void Cleanup(){Clear(p);primary.Corpse?.Delete();primary.Delete();secondary.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Thunder lifetime timeout");Require(Own(p).Flights.All(x=>x.Fault==null),"Natural missile fault");
                damaged|=SkillHealth.Balance256(secondary)<hp;
                if(Own(p).Flights.Length==0)
                {Require(damaged && NecromancerEffects.ManaBalance256(p)==mana && AssassinCharges.State(p,CombatAbility.ClawsOfThunder)?.Count==2,"Natural damaging release/expiry/payment/consumption");Cleanup();Log.Information("thunder-natural-result: passed");return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
            }
            catch(Exception e){Cleanup();Log.Error(e,"thunder-natural-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
    private static void Check()
    {
        Differential();var p=Fresh();var target=Target(p);Missiles(p,target);var helm=Parent(p,target);Hold(p,Charge(p),Charge(p,2,CombatAbility.FistsOfFire));var at=TimedEffects.Now;var packet=MartialLightning.Capture(p,AssassinCharges.State(p,CombatAbility.ClawsOfThunder));MartialLightning.AfterParent(p,target.Location,p.Map,packet,at);TickOwner(p,at.AddMilliseconds(160));
        // Held payloads make reload coverage independent of the full suite's
        // duration; the natural77/13frame lifetimes were checked separately.
        var all=MartialLightning.Snapshot();var ownCasts=Own(p).Flights.Select(x=>x.Cast).ToHashSet();var shift=TimedEffects.Now.AddHours(1)-Own(p).Flights.Min(x=>x.NextUtc);MartialLightning.Restore(all with{Flights=all.Flights.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc+shift}:x).ToArray(),Leases=all.Leases.Select(x=>ownCasts.Contains(x.Cast)?x with{UntilUtc=x.UntilUtc+shift}:x).ToArray()});
        p.AddToBackpack(helm);ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,12345);SkillHealth.SetBalance256(p,5432);var own=Own(p);Require(own.Flights.Length>0 && own.Flights.Any(x=>!x.Bolt && x.Frame==4 && x.AccelerationCounter==4),"Saved partial flight");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,p.Location,own,AssassinCharges.Finisher(p),helm.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p))));
        Log.Information("Thunder release:18 exact emitted-pattern contexts,576 complete child endpoint cases,255 rank captures,actual mastery/pierce/inline/stacked families,real nova damage/shared hit windows,offline expiry,facet/lethal/seed/corrupt-save checks pass");
    }
    private static void Reload()
    {
        Differential();var save=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)save.Owner);var t=(BaseCreature)World.FindMobile((Serial)save.Target);
        Require(JsonSerializer.Serialize(Own(p))==JsonSerializer.Serialize(save.Flights with{NextId=Own(p).NextId,NextCast=Own(p).NextCast}),"Saved exact missile payload/path/seed/clock/lease");Require(AssassinCharges.Finisher(p).SequenceEqual(save.Charges) && NecromancerEffects.ManaBalance256(p)==save.Mana256 && SkillHealth.Balance256(p)==save.Life256,$"Saved charges/resources/injury: charges={AssassinCharges.Finisher(p).SequenceEqual(save.Charges)} mana={NecromancerEffects.ManaBalance256(p)}/{save.Mana256} life={SkillHealth.Balance256(p)}/{save.Life256}");Require(World.FindItem((Serial)save.Gear).Parent==p.Backpack,"Saved removed gear");p.MoveToWorld(save.Origin,Map.Trammel);
        var before=Own(p);int hp=SkillHealth.Balance256(t);TickOwner(p,before.Flights.Min(x=>x.NextUtc));Require(Own(p).Flights.Any(x=>!x.Bolt && x.Frame==5 && x.VelocityFp8==3608 && x.AccelerationCounter==0) && SkillHealth.Balance256(t)<=hp,"Restored missile did not advance");
        var all=MartialLightning.Snapshot();var foreign=all.Flights.Where(x=>x.Owner!=p.Serial.Value).ToArray();var casts=foreign.Select(x=>x.Cast).ToHashSet();MartialLightning.Restore(all with{Flights=foreign.Concat(save.Flights.Flights).OrderBy(x=>x.Id).ToArray(),Leases=all.Leases.Where(x=>casts.Contains(x.Cast)).Concat(save.Flights.Leases).OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray()});p.Internalize();
    }
}
