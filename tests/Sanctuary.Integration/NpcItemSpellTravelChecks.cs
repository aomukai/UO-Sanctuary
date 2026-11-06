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

public static class NpcItemSpellTravelChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemSpellTravelChecks));
    private sealed record Contact(long Child,long Cast,int Skill,int Frame,DateTime At,uint Target,uint Source,int Before);
    private sealed record Scope(SorceressSpellFlight Flight,List<Contact> Contacts);
    private sealed record TargetState(int Skill,uint Actor,int Life);
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,
        TargetState[] Targets,SorceressSpellFlight[] Flights,int Generation=0);
    private static Scope Current;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-spell-travel-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:GraveyardZombie
    {
        [Constructible]public Recipient(){CantWalk=true;Frozen=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {
            if(Current is{} scope)
            {
                var f=scope.Flight;scope.Contacts.Add(new(f.Id,f.Cast,f.Npc.Packet.Skill,f.Frame+1,f.NextUtc,Serial.Value,from?.Serial.Value??0,SkillHealth.Balance256(this)));
            }
            base.OnDamage(amount,from,willKill);
        }
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);Require(r.ReadInt()==0,"Travel recipient version");}
    }
    public static void Configure(){Register("npc-item-spell-travel-check",false);Register("npc-item-spell-travel-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Natural original NPC charged/ring contacts,delay leases and saved partial travel",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SorceressSpellFlight[] Own(AmazonValkyrieBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static void Place(AmazonValkyrieBody b,PlayerMobile p)
    {
        b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();var row=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,row with{Parked=false});
        Require(NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Travel original source not active");
    }
    private static Recipient Target(AmazonValkyrieBody b,int dx,int dy=0)
    {
        var t=new Recipient();t.SetHits(65000);t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();
        foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static SorceressSpellFlight[] Emit(AmazonValkyrieBody b,int skill,DateTime at)
    {
        var f=SorceressSpellMissiles.PrepareValkyrie(b,new(b.X+10,b.Y,b.Z),skill,3,at,i=>SourceRandom.InitLow(1700+i));
        Require(f.All(x=>x.Npc.Packet.FirePierce==0&&x.Npc.Packet.ColdPierce==0&&x.Npc.Packet.LightningPierce==0),"Fixture original packet needs explicit zero-pierce arithmetic controls");
        SorceressSpellMissiles.CommitValkyrie(f);return f;
    }
    private static void Step(SorceressSpellFlight f,List<Contact> contacts,bool actions=true)
    {
        var old=Current;Current=new(f,contacts);try{SorceressSpellMissiles.Tick(f.NextUtc,actions,f.Id);}finally{Current=old;}
    }
    private static void Frame(AmazonValkyrieBody b,List<Contact> contacts,Func<SorceressSpellFlight,bool> select=null)
    {
        foreach(var f in Own(b).Where(x=>select?.Invoke(x)??true).OrderBy(x=>x.NextUtc).ThenByDescending(x=>x.Id)){Step(f,contacts);}
    }
    private static void Through(AmazonValkyrieBody b,List<Contact> contacts,int frames,Func<SorceressSpellFlight,bool> select=null)
    {for(int i=0;i<frames&&Own(b).Any(x=>select?.Invoke(x)??true);i++){Frame(b,contacts,select);}}
    private static void Verify(AmazonValkyrieBody b,NpcEquipmentSeed start,SorceressSpellFlight[] flights,List<Contact> contacts,Recipient[] targets,int[] initial)
    {
        var seeds=flights.ToDictionary(f=>f.Id,f=>f.Seed);var packets=flights.ToDictionary(f=>f.Id);var lives=targets.Select((t,i)=>(t.Serial.Value,Life:initial[i])).ToDictionary(x=>x.Value,x=>x.Life);ulong critical=start.Seed;
        Require(NecromancerSummons.Source("valkyrie","Crit")==5,"Pinned intrinsic critical changed");
        foreach(var hit in contacts)
        {
            var f=packets[hit.Child];int raw=SorceressSpellMissiles.Roll(f.Minimum256,f.Maximum256,seeds[hit.Child],out ulong next);seeds[hit.Child]=next;
            if(SourceRandom.Roll(ref critical)%100<5){raw=checked(raw*2);}
            Require(hit.Source==b.Serial.Value&&hit.Before==lives[hit.Target],"Natural contact source or notification-before-HP differs");lives[hit.Target]-=raw;
        }
        Require(targets.All(t=>SkillHealth.Balance256(t)==lives[t.Serial.Value]),"Natural contacts differ from independent child rolls and actor critical stream");
        Require(NpcEquipmentRandom.State(b)==start with{Seed=critical},"Natural travel drew actor RNG outside accepted contacts");
        foreach(var group in contacts.Where(c=>c.Cast>0).GroupBy(c=>(c.Cast,c.Target)))
        {var rows=group.ToArray();for(int i=1;i<rows.Length;i++){Require(rows[i].At-rows[i-1].At>=TimeSpan.FromMilliseconds(160),"Same-cast ring contacts ignored their four-frame delay");}}
    }
    private static void Leases(List<Contact> contacts)
    {
        foreach(var group in contacts.Where(c=>c.Cast>0).GroupBy(c=>(c.Cast,c.Target)))
        {
            var rows=group.ToArray();for(int i=1;i<rows.Length;i++){Require(rows[i].At-rows[i-1].At>=TimeSpan.FromMilliseconds(160),"Same-cast ring rays bypassed their shared four-frame delay");}
            var last=rows[^1];Require(!SorceressSpellMissiles.Ready(last.Cast,last.Target,last.At.AddMilliseconds(159))
                &&SorceressSpellMissiles.Ready(last.Cast,last.Target,last.At.AddMilliseconds(160)),"Natural ring lease lost its exact boundary");
        }
    }
    private static void Charged(AmazonValkyrieBody b)
    {
        var near=Target(b,0);var behind=Target(b,4);var contacts=new List<Contact>();
        try
        {
            var f=Emit(b,38,TimedEffects.Now);var random=NpcEquipmentRandom.State(b);var lives=new[]{SkillHealth.Balance256(near),SkillHealth.Balance256(behind)};
            Through(b,contacts,80);Require(Own(b).Length==0&&contacts.Count==5&&contacts.All(c=>c.Target==near.Serial.Value)&&contacts.Select(c=>c.Child).Distinct().Count()==5,
                "Natural charged volley did not deliver five independent,once-only opening contacts");
            Verify(b,random,f,contacts,[near,behind],lives);
        }
        finally{SorceressSpellMissiles.Cancel(b);near.Delete();behind.Delete();}
        contacts=[];
        var shots=Emit(b,38,TimedEffects.Now);long chosen=shots[0].Id;var seed=NpcEquipmentRandom.State(b);
        try
        {
            Through(b,contacts,7,f=>f.Id==chosen);var partial=Own(b).Single(f=>f.Id==chosen);
            Require(partial.Frame==7&&partial.Charged.Next>1&&(partial.X!=shots[0].X||partial.Y!=shots[0].Y)&&contacts.Count==0&&NpcEquipmentRandom.State(b)==seed,"Charged path failed to advance independently without a target");
            var next=partial.Charged.Path[partial.Charged.Next];var target=Target(b,10);target.MoveToWorld(new(partial.Charged.OriginX+next.X,partial.Charged.OriginY+next.Y,b.Z),b.Map);
            try
            {
                int hp=SkillHealth.Balance256(target);Through(b,contacts,80,f=>f.Id==chosen);
                Require(!Own(b).Any(f=>f.Id==chosen)&&contacts.Count==1&&contacts[0].Child==chosen&&SkillHealth.Balance256(target)<hp
                    &&Own(b).All(f=>f.Frame==0),"Moving charged path missed its next waypoint target or advanced unrelated bolts");
                Verify(b,seed,[partial],contacts,[target],[hp]);
            }
            finally{target.Delete();}
        }
        finally{SorceressSpellMissiles.Cancel(b);}
    }
    private static void Rings(AmazonValkyrieBody b)
    {
        foreach(int skill in new[]{48,44})foreach(int count in new[]{1,2})
        {
            var east=Target(b,2);var north=Target(b,0,2);var far=Target(b,10);var targets=new[]{east,north,far};var contacts=new List<Contact>();
            try
            {
                var flights=new List<SorceressSpellFlight>();var now=TimedEffects.Now;for(int i=0;i<count;i++){flights.AddRange(Emit(b,skill,now));}
                var random=NpcEquipmentRandom.State(b);var lives=targets.Select(SkillHealth.Balance256).ToArray();
                for(int frame=0;frame<20&&Own(b).Length>0;frame++)
                {
                    int before=contacts.Count;Frame(b,contacts);
                    if(contacts.Count>before&&Own(b).Length>0){Leases(contacts.Skip(before).ToList());}
                }
                Require(Own(b).Length==0&&contacts.Any(c=>c.Target==east.Serial.Value)&&contacts.Any(c=>c.Target==north.Serial.Value)&&contacts.All(c=>c.Target!=far.Serial.Value),"Natural NPC ring missed nearby enemies or exceeded its travel range");
                foreach(var t in new[]{east,north}){Require(contacts.Where(c=>c.Target==t.Serial.Value).Select(c=>c.Cast).Distinct().Count()==count,"Separate ring casts shared a target delay");}
                if(count==2){Require(contacts.GroupBy(c=>(c.Target,c.Frame)).Any(g=>g.Select(c=>c.Cast).Distinct().Count()==2),"Independent casts never landed within the same frame");}
                Verify(b,random,flights.ToArray(),contacts,targets,lives);
                if(skill==44){Require(TimedEffects.TryActor(east,out var state)&&state.Cold?.Source==b.Serial.Value,"Natural Frost Nova lost NPC cold ownership");}
                else{Require(!TimedEffects.TryActor(east,out var state)||state.Cold==null,"Natural lightning ring applied cold");}
            }
            finally{SorceressSpellMissiles.Cancel(b);foreach(var t in targets){t.Delete();}}
        }
    }
    private static void Resume(AmazonValkyrieBody b,Saved s)
    {
        foreach(int skill in new[]{38,48,44})
        {
            var entry=s.Targets.Single(t=>t.Skill==skill);var target=(Recipient)World.FindMobile((Serial)entry.Actor);var contacts=new List<Contact>();
            var flights=Own(b).Where(f=>f.Npc.Packet.Skill==skill).ToArray();var seed=NpcEquipmentRandom.State(b);int hp=SkillHealth.Balance256(target);
            foreach(var f in flights){Step(f,contacts);}
            var aged=Own(b).Where(f=>f.Npc.Packet.Skill==skill).ToArray();
            Require(contacts.Count==0&&NpcEquipmentRandom.State(b)==seed&&SkillHealth.Balance256(target)==hp&&aged.Zip(flights).All(pair=>pair.First.Frame==pair.Second.Frame+1&&pair.First.X==pair.Second.X&&pair.First.Y==pair.Second.Y),"Reload replayed old movement,contacts or RNG");
            if(skill==38)
            {
                var chosen=aged.Single(f=>f.Frame==8);var c=chosen.Charged;var next=c.Path[c.Next];target.MoveToWorld(new(c.OriginX+next.X,c.OriginY+next.Y,b.Z),b.Map);
                Through(b,contacts,80,f=>f.Id==chosen.Id);Require(contacts.Count==1&&contacts[0].Child==chosen.Id,"Saved moving charged bolt did not resume a single real hit");
                foreach(var f in Own(b).Where(f=>f.Npc.Packet.Skill==38).ToArray())
                {SorceressSpellMissiles.Tick(f.NextUtc.AddMilliseconds(80*40),false,f.Id);}
            }
            else
            {
                target.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);Through(b,contacts,20,f=>f.Npc.Packet.Skill==skill);
                Require(contacts.Count>0&&contacts.All(c=>c.Target==target.Serial.Value),"Saved partial ring did not resume a real hit");
                if(skill==44){Require(TimedEffects.TryActor(target,out var state)&&state.Cold?.Source==b.Serial.Value,"Resumed Frost Nova lost actual source");}
            }
            Require(!Own(b).Any(f=>f.Npc.Packet.Skill==skill),"Saved emission did not retire");Verify(b,seed,aged,contacts,[target],[hp]);target.Delete();
        }
        Require(Own(b).Length==0,"Saved natural flights leaked");
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra travel reload");
            p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false},"Saved natural source/owner lost");point=s.Point;
            Require(b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life
                &&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&Json(Own(b))==Json(s.Flights)&&s.Targets.All(t=>World.FindMobile((Serial)t.Actor)is Recipient r&&SkillHealth.Balance256(r)==t.Life),"Actual partial travel/items/resources/wounds/seed changed on reload");
            p.MoveToWorld(point,Map.Trammel);Place(b,p);Resume(b,s);
        }
        else
        {
            p=new PlayerMobile{Name="NpcItemSpellTravelFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6670,20,3680);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var c=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,c);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);Place(b,p);
        }
        Charged(b);Rings(b);
        var held=new List<TargetState>();var at=TimedEffects.Now.AddMinutes(10);var empty=new List<Contact>();
        foreach(int skill in new[]{38,48,44})
        {
            var target=Target(b,15);var flights=Emit(b,skill,at);
            if(skill==38){Through(b,empty,7,f=>f.Id==flights[0].Id);}else{Frame(b,empty,f=>f.Npc.Packet.Skill==skill);}
            Require(empty.Count==0,"Inspection flight hit before being saved");held.Add(new(skill,target.Serial.Value,SkillHealth.Balance256(target)));
        }
        var saved=Own(b);Require(saved.Length==133&&saved.Count(f=>f.Frame==7)==1&&saved.Count(f=>f.Frame==1)==128,"Partial charged/radial save inventory differs");
        var seedNow=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));PersistentMinions.Suspend(p);p.Internalize();
        Require(seedNow==NpcEquipmentRandom.State(b)&&life==SkillHealth.Balance256(b)&&resources==Json(GeneratedNpcLiveResources.State(b)),"Parking reset saved travel source");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),seedNow,life,resources,held.ToArray(),saved,generation)));
        Require(!ClassCombat.Available(p,CombatAbility.Valkyrie),"Natural travel evidence enabled learned casting");
        Log.Information("Original NPC spell travel:five independent charged contacts and moving waypoint hit;Nova/Frost Nova natural multi-target/per-cast four-frame leases,independent simultaneous casts,exact child/actor RNG and HP;133partially travelled flights/original items/resources/wounds persist and resume real charged/ring hits through two reloads;proc callbacks/Chain/shield targets/AI/cast remain gated");
    }
}
