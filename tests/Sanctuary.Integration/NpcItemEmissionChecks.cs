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

public static class NpcItemEmissionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemEmissionChecks));
    private sealed record Contact(uint Target,uint Source,int Before,int Min,int Max,ulong Seed);
    private sealed record Held(int Skill,uint Target,Point3D Point,long[] Ids,int Life);
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,
        Held[] Held,SorceressSpellFlight[] Flights,int Generation=0);
    private static SorceressSpellFlight Current;private static List<Contact> Contacts;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-emission-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:GraveyardZombie
    {
        [Constructible]public Recipient(){CantWalk=true;Frozen=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {
            if(Current is{} f&&Contacts!=null){Contacts.Add(new(Serial.Value,from?.Serial.Value??0,SkillHealth.Balance256(this),f.Minimum256,f.Maximum256,f.Seed));}
            base.OnDamage(amount,from,willKill);
        }
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);Require(r.ReadInt()==0,"Item emission recipient version");}
    }
    public static void Configure(){Register("npc-item-emission-check",false);Register("npc-item-emission-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original item factory requests,partial rings,natural HP and saved outgoing flights",commandLine=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SorceressSpellFlight[] Own(AmazonValkyrieBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static void Place(AmazonValkyrieBody b,PlayerMobile p)
    {b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();var r=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,r with{Parked=false});Require(NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Item emission source not active");}
    private static Recipient Target(AmazonValkyrieBody b,int dx,int dy=0)
    {var t=new Recipient();t.SetHits(65000);t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Step(long id,List<Contact> contacts)
    {
        var f=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Id==id);var prior=Current;var previous=Contacts;Current=f;Contacts=contacts;
        try{SorceressSpellMissiles.Tick(f.NextUtc,true,id);}finally{Current=prior;Contacts=previous;}
    }
    private static void Through(AmazonValkyrieBody b,long[] ids,List<Contact> contacts)
    {
        var set=ids.ToHashSet();for(int i=0;i<100;i++)
        {var left=Own(b).Where(f=>set.Contains(f.Id)).OrderBy(f=>f.Id).ToArray();if(left.Length==0){return;}foreach(var f in left){Step(f.Id,contacts);}}
        throw new InvalidOperationException("Item emissions exceeded natural lifetime");
    }
    private static void Verify(AmazonValkyrieBody b,NpcEquipmentSeed before,List<Contact> contacts,Recipient[] targets,int[] initial)
    {
        var lives=targets.Select((t,i)=>(Id:t.Serial.Value,Life:initial[i])).ToDictionary(x=>x.Id,x=>x.Life);ulong seed=before.Seed;
        foreach(var c in contacts)
        {
            int raw=SorceressSpellMissiles.Roll(c.Min,c.Max,c.Seed,out _);if(SourceRandom.Roll(ref seed)%100<5){raw=checked(raw*2);}
            Require(c.Source==b.Serial.Value&&c.Before==lives[c.Target],"Outgoing item source or notification-before-HP lost");lives[c.Target]-=raw;
        }
        Require(targets.All(t=>SkillHealth.Balance256(t)==lives[t.Serial.Value])&&NpcEquipmentRandom.State(b)==before with{Seed=seed},"Item emission HP or projectile/actor RNG differs");
    }
    private static NpcItemSpellEmission Emit(AmazonValkyrieBody b,Recipient target,int skill,int outgoing,DateTime at,int failure,List<NpcItemMissileRequest> requests)
    {
        string body=b.SavedText,items=Json(Items(b)),resources=Json(GeneratedNpcLiveResources.State(b));var seed=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);
        var result=SorceressSpellMissiles.EmitValkyrieItem(b,NpcItemTarget.Capture(target),target.Location,skill,3,outgoing,at,r=>
        {int i=requests.Count;requests.Add(r);return new(failure!=3&&(failure!=1||i!=0)&&(failure!=2||i%3!=0),SourceRandom.InitLow(6000+i));});
        Require(b.SavedText==body&&Json(Items(b))==items&&Json(GeneratedNpcLiveResources.State(b))==resources&&NpcEquipmentRandom.State(b)==seed&&SkillHealth.Balance256(b)==life,"Item emission mutated original source/items/resources/RNG");
        Require(result.Result==1&&result.Attempts==requests.Count&&requests.Count==(skill==36?1:64),"Item source return/attempt count differs");
        var flights=Own(b).Where(f=>result.Flights.Contains(f.Id)).ToArray();
        int expected=failure==3?0:requests.Where((r,i)=>(failure!=1||i!=0)&&(failure!=2||i%3!=0)).Count();
        Require(flights.Length==expected&&result.Flights.Length==expected,"Partial ring allocation created missing rays or dropped survivors");
        foreach(var f in flights)
        {
            int ordinal=skill==36?0:f.RadialIndex;var r=requests[ordinal];Require(f.X*2==r.X&&f.Y*2==r.Y&&f.Npc.Actor==b.Serial.Value&&f.Seed==SourceRandom.InitLow(6000+ordinal),"Actual emitted origin/owner/seed differs from request");
            Require((outgoing==1?f.X==target.X&&f.Y==target.Y:f.X==b.X&&f.Y==b.Y)&&r.Skill==skill&&r.Rank==3,"Outgoing/incoming launch position lost");
            if(skill==36){Require((r.Flags&~0x1000)==33&&r.AttackRating==b.EquipmentNumbers.Read(19)&&r.TargetX==(outgoing==1?4*target.X-2*b.X:2*target.X),"Item Fire Bolt direction/own AR flags lost");}
            else{Require(r.Flags==7&&r.Velocity==24&&f.Cast+ordinal==f.Id,"Partial ring identity/velocity changed");}
        }
        return result;
    }
    private static void Controls(AmazonValkyrieBody b,PlayerMobile p)
    {
        foreach(int skill in new[]{36,44,48})foreach(int outgoing in new[]{0,1})foreach(int failure in new[]{0,1,2,3})
        {
            Place(b,p);var primary=Target(b,12);var near=Target(b,2);var far=Target(b,14);var targets=new[]{primary,near,far};var requests=new List<NpcItemMissileRequest>();var contacts=new List<Contact>();
            try
            {
                var emitted=Emit(b,primary,skill,outgoing,TimedEffects.Now.AddMinutes(10),failure,requests);var seed=NpcEquipmentRandom.State(b);var hp=targets.Select(SkillHealth.Balance256).ToArray();
                if(skill==36&&outgoing==1){primary.Internalize();far.MoveToWorld(new(b.X+16,b.Y,b.Z),b.Map);}
                b.MoveToWorld(new(b.X,b.Y+3,b.Z),b.Map);Through(b,emitted.Flights,contacts);Verify(b,seed,contacts,targets,hp);
                if(emitted.Flights.Length==0){Require(contacts.Count==0,"Failed item allocation dealt damage");}
                else if(skill==36){Require(contacts.Count>0&&contacts.All(c=>c.Target==(outgoing==1?far.Serial.Value:near.Serial.Value)),"Item Fire Bolt did not travel away from its actual origin");}
                else
                {
                    Require(contacts.Count>0,"Partial/complete item ring never contacted a natural recipient");
                    Require(outgoing==1?SkillHealth.Balance256(near)==hp[1]:SkillHealth.Balance256(primary)==hp[0]&&SkillHealth.Balance256(far)==hp[2],"Item ring remained centered on the wrong unit");
                }
            }
            finally{SorceressSpellMissiles.Cancel(b);foreach(var t in targets){t.Delete();}}
        }
        Place(b,p);int calls=0;var before=Json(Own(b));var random=NpcEquipmentRandom.State(b);
        var missing=SorceressSpellMissiles.EmitValkyrieItem(b,null,b.Location,48,3,1,TimedEffects.Now,_=>{calls++;return new(true,1);});
        Require(missing.Result==0&&missing.Attempts==0&&calls==0&&Json(Own(b))==before&&NpcEquipmentRandom.State(b)==random,"Missing outgoing ring target allocated or mutated source");
        var empty=SorceressSpellMissiles.EmitValkyrieItem(b,null,Point3D.Zero,36,3,1,TimedEffects.Now,_=>{calls++;return new(true,1);});
        Require(empty.Result==1&&empty.Attempts==0&&calls==0,"Zero-path direct item return/guard differs");
        var distant=SorceressSpellMissiles.EmitValkyrieItem(b,null,new(b.X+60,b.Y,b.Z),36,3,0,TimedEffects.Now,_=>{calls++;return new(true,1);});
        Require(distant.Result==1&&distant.Attempts==1&&calls==0&&Json(Own(b))==before,"Source100-subtile factory range guard allocated or changed direct return");
        foreach(int unsupported in new[]{41,53})
        {bool refused=false;try{SorceressSpellMissiles.EmitValkyrieItem(b,null,b.Location,unsupported,3,0,TimedEffects.Now,_=>{calls++;return new(true,1);});}catch(NotSupportedException){refused=true;}Require(refused&&calls==0&&NpcEquipmentRandom.State(b)==random,"Unqualified item world path allocated or drew RNG");}
    }
    private static Held Hold(AmazonValkyrieBody b,int skill,DateTime at)
    {
        var primary=Target(b,8);var requests=new List<NpcItemMissileRequest>();var emitted=Emit(b,primary,skill,1,at,skill==36?0:2,requests);
        var hitPoint=new Point3D(b.X+(skill==36?14:10),b.Y,b.Z);primary.Internalize();var contacts=new List<Contact>();
        foreach(long id in emitted.Flights){Step(id,contacts);}Require(contacts.Count==0,"Internal held target damaged");
        if(skill==36){Step(emitted.Flights[0],contacts);}
        return new(skill,primary.Serial.Value,hitPoint,emitted.Flights,SkillHealth.Balance256(primary));
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra item emission reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;point=s.Point;
            Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false}&&b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random
                &&SkillHealth.Balance256(b)==s.Life&&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&Json(Own(b))==Json(s.Flights),"Actual outgoing partial rings/items/wounds/resources/RNG changed on reload");
            p.MoveToWorld(point,Map.Trammel);Place(b,p);
            foreach(var h in s.Held)
            {
                var target=(Recipient)World.FindMobile((Serial)h.Target);var contacts=new List<Contact>();var seed=NpcEquipmentRandom.State(b);var old=Own(b).Where(f=>h.Ids.Contains(f.Id)).ToArray();
                foreach(long id in h.Ids){Step(id,contacts);}var aged=Own(b).Where(f=>h.Ids.Contains(f.Id)).ToArray();
                Require(aged.Length==old.Length&&aged.Zip(old).All(pair=>pair.First.X==pair.Second.X&&pair.First.Y==pair.Second.Y&&pair.First.Frame==pair.Second.Frame+1)&&contacts.Count==0&&NpcEquipmentRandom.State(b)==seed,"Loaded outgoing item replayed movement/HP/RNG");
                Require(SkillHealth.Balance256(target)==h.Life,"Saved target life changed");target.MoveToWorld(h.Point,b.Map);Through(b,h.Ids,contacts);
                Require(contacts.Count>0&&SkillHealth.Balance256(target)<h.Life,"Saved outgoing item failed natural post-reload hit");Verify(b,seed,contacts,[target],[h.Life]);target.Delete();
            }
        }
        else
        {
            p=new PlayerMobile{Name="NpcItemEmissionFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6800,24,3770);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var c=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,c);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);Place(b,p);
        }
        Controls(b,p);Place(b,p);var at=TimedEffects.Now.AddMinutes(10);var held=new[]{Hold(b,36,at),Hold(b,44,at),Hold(b,48,at)};var flights=Own(b);
        Require(flights.Length==85&&flights.Count(f=>f.Npc.Packet.Skill==36)==1&&flights.Count(f=>f.Npc.Packet.Skill==44)==42&&flights.Count(f=>f.Npc.Packet.Skill==48)==42,"Partial outgoing saved population differs");
        var random=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));PersistentMinions.Suspend(p);p.Internalize();
        Require(random==NpcEquipmentRandom.State(b)&&life==SkillHealth.Balance256(b)&&resources==Json(GeneratedNpcLiveResources.State(b)),"Outgoing emission parking reset original source");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),random,life,resources,held,flights,generation)));
        Log.Information("Original item emission:Fire Bolt/target-centered Nova/Frost Nova use verified requests and shared motion/HP;24incoming/outgoing allocation cases,partial ray survival,source100-subtile guard,exact child/actor RNG and natural contacts;85partial outgoing flights plus original items/resources/wounds through two reloads;Charged/Chain world binding/full callback history/game RNG remain separate");
    }
}
