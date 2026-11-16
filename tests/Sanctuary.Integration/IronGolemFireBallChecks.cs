// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class IronGolemFireBallChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(IronGolemFireBallChecks));
    private sealed record Saved(uint Owner,uint Body,uint Item,Point3D Point,string Recipe,string Gear,string Events,NpcEquipmentSeed Random,
        int Life,int Mana,uint Target,int TargetLife,SorceressSpellFlight Flight,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-fireball-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:GraveyardZombie
    {
        public int Notifications,Before;public uint Source;
        [Constructible]public Recipient(){CantWalk=true;Frozen=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;Before=SkillHealth.Balance256(this);Source=from.Serial.Value;base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);Require(r.ReadInt()==0,"Fire Ball recipient version");}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var prior=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="Iron Fire Ball owner [fixture]"&&prior?.Invoke(m)!=false;
    }
    public static void Configure(){Register("iron-golem-fireball-check",false);Register("iron-golem-fireball-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Iron Golem Fire Ball proc,area and saves",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,string id)
    {TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(var prior in row.Prerequisites){Train(p,prior);}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static Item Original()
    {
        // Existing plain original and explicit proc carrier. Whole Hellslayer
        // acquisition/projection remains unavailable; do not enable its ledger.
        var definition=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Hellslayer");
        Require(!NamedGear.Available(definition.Id),"Fixture enabled named acquisition");
        var item=new DoubleAxe();ClassGear.Profiles.Ensure(item);return item;
    }
    private static Recipient Target(IronGolemBody body,int dx,int dy=0,int fire=0)
    {
        var t=new Recipient();t.SetHits(65000);t.MoveToWorld(new(body.X+dx,body.Y+dy,body.Z),body.Map);t.AIObject.AITimer.Stop();
        foreach(var resistance in Enum.GetValues<ResistanceType>()){t.SetResistance(resistance,0);}t.SetResistance(ResistanceType.Fire,fire);
        TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static SorceressSpellFlight[] Flights(IronGolemBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static void Place(IronGolemBody b,PlayerMobile p)
    {b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var row=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,row with{Parked=false});Require(NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Original golem not attached");}
    private static ulong Seed(bool success)
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if((SourceRandom.Roll(ref copy)%100<10)==success){return seed;}}throw new InvalidOperationException("No chance seed");}
    private static SorceressSpellFlight Emit(IronGolemBody b,PlayerMobile p,Recipient target,DateTime at)
    {
        var graph=NpcItemProcs.Get(b);
        Require(graph!=null&&graph.Capture().Items.Single().Item==IronGolemItems.State(b).Item,"Original event registration absent");
        var before=NpcWorldAllocation.Capture();NpcEquipmentRandom.Set(b,Seed(false));ulong expected=NpcEquipmentRandom.State(b).Seed;SourceRandom.Roll(ref expected);
        graph.Dispatch("domeleeattack",new(0,0,0,0,0,HitFlags:32),b,target,at,null);
        Require(Flights(b).Length==0&&NpcEquipmentRandom.State(b).Seed==expected&&NpcWorldAllocation.Capture()==before,"Failed chance allocated a flight or changed wrong stream");
        NpcEquipmentRandom.Set(b,Seed(true));expected=NpcEquipmentRandom.State(b).Seed;SourceRandom.Roll(ref expected);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(b);
        graph.Dispatch("domeleeattack",new(0,0,0,0,0,HitFlags:32),b,target,at,null);
        var flight=Flights(b).Single();var packet=flight.Npc.Packet;
        Require(packet.Skill==47&&packet.Rank==20&&packet.Emissions==1&&packet.Element=="fire"&&flight.Owner==p.Serial.Value&&flight.Npc.Actor==b.Serial.Value
            &&flight.Modifiers==new FirestormModifiers(0,0,0,0,0)&&NpcEquipmentRandom.State(b).Seed==expected,"Fire Ball lost explicit proc rank/own packet/separate RNG");
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(b)==life,"Item spell spent owner mana or pet life");return flight;
    }
    private static void Travel(IronGolemBody b,PlayerMobile p,SorceressSpellFlight first,Recipient[] targets)
    {
        var lives=targets.Select(SkillHealth.Balance256).ToArray();ulong stream=NpcEquipmentRandom.State(b).Seed;
        int raw=SorceressSpellMissiles.Roll(first,out _);int crit=NecromancerSummons.Source("irongolem","Crit");
        for(int i=0;i<80&&Flights(b).Length!=0;i++){var current=Flights(b).Single();SorceressSpellMissiles.Tick(current.NextUtc,true,current.Id);}
        Require(Flights(b).Length==0,"Fire Ball outlived its source flight");
        for(int i=0;i<targets.Length;i++)
        {
            var t=targets[i];int damage=raw;if(i<3&&SourceRandom.Roll(ref stream)%100<crit){damage*=2;}
            int expected=i==2||i==3?0:i==1?damage*50/100:damage;
            Require(SkillHealth.Balance256(t)==lives[i]-expected,"Copied-area shared roll/resistance/radius differs: "+i);
            Require(expected==0?t.Notifications==0:t.Notifications==1&&t.Before==lives[i]&&t.Source==b.Serial.Value,"Area damage source/notification-before-HP differs");
        }
        Require(NpcEquipmentRandom.State(b).Seed==stream,"Area intrinsic-critical stream differs");
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;IronGolemBody b;Item original;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra Fire Ball reload");
            p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Body)as IronGolemBody;original=World.FindItem((Serial)s.Item);point=s.Point;
            var t=World.FindMobile((Serial)s.Target)as Recipient;
            Require(p!=null&&b!=null&&original?.Parent==b&&t!=null,"Saved original identities lost");
            Require(Json(b.Prepared.Recipe)==s.Recipe,"Saved original recipe changed");
            Require(Json(ClassGear.State(original))==s.Gear,"Saved original gear changed");
            Require(Json(NpcItemProcs.Get(b).Capture())==s.Events,"Saved original event graph changed");
            Require(NpcEquipmentRandom.State(b)==s.Random,"Saved original actor RNG changed");
            Require(SkillHealth.Balance256(b)==s.Life,"Saved original pet life changed");
            Require(NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved owner mana changed: "+NecromancerEffects.ManaBalance256(p)+" expected "+s.Mana);
            Require(SkillHealth.Balance256(t)==s.TargetLife,"Saved target life changed");
            Require(Json(Flights(b).Single())==Json(s.Flight),"Saved original flight changed");
            p.MoveToWorld(point,Map.Trammel);Place(b,p);var f=Flights(b).Single();SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);
            var aged=Flights(b).Single();Require(aged.Frame==f.Frame+1&&aged.X==f.X&&SkillHealth.Balance256(t)==s.TargetLife,"Loaded flight replayed movement/damage");
            for(int i=0;i<80&&Flights(b).Length!=0;i++){f=Flights(b).Single();SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);}
            Require(SkillHealth.Balance256(t)<s.TargetLife&&t.Source==b.Serial.Value,"Saved original Fire Ball did not resume");t.Delete();
        }
        else
        {
            p=new PlayerMobile{Body=0x190,Player=true,Name="Iron Fire Ball owner [fixture]"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"necromancer.irongolem");
            FixtureSpace.Corridor(p,6100,24,3500);point=p.Location;original=Original();original.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
            b=IronGolemWorld.Create(p,original,original.Location,TimedEffects.Now.AddHours(1),1);Place(b,p);
            var graph=NpcItemProcs.Create(b);graph.Add(original,IronGolemItems.Metadata(ClassGear.State(original).BaseCode).Body1,1);
            var property=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Hellslayer").Properties.Single(x=>x.Code=="att-skill");
            graph.Apply(original,EquipmentPropertyWriter.Plan(property.Code),new(property.SourceMin.Value,property.SourceMax.Value,NamedPropertyRuntime.Parameter(property)),ItemSkillProperties.ForLevel(99));
        }
        NecromancerEffects.SetMana256(p,0);var at=TimedEffects.Now.AddMinutes(10);
        var primary=Target(b,6);var near=Target(b,6,1,50);var immune=Target(b,7,0,100);var far=Target(b,6,3);
        try{var flight=Emit(b,p,primary,at);Travel(b,p,flight,[primary,near,immune,far]);}
        finally{SorceressSpellMissiles.Cancel(b);foreach(var t in new[]{primary,near,immune,far}){t.Delete();}}
        var held=Target(b,8);var pending=Emit(b,p,held,at);
        SkillHealth.SetBalance256(b,SkillHealth.Balance256(b)-257);int life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p);
        string events=Json(NpcItemProcs.Get(b).Capture());PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,original.Serial.Value,point,Json(b.Prepared.Recipe),Json(ClassGear.State(original)),events,
            NpcEquipmentRandom.State(b),life,mana,held.Serial.Value,SkillHealth.Balance256(held),pending,generation)));
        Log.Information("Original Iron Golem Fire Ball:unstripped pinned Hellslayer inspection item,real writer/registration/chance/world allocation,natural copied area,own source/RNG,no owner mana,immunity and radius;original wounds/item/events/projectile through reloads. Named acquisition,other procs and NPC terrain parity remain unqualified.");
    }
}
