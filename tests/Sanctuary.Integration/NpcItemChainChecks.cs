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

public static class NpcItemChainChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemChainChecks));
    private sealed record Contact(int Ordinal,uint Target,uint Source,int Before,int Minimum,int Maximum,ulong Seed);
    private sealed record TargetState(uint Actor,int Life);
    private sealed record Held(long Cast,int Ordinal,TargetState First,TargetState Second);
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,Held[] Held,ChainFlight[] Flights,int Generation=0);
    private static ChainFlight Current;private static List<Contact> Contacts;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-chain-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:GraveyardZombie
    {
        [Constructible]public Recipient(){CantWalk=true;Frozen=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {
            if(Current is{} f&&Contacts!=null){Contacts.Add(new(f.OriginalNpc.Ordinal,Serial.Value,from?.Serial.Value??0,SkillHealth.Balance256(this),f.Min256,f.Max256,f.OriginalNpc.Seed));}
            base.OnDamage(amount,from,willKill);
        }
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);Require(r.ReadInt()==0,"Chain recipient version");}
    }
    public static void Configure(){Register("npc-item-chain-check",false);Register("npc-item-chain-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC retained-rank Chain Lightning,per-hop allocator and real saves",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ChainFlight[] Own(AmazonValkyrieBody b)=>ChainLightning.Snapshot().Where(f=>f.OriginalNpc?.Source.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static void Place(AmazonValkyrieBody b,PlayerMobile p)
    {b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();var row=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,row with{Parked=false});Require(NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Original chain source not active");}
    private static Recipient Target(AmazonValkyrieBody b,int dx)
    {var t=new Recipient();t.SetHits(65000);t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static NpcChainAllocation Allocate(NpcChainRequest r,List<NpcChainRequest> requests,bool fail=false)
    {
        Require(r.Skill==53&&r.Rank==3&&r.Flags==0x21&&r.Remaining==5-r.Ordinal&&r.Actor!=r.Owner,"Original chain allocation request lost source/rank/flags/budget");
        requests.Add(r);return new(!fail,SourceRandom.InitLow(3000+r.Ordinal));
    }
    private static ChainFlight Root(AmazonValkyrieBody b,DateTime at,List<NpcChainRequest> requests)
    {
        var before=NpcEquipmentRandom.State(b);var f=ChainLightning.PrepareValkyrie(b,new(b.X+12,b.Y,b.Z),3,at,r=>Allocate(r,requests));
        Require(f is{Remaining:5,Rank:3,OriginalNpc.Ordinal:0}&&f.PlayerPacket==null&&f.NpcCaster==0&&NpcEquipmentRandom.State(b)==before,"NPC root borrowed Mage/player context or consumed actor RNG");
        Require(f.OriginalNpc.Source.Packet.LightningPierce==0,"Fixture needs original zero-pierce packet");ChainLightning.CommitValkyrie(f);return f;
    }
    private static void Step(AmazonValkyrieBody b,long cast,List<Contact> contacts,Func<NpcChainRequest,NpcChainAllocation> allocate,bool actions=true)
    {
        var f=Own(b).Single(x=>x.Cast==cast);var old=Current;var previous=Contacts;Current=f;Contacts=contacts;
        try{ChainLightning.Tick(f.NextUtc,actions,cast,allocate);}finally{Current=old;Contacts=previous;}
    }
    private static void Through(AmazonValkyrieBody b,long cast,List<Contact> contacts,Func<NpcChainRequest,NpcChainAllocation> allocate,int limit=150)
    {for(int i=0;i<limit&&Own(b).Any(f=>f.Cast==cast);i++){Step(b,cast,contacts,allocate);}}
    private static void Verify(AmazonValkyrieBody b,NpcEquipmentSeed before,List<Contact> contacts,Recipient[] targets,int[] initial)
    {
        var lives=targets.Select((t,i)=>(t.Serial.Value,Life:initial[i])).ToDictionary(x=>x.Value,x=>x.Life);ulong critical=before.Seed;
        foreach(var c in contacts)
        {
            int raw=SorceressSpellMissiles.Roll(c.Minimum,c.Maximum,c.Seed,out _);if(SourceRandom.Roll(ref critical)%100<5){raw=checked(raw*2);}
            Require(c.Source==b.Serial.Value&&c.Before==lives[c.Target],"Chain source or notification-before-HP changed");lives[c.Target]-=raw;
        }
        Require(targets.All(t=>SkillHealth.Balance256(t)==lives[t.Serial.Value])&&NpcEquipmentRandom.State(b)==before with{Seed=critical},"Original chain HP/child seeds/intrinsic RNG differ");
    }
    private static void Controls(AmazonValkyrieBody b,PlayerMobile p)
    {
        foreach(int mode in new[]{0,1,2})
        {
            Place(b,p);var first=Target(b,2);var second=mode==0?null:Target(b,8);var requests=new List<NpcChainRequest>();var contacts=new List<Contact>();
            try
            {
                var root=Root(b,TimedEffects.Now,requests);var seed=NpcEquipmentRandom.State(b);var targets=second==null?new[]{first}:new[]{first,second};var lives=targets.Select(SkillHealth.Balance256).ToArray();
                b.MoveToWorld(new(b.X,b.Y+3,b.Z),b.Map);
                Through(b,root.Cast,contacts,r=>
                {
                    Require(contacts.Count==r.Ordinal-1&&Math.Abs(r.Y-b.Y)>=2,"Child allocation happened after damage or used moved caster origin");return Allocate(r,requests,mode==2);
                });
                int expected=mode==1?5:1;Require(Own(b).Length==0&&contacts.Count==expected&&requests.Count==(mode==0?1:mode==1?5:2),"Original chain lone/bounce/allocation-failure policy differs mode="+mode+" contacts="+contacts.Count+" requests="+requests.Count);
                Require(contacts.Select(c=>c.Ordinal).SequenceEqual(Enumerable.Range(0,expected)),"Chain rank budget skipped or replayed a segment");Verify(b,seed,contacts,targets,lives);
            }
            finally{ChainLightning.Cancel(b);first.Delete();second?.Delete();}
        }
        Place(b,p);var rng=NpcEquipmentRandom.State(b);var failed=ChainLightning.PrepareValkyrie(b,b.Location,3,TimedEffects.Now,_=>new(false,0));
        Require(failed==null&&Own(b).Length==0&&NpcEquipmentRandom.State(b)==rng,"Failed root emitted or consumed actor RNG");
        var a=Target(b,2);var peer=Target(b,8);var list=new List<NpcChainRequest>();
        try
        {
            var f=Root(b,TimedEffects.Now,list);var original=Json(Own(b));rng=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(a);bool refused=false;
            for(int i=0;i<25;i++)
            {
                try{f=NpcItemChainFlights.Advance(f,f.NextUtc,true,false,null);}catch(NotSupportedException e){Require(e.Message.Contains("per-hop allocator"),"Unexpected unsupported chain branch");refused=true;break;}
            }
            Require(refused&&Json(Own(b))==original&&NpcEquipmentRandom.State(b)==rng&&SkillHealth.Balance256(a)==life&&list.Count==1,"Missing child allocator published movement,HP or RNG");
            var snapshot=ChainLightning.Snapshot();var bad=snapshot.Select(x=>x.Cast==f.Cast?x with{OriginalNpc=x.OriginalNpc with{Ordinal=9}}:x).ToArray();refused=false;
            try{ChainLightning.Restore(bad);}catch(InvalidDataException){refused=true;}Require(refused&&Json(Own(b))==original,"Invalid NPC restore partly replaced chain pool");
            var copy=ChainLightning.Snapshot().Single(x=>x.Cast==f.Cast);copy.OriginalNpc.Steps.Cells[0]=new(1,1);Require(Own(b)[0].OriginalNpc.Steps.Cells[0]!=copy.OriginalNpc.Steps.Cells[0],"NPC chain snapshot aliases retained cells");
        }
        finally{ChainLightning.Cancel(b);a.Delete();peer.Delete();}
        var at=TimedEffects.Now;list=[];var offline=Root(b,at,list);rng=NpcEquipmentRandom.State(b);var empty=new List<Contact>();
        Step(b,offline.Cast,empty,_=>throw new InvalidOperationException("Offline allocation"),false);var aged=Own(b).Single();
        Require(aged.Frame==1&&aged.X==offline.X&&aged.Y==offline.Y&&NpcEquipmentRandom.State(b)==rng&&empty.Count==0,"Offline chain moved or fought");ChainLightning.Cancel(b);
    }
    private static Held Hold(AmazonValkyrieBody b,int ordinal,DateTime at)
    {
        var a=Target(b,ordinal==0?6:2);var peer=Target(b,ordinal==0?12:8);var requests=new List<NpcChainRequest>();var contacts=new List<Contact>();
        var root=Root(b,at,requests);
        if(ordinal==0){Step(b,root.Cast,contacts,r=>Allocate(r,requests));Step(b,root.Cast,contacts,r=>Allocate(r,requests));}
        else
        {
            for(int i=0;i<25&&Own(b).Single(f=>f.Cast==root.Cast).OriginalNpc.Ordinal==0;i++){Step(b,root.Cast,contacts,r=>Allocate(r,requests));}
            Require(contacts.Count==1&&Own(b).Single(f=>f.Cast==root.Cast).OriginalNpc.Ordinal==1,"No actual child available to save");Step(b,root.Cast,contacts,r=>Allocate(r,requests));
        }
        var held=Own(b).Single(f=>f.Cast==root.Cast);Require(held.OriginalNpc.Ordinal==ordinal&&held.Frame==(ordinal==0?2:1),"Saved root/child travel phase differs");
        var result=new Held(root.Cast,ordinal,new(a.Serial.Value,SkillHealth.Balance256(a)),new(peer.Serial.Value,SkillHealth.Balance256(peer)));a.Internalize();peer.Internalize();return result;
    }
    private static void Resume(AmazonValkyrieBody b,Saved s)
    {
        foreach(var h in s.Held)
        {
            var a=(Recipient)World.FindMobile((Serial)h.First.Actor);var peer=(Recipient)World.FindMobile((Serial)h.Second.Actor);var contacts=new List<Contact>();var requests=new List<NpcChainRequest>();var old=Own(b).Single(f=>f.Cast==h.Cast);var seed=NpcEquipmentRandom.State(b);
            Step(b,h.Cast,contacts,r=>Allocate(r,requests));var aged=Own(b).Single(f=>f.Cast==h.Cast);
            Require(aged.Frame==old.Frame+1&&aged.X==old.X&&aged.Y==old.Y&&contacts.Count==0&&requests.Count==0&&NpcEquipmentRandom.State(b)==seed,"Loaded chain replayed old travel/allocation/damage");
            a.MoveToWorld(new(b.X+(h.Ordinal==0?6:2),b.Y,b.Z),b.Map);peer.MoveToWorld(new(b.X+(h.Ordinal==0?12:8),b.Y,b.Z),b.Map);
            var lives=new[]{SkillHealth.Balance256(a),SkillHealth.Balance256(peer)};
            Through(b,h.Cast,contacts,r=>Allocate(r,requests));Require(contacts.Count==5-h.Ordinal&&requests.Count==4-h.Ordinal&&!Own(b).Any(f=>f.Cast==h.Cast),"Saved root/child failed retained-rank natural hop budget");
            Require(contacts.Select(c=>c.Ordinal).SequenceEqual(Enumerable.Range(h.Ordinal,5-h.Ordinal)),"Saved hop replayed or skipped ordinal");Verify(b,seed,contacts,[a,peer],lives);a.Delete();peer.Delete();
        }
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra chain reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;point=s.Point;
            Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false}&&b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random
                &&SkillHealth.Balance256(b)==s.Life&&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&Json(Own(b))==Json(s.Flights)
                &&s.Held.SelectMany(h=>new[]{h.First,h.Second}).All(t=>World.FindMobile((Serial)t.Actor)is Recipient r&&SkillHealth.Balance256(r)==t.Life),"Original NPC root/child/items/resources/wounds/RNG changed on actual reload");
            p.MoveToWorld(point,Map.Trammel);Place(b,p);Resume(b,s);
        }
        else
        {
            p=new PlayerMobile{Name="NpcItemChainFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6800,24,3680);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var c=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,c);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);Place(b,p);
        }
        Controls(b,p);Place(b,p);var at=TimedEffects.Now.AddMinutes(10);var held=new[]{Hold(b,0,at),Hold(b,1,at)};var flights=Own(b);Require(flights.Length==2,"Saved original root/child count");
        var random=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));PersistentMinions.Suspend(p);p.Internalize();
        Require(random==NpcEquipmentRandom.State(b)&&life==SkillHealth.Balance256(b)&&resources==Json(GeneratedNpcLiveResources.State(b)),"Chain parking reset original source");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),random,life,resources,held,flights,generation)));
        Require(!ClassCombat.Available(p,CombatAbility.Valkyrie),"Prepared original Chain enabled learned casting");
        Log.Information("Original NPC Chain:own source/rank/packet and shared cell travel;natural lone/five-hop/failure contacts,per-hop allocator-before-HP,moved-caster origin,exact child/actor RNG,atomic restore/missing-service/offline guards;original partial root+child/items/resources/wounds through two reloads;proc dispatch/allocator provenance/shield targets/AI/cast remain gated");
    }
}
