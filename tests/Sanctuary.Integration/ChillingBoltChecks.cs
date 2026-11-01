// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ChillingBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChillingBoltChecks));
    private sealed record Saved(uint Owner,uint Target,uint Orb,int Map,Point3D Point,int Mana,int OwnerLife,int TargetLife,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"chilling-bolt-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="ChillingBoltFixture"&&prior?.Invoke(p)!=false;
        Register("chilling-bolt-check",Check);Register("chilling-bolt-reload-check",()=>FixtureNetwork.Connected("chilling-bolt-reload-result",Reload));
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()
    {var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="ChillingBoltFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Chilling Armor reactive saved missile",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {var p=ColdArmorChecks.Owner(CombatAbility.ChillingArmor);p.Name="ChillingBoltFixture";FixtureSpace.Corridor(p,5870,32,3096);return p;}
    private static void Train(PlayerMobile p,CombatAbility a,int rank)
    {string id=SkillMechanics.For(a).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Chilling train "+id);}}
    private static ColdArmorState Armor(PlayerMobile p)
    {Require(SorceressCombat.Release(p,CombatAbility.ChillingArmor,Serial.Zero,p.Location),"Chilling paid cast");return SorceressEffects.Armor(p);}
    private static SorceressSpellFlight Flight(PlayerMobile p)=>SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
    private static void Clear(PlayerMobile p)=>SorceressSpellMissiles.Cancel(p);
    private static void Check()
    {
        Goldens();Factory();Capture();Delivery();Native();Save();
        Log.Information("Chilling bolt:765 rank/hard goldens,36 complete event/factory comparisons,reactive no-mana/ReturnFire exclusion,retained-rank live-hard/gear capture,typed immunity/potion/cold and original saved flight pass");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ChillingArmor);
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            int Tier(int basis,int step){int n=rank-1;return basis+Math.Min(n,7)*step+Math.Min(Math.Max(0,n-7),8)*(step+2)
                +Math.Min(Math.Max(0,n-15),6)*(step+4)+Math.Min(Math.Max(0,n-21),6)*(step+6)+Math.Max(0,n-27)*(step+8);}
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:255,_=>hard,99);
            Require(v.Damage256(false)==(long)Tier(16,8)*128*(100+18*hard)/100
                &&v.Damage256(true)==(long)Tier(20,9)*128*(100+18*hard)/100
                &&v.DurationFrames==100+Math.Min(Math.Max(0,rank-8),8)*25+Math.Max(0,rank-16)*50,"Independent Chilling damage/length tiers");
        }
    }
    private static void Factory()
    {
        var p=Owner();var held=AmazonSlowMissiles.Snapshot();int count=0;
        try
        {
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"chilling-armor-source-cases.json")));
            foreach(var r in source.RootElement.EnumerateArray())
            {
                int V(int i)=>r[i].GetInt32();if(V(0)!=0||V(1)!=0||V(2)<1){continue;}
                AmazonSlowMissiles.Clear(p);if(V(5)!=0){AmazonSlowMissiles.Restore(held.Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,100,TimedEffects.Now.AddMinutes(1))).ToArray());}
                var v=SkillMechanics.Values(p,CombatAbility.ChillingArmor,V(2));var armor=SorceressEffects.PrepareColdArmor(CombatAbility.ChillingArmor,v,TimedEffects.Now.AddMinutes(5));
                int mana=NecromancerEffects.ManaBalance256(p);
                Require(SorceressSpellMissiles.ReactiveArmor(p,new(p.X+V(3)/2,p.Y+V(4)/2,p.Z),armor),"Reactive factory emission");
                var s=Flight(p);Require(s.Rank==V(19)&&s.VelocityFp8==V(16)&&s.Frames==V(17)&&s.Frame==0&&s.Ability==CombatAbility.ChillingArmor
                    &&s.Steps.Cells.SequenceEqual(new[]{new SourceMissileCell(p.X*2,p.Y*2)})&&NecromancerEffects.ManaBalance256(p)==mana,"Complete factory rank/velocity/lifetime/origin/no payment");
                Clear(p);count++;
            }
            Require(count==36,"Native factory comparison count");
            var a=Armor(p);int before=NecromancerEffects.ManaBalance256(p);
            foreach(string key in new[]{"chillingarmorbolt","unknown-missile"}){SorceressEffects.MissileHit(p,p,key);Require(!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Self/nonreturning missile created loop");}
            // Rankless historical state is carried as rank0. Fixed velocity and
            // lifetime require no guessed rank; captured damage/length survive.
            Require(SorceressSpellMissiles.ReactiveArmor(p,new(p.X+6,p.Y,p.Z),a with{Rank=0,Min256=1234,Max256=2345,ColdFrames=137}),"Legacy reactive packet");
            Require(Flight(p) is{Rank:0,Minimum256:1234,Maximum256:2345,ColdFrames:137}&&NecromancerEffects.ManaBalance256(p)==before,"Legacy armor invented current rank/values");
            Clear(p);
        }
        finally{AmazonSlowMissiles.Restore(held);FireBoltChecks.Clean(p);}
    }
    private static void Capture()
    {
        var p=Owner();var enemy=FireBoltChecks.Target(p,8);
        try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");var armor=Armor(p);
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Train(p,CombatAbility.FrozenArmor,20);
            SorceressEffects.MissileHit(enemy,p,"arrow");var first=Flight(p);var v=SkillMechanics.Values(p,CombatAbility.ChillingArmor,armor.Rank);
            var ends=DruidFirestorm.Endpoints(v,NamedElements.MasteryPercent(p,"cold"),SlowMissilePayload.Normal);
            Require(armor.Rank>1&&SkillMechanics.Rank(p,CombatAbility.ChillingArmor)<armor.Rank&&first.Rank==armor.Rank
                &&first.Minimum256==ends.Min&&first.Maximum256==ends.Max&&first.Modifiers.ColdMastery>0,"Retained rank/live hard synergy/current positive cold enhancement");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Train(p,CombatAbility.ShiverArmor,20);
            Require(Flight(p)==first,"Existing packet reread later gear/hard changes");Clear(p);
            SorceressEffects.MissileHit(enemy,p,"arrow");var next=Flight(p);
            Require(next.Rank==armor.Rank&&next.Modifiers.ColdMastery==0&&next.Minimum256!=first.Minimum256,"Fresh packet retained stale gear/hard context");
            SorceressEffects.Profiles.Store(p,(SorceressEffects.Profiles.Find(p)??new()) with{Armor=armor with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}});
            int life=SkillHealth.Balance256(enemy);FireBoltChecks.Through(next.Id,25);
            Require(FlightOrNull(next.Id)==null&&SkillHealth.Balance256(enemy)<life,"Armor expiry cancelled an already emitted bolt");
        }
        finally{FireBoltChecks.Clean(p,enemy);}
    }
    private static SorceressSpellFlight FlightOrNull(long id)=>FireBoltChecks.State(id);
    private static void Delivery()
    {
        foreach(var (resistance,potion,expected) in new[]{(0,false,25600),(50,false,12800),(100,false,0),(100,true,7680),(0,true,35840)})
        {
            var p=Owner();var target=FireBoltChecks.Target(p,6);
            try
            {
                var a=Armor(p);target.SetResistance(ResistanceType.Cold,resistance);
                var prepared=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.ChillingArmor),TimedEffects.Now,reaction:a)
                    with{Minimum256=25600,Maximum256=25600};long id=SorceressSpellMissiles.Commit(prepared);
                if(potion){Require(CombatPotions.Apply(p,3),"Impact potion");}int hp=SkillHealth.Balance256(target);FireBoltChecks.Through(id,25);
                Require(FlightOrNull(id)==null&&hp-SkillHealth.Balance256(target)==expected,"Chilling typed damage/immunity/live potion");
                Require((TimedEffects.Chill(target,TimedEffects.Now)>0)==(resistance<100),"Cold immune potion target got ordinary chill");
            }
            finally{FireBoltChecks.Clean(p,target);}
        }
    }
    private static void Native()
    {
        var p=Owner();var target=FireBoltChecks.Target(p,6);
        try
        {
            Armor(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target);
            SorceressEffects.MissileHit(target,p,"chillingarmorbolt");Require(!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"ReturnFire0 recursed");
            AOS.Damage(p,target,1,false,100,0,0,0,0,archer:true,weaponDamage:true);var s=Flight(p);
            Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(target)==hp,"Native arrow instant retaliation or additional mana");
            SorceressSpellMissiles.Tick(s.NextUtc,false);
            Require(FlightOrNull(s.Id) is{} aged&&aged.Frame==1&&aged.X==s.X&&aged.Y==s.Y&&SkillHealth.Balance256(target)==hp,"Offline return bolt moved/dealt damage instead of harmless aging");
            FireBoltChecks.Through(s.Id,25);Require(SkillHealth.Balance256(target)<hp&&FlightOrNull(s.Id)==null,"Native arrow failed saved travelling return");
            SorceressEffects.MissileHit(target,p,"chillingarmorbolt");Require(!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Finished return allowed recursion");
        }
        finally{FireBoltChecks.Clean(p,target);}
    }
    private static void Hold(SorceressSpellFlight flight)
    {
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==flight.Id?flight:x).ToArray()});
    }
    private static void Save()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,8);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Armor(p);
        AOS.Damage(p,t,1,false,100,0,0,0,0,archer:true,weaponDamage:true);
        var held=Flight(p) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(held);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,orb.Serial.Value,p.Map.MapID,p.Location,
            NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(t),held,0)));p.Internalize();
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(saved.Generation<2&&FlightOrNull(saved.Flight.Id)==saved.Flight&&World.FindItem((Serial)saved.Orb)?.Parent==p
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.OwnerLife&&SkillHealth.Balance256(target)==saved.TargetLife,"Original saved bolt/gear/resources/recipient wounds changed");
        target.AIObject.AITimer.Stop();var account=FixtureNetwork.Authenticate(network,p,"chilling-bolt-reload-");p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);
        var resumed=saved.Flight with{NextUtc=TimedEffects.Now.AddMilliseconds(100)};Hold(resumed);long deadline=Core.TickCount+4000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Chilling original resume timeout");var s=FlightOrNull(saved.Flight.Id);
                if(saved.Generation==0)
                {
                    if(s is not{Frame:>=3}){Require(s!=null,"Original bolt hit before inspection boundary");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                    Require(s.X>saved.Flight.X&&SkillHealth.Balance256(target)==saved.TargetLife,"Original flight did not move without premature damage");
                    var held=s with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(held);p.Internalize();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Generation=1}));
                }
                else
                {
                    if(s!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                    Require(SkillHealth.Balance256(target)<saved.TargetLife&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.OwnerLife,"Original final hit changed source resources or missed recipient");
                    p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{TargetLife=SkillHealth.Balance256(target),Generation=2}));
                }
                network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();Log.Information("chilling-bolt-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"chilling-bolt-reload-result: failed");}
        }
        FixtureClock.NextLoop("chilling-bolt-reload-result",Observe);
    }
}
