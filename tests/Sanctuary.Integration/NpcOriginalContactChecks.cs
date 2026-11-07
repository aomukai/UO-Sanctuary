// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcOriginalContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcOriginalContactChecks));
    private sealed record Seen(uint Actor,NpcOriginalProcVisit Visit);
    private sealed record Held(uint Actor,string Body,string[] Items,string Resources,NpcEquipmentSeed Random,int Life,uint Target,int TargetLife,IronGolemContact.Saved Pending);
    private sealed record Saved(uint Owner,Point3D Point,Held[] Bodies,ulong Game,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-original-contact-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static void Cancel(AmazonValkyrieBody b){SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);}
    private sealed class Host:IDisposable
    {
        private readonly Func<SkeletonGuardian,NpcItemMissileRequest,DateTime,NpcItemSpellAllocation> Old=NpcOriginalItemProcs.Allocator;
        private readonly Func<SkeletonGuardian,Point3D> OldPath=NpcOriginalItemProcs.FirstPath;
        private readonly Action<SkeletonGuardian,NpcOriginalProcVisit> OldObserved=NpcOriginalItemProcs.Observed;
        private readonly Func<NpcChainRequest,NpcChainAllocation> OldChain=ChainLightning.OriginalNpcAllocator;
        internal ulong Game;internal readonly List<Seen> Visits=[];internal readonly List<NpcItemMissileRequest> Allocations=[];internal bool Fail;
        internal Host(ulong game)
        {
            Game=game;NpcOriginalItemProcs.Allocator=(b,r,at)=>{Require(at.Kind==DateTimeKind.Utc,"Allocation lost UTC frame");Allocations.Add(r);uint n=SourceRandom.Roll(ref Game);return new(!Fail,SourceRandom.InitLow(unchecked((int)n)));};
            NpcOriginalItemProcs.FirstPath=_=>throw new InvalidOperationException("Ordinary target/corpse unexpectedly requested AI path");
            NpcOriginalItemProcs.Observed=(b,v)=>Visits.Add(new(b.Serial.Value,v));
            ChainLightning.OriginalNpcAllocator=_=>{uint n=SourceRandom.Roll(ref Game);return new(!Fail,SourceRandom.InitLow(unchecked((int)n)));};
        }
        internal void Clear(){Visits.Clear();Allocations.Clear();}
        public void Dispose(){NpcOriginalItemProcs.Allocator=Old;NpcOriginalItemProcs.FirstPath=OldPath;NpcOriginalItemProcs.Observed=OldObserved;ChainLightning.OriginalNpcAllocator=OldChain;}
    }
    public static void Configure(){Register("npc-original-contact-check",false);Register("npc-original-contact-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original ordinary/incoming producers and saved original contact context",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ValkyrieContactChecks.Recipient Target(AmazonValkyrieBody b,int distance=1)
    {
        var t=new ValkyrieContactChecks.Recipient();t.Frozen=false;t.SetHits(65000);t.MoveToWorld(new(b.X+distance,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();
        foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static ulong Boundary(int residue)
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if(SourceRandom.Roll(ref copy)%100==(uint)residue){return seed;}}throw new InvalidOperationException("Missing boundary seed");}
    private static IronGolemContact.Prepared Prepare(AmazonValkyrieBody b,PlayerMobile p,BaseCreature t,bool hit=true)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRatingAgainst(b,t,0),ClassStats.Defense(t),ClassStats.Level(b),ClassStats.Level(t));
        for(int i=1;i<10000;i++)
        {
            ulong seed=SourceRandom.InitLow(i),copy=seed;if((SourceRandom.Roll(ref copy)%100/100.0<chance)!=hit){continue;}
            NpcEquipmentRandom.Set(b,seed);var contact=ValkyrieEquipmentContact.Prepare(b,p,t,_=>true,TimedEffects.Now);
            Require(((contact.Impact.Packet.ResultFlags&1)!=0)==hit,"Ordinary result fixture did not select requested hit/miss");return contact;
        }
        throw new InvalidOperationException("No ordinary contact result seed");
    }
    private static void Sequence(AmazonValkyrieBody b,Host host,UnitEventEntry[] nodes,int[] phases,bool allowProc)
    {
        var actual=host.Visits.Where(v=>v.Actor==b.Serial.Value).Select(v=>(v.Visit.Function,v.Visit.Phase,v.Visit.Packed)).ToArray();
        var expected=phases.SelectMany(p=>nodes.Where(n=>n.Event==p).Select(n=>(n.Function,(int)n.Event,unchecked((uint)n.Guid1)))).ToArray();
        Require(actual.SequenceEqual(expected),"Original ordinary/incoming event was duplicated,missing or reordered actual="+Json(actual)+" expected="+Json(expected));
        int emitted=0;
        foreach(var seen in host.Visits.Where(v=>v.Actor==b.Serial.Value))
        {
            var v=seen.Visit;if(v.Function is not(20 or 21)){continue;}
            int chance=b.EquipmentNumbers.Read((int)(v.Packed>>16),(ushort)v.Packed);ulong seed=v.Before;int result=0;
            if(allowProc&&chance>0){if(SourceRandom.Roll(ref seed)%100<(uint)chance){result=1;}}
            Require(v.After==seed&&v.Result==result,"Producer supplied wrong proc gate or consumed actor stream twice");
            if(result!=0){int skill=(ushort)v.Packed>>6,rank=(int)(v.Packed&63);emitted+=skill is 44 or 48?64:skill==38?rank+2:1;}
        }
        Require(host.Allocations.Count==emitted,"Original producer allocated duplicate/missing proc missiles");
    }
    private static void MissingHost(AmazonValkyrieBody b,PlayerMobile p,Host host)
    {
        var t=Target(b);var a=NpcOriginalItemProcs.Allocator;
        try
        {
            var saved=Prepare(b,p,t);string body=b.SavedText,resources=Json(GeneratedNpcLiveResources.State(b));var rng=NpcEquipmentRandom.Require(b);int life=SkillHealth.Balance256(t);ulong game=host.Game;bool refused=false;
            NpcOriginalItemProcs.Allocator=null;
            try{_=ValkyrieEquipmentContact.Prepare(b,p,t,_=>true,TimedEffects.Now);}catch(NotSupportedException){refused=true;}
            Require(refused&&NpcEquipmentRandom.Require(b)==rng&&SkillHealth.Balance256(t)==life&&host.Game==game,"Missing provider consumed ordinary allocation/HP/RNG");
            refused=false;try{IronGolemContact.Release(b,p,t,saved,_=>true,TimedEffects.Now);}catch(NotSupportedException){refused=true;}
            Require(refused&&!saved.Consumed&&NpcEquipmentRandom.Require(b)==rng&&SkillHealth.Balance256(t)==life&&body==b.SavedText&&resources==Json(GeneratedNpcLiveResources.State(b)),"Missing provider changed pending contact/resources before refusal");saved.Cancel();
        }
        finally{NpcOriginalItemProcs.Allocator=a;t.Delete();}
    }
    private static void Outgoing(AmazonValkyrieBody b,PlayerMobile p,Host host)
    {
        var nodes=b.EquipmentNumbers.ItemEvents.Capture().Events.Entries;
        foreach(bool hit in new[]{true,false})foreach(int residue in new[]{0,99})foreach(bool fail in new[]{false,true})
        {
            var t=Target(b);host.Fail=fail;
            try
            {
                var contact=Prepare(b,p,t,hit);var packet=contact.Impact.Packet;NpcEquipmentRandom.Set(b,Boundary(residue));ulong seed=NpcEquipmentRandom.Require(b).Seed,game=host.Game;int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),packets=NativeMonsterReturn.Session(t).Packets;
                host.Clear();var result=IronGolemContact.Release(b,p,t,contact,_=>true,TimedEffects.Now.AddMinutes(10));
                Require(result.Exit==MonsterContactExit.Finished&&contact.Consumed&&NativeMonsterReturn.Session(t).Packets==packets+1,"Original ordinary packet did not finish/free exactly once");
                Require(hit?result.Commit!=null&&SkillHealth.Balance256(t)==life-packet.Total256:result.Commit==null&&SkillHealth.Balance256(t)==life,"Original ordinary hit/miss HP changed or damage recomputed");
                Sequence(b,host,nodes,hit?[5,7]:[7],hit);if(host.Visits.Count>0){seed=host.Visits[^1].Visit.After;}Require(NpcEquipmentRandom.Require(b).Seed==seed,"Original ordinary events used unobserved chance draws");
                ulong expected=game;for(int i=0;i<host.Allocations.Count;i++){SourceRandom.Roll(ref expected);}Require(host.Game==expected&&NecromancerEffects.ManaBalance256(p)==mana,"Ordinary callback borrowed actor/owner allocation/payment");
                int published=SorceressSpellMissiles.Snapshot().Flights.Count(f=>f.Npc?.Actor==b.Serial.Value)+ChainLightning.Snapshot().Count(f=>f.OriginalNpc?.Source.Actor==b.Serial.Value);
                Require(published==(fail?0:host.Allocations.Count),"Ordinary proc did not publish actual allocated projectiles");
            }
            finally{Cancel(b);t.Delete();}
        }
        host.Fail=false;using var unused=new DisposableTarget(Target(b));
        var outOfRange=Prepare(b,p,unused.Target);host.Clear();ulong before=NpcEquipmentRandom.Require(b).Seed;var resultOut=IronGolemContact.Release(b,p,unused.Target,outOfRange,_=>false,TimedEffects.Now);
        Require(resultOut.Exit==MonsterContactExit.OutOfRangeFreed&&host.Visits.Count==0&&host.Allocations.Count==0&&NpcEquipmentRandom.Require(b).Seed==before,"Out-of-range original contact dispatched callbacks");
    }
    private sealed class DisposableTarget(BaseCreature target):IDisposable{internal BaseCreature Target=target;public void Dispose()=>Target.Delete();}
    private static void Incoming(AmazonValkyrieBody b,PlayerMobile p,Host host)
    {
        var enemy=Target(b,2);PreparedMonsterNativeDamage.RequireIncoming(p,b,enemy);var nodes=b.EquipmentNumbers.ItemEvents.Capture().Events.Entries;
        try
        {
            var before=NpcEquipmentRandom.Require(b);int hp=SkillHealth.Balance256(b);bool refused=false;
            try{PreparedMonsterNativeDamage.RequireIncoming(p,b,p);}catch(NotSupportedException){refused=true;}
            Require(refused&&NpcEquipmentRandom.Require(b)==before&&SkillHealth.Balance256(b)==hp,"Original guardian friendship wrapper was bypassed");
            foreach(bool missile in new[]{false,true})foreach(int result in new[]{1,5,37})foreach(uint flags in new[]{0u,32u,160u})
            {
                host.Clear();NpcEquipmentRandom.Set(b,Boundary(0));int life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p);ulong seed=NpcEquipmentRandom.Require(b).Seed;
                var at=TimedEffects.Now.AddMinutes(10);bool beforeHealth=false;
                var receiver=new NpcIncomingProcChecks.Receiver(p,enemy,b,at,missile,()=>
                {beforeHealth=true;Require(SkillHealth.Balance256(b)==life,"Original incoming callback ran after HP");});
                var packet=new PreparedMonsterDamagePacket(256,256,0,0,result,HitFlags:flags);var recipient=new PreparedMonsterRecipient(false,()=>false,()=>8,()=>false);
                bool phase=(result&32)==0&&(!missile||(flags&32)!=0);
                if(missile){PreparedMonsterDamage.ReleaseMissile(enemy,b,packet,recipient,_=>throw new InvalidOperationException("No stun roll"),receiver,at);}
                else{PreparedMonsterDamage.Release(enemy,b,packet,recipient,_=>throw new InvalidOperationException("No stun roll"),receiver,at);}
                Sequence(b,host,nodes,phase?(missile?[2]:[1,3]):missile?[]:[3],phase&&(result&4)!=0);
                if(host.Visits.Count>0){seed=host.Visits[^1].Visit.After;}Require(beforeHealth&&SkillHealth.Balance256(b)==life-256&&NpcEquipmentRandom.Require(b).Seed==seed&&NecromancerEffects.ManaBalance256(p)==mana,"Incoming producer HP/RNG/payment mismatch");
                Require(receiver.Incoming==(phase?1:0)&&receiver.Outgoing==(phase&&(!missile||(flags&128)==0)?1:0),"Original incoming phase mask/double dispatch");
                Cancel(b);
            }
        }
        finally{enemy.Delete();Cancel(b);}
    }
    private static void Lethal(AmazonValkyrieBody b,PlayerMobile p,Host host)
    {
        var t=Target(b);SkillHealth.SetBalance256(t,256);var nodes=b.EquipmentNumbers.ItemEvents.Capture().Events.Entries;host.Clear();host.Fail=false;
        try
        {
            var prepared=Prepare(b,p,t);Require(prepared.Impact.Packet.Total256>256,"Lethal original fixture needs more than1HP damage");NpcEquipmentRandom.Set(b,Boundary(0));
            var result=IronGolemContact.Release(b,p,t,prepared,_=>true,TimedEffects.Now.AddMinutes(10));
            Require(result.Commit is{AfterHealth256:0}&&(t.Deleted||!t.Alive)&&t.LastKiller==b&&ClassLoot.Killer(t)==p,"Original lethal contact lost death/credit");
            Sequence(b,host,nodes,[5,9,7],true);
        }
        finally{Cancel(b);t.Corpse?.Delete();t.Delete();}
    }
    private static void Contexts(AmazonValkyrieBody first,AmazonValkyrieBody second,PlayerMobile p,Host host)
    {
        NpcItemChainChecks.Place(first,p);NpcItemChainChecks.Place(second,p);
        var prepared=IronGolemContact.Prepare(first,second,IronGolemContact.Require(first,p,second),()=>0);var saved=IronGolemContact.Capture(prepared);
        Require(saved.SourceOriginalEvents&&saved.TargetOriginalEvents&&Json(saved)==Json(IronGolemContact.Capture(IronGolemContact.Restore(saved))),"Saved original defender event profile was lost");
        prepared.Cancel();PersistentMinions.Park(first);PersistentMinions.Park(second);
    }
    private static void MissingChild(AmazonValkyrieBody b,PlayerMobile p,Host host)
    {
        if(!b.EquipmentNumbers.Stats.Any(v=>v.Value>0&&v.Stat is 195 or 196 or 198 or 201&&((ushort)v.Packed>>6)==53)){return;}
        var target=Target(b);var provider=ChainLightning.OriginalNpcAllocator;ulong seed=NpcEquipmentRandom.Require(b).Seed,game=host.Game;int life=SkillHealth.Balance256(target);bool refused=false;
        try
        {
            ChainLightning.OriginalNpcAllocator=null;try{_=ValkyrieEquipmentContact.Prepare(b,p,target,_=>true,TimedEffects.Now);}catch(NotSupportedException){refused=true;}
            Require(refused&&NpcEquipmentRandom.Require(b).Seed==seed&&host.Game==game&&SkillHealth.Balance256(target)==life,"Missing Chain child service consumed ordinary HP/RNG");
        }
        finally{ChainLightning.OriginalNpcAllocator=provider;target.Delete();}
    }
    private static Held Pending(AmazonValkyrieBody b,PlayerMobile p)
    {
        NpcItemChainChecks.Place(b,p);var target=Target(b);var prepared=Prepare(b,p,target);var saved=IronGolemContact.Capture(prepared);prepared.Cancel();
        Require(saved.SourceOriginalEvents&&!saved.TargetOriginalEvents&&Json(saved)==Json(IronGolemContact.Capture(IronGolemContact.Restore(saved))),"Saved original contact route did not round-trip");
        PersistentMinions.Park(b);return new(b.Serial.Value,b.SavedText,Items(b),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),target.Serial.Value,SkillHealth.Balance256(target),saved);
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;Point3D point;int generation=0;var bodies=new List<AmazonValkyrieBody>();ulong game=SourceRandom.InitLow(12500);
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra original contact reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;point=s.Point;game=s.Game;Require(p is{Alive:true,Deleted:false},"Original contact owner missing");
            foreach(var h in s.Bodies)
            {
                var b=World.FindMobile((Serial)h.Actor)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&b.SavedText==h.Body&&Json(Items(b))==Json(h.Items)&&Json(GeneratedNpcLiveResources.State(b))==h.Resources&&NpcEquipmentRandom.Require(b)==h.Random&&SkillHealth.Balance256(b)==h.Life,"Original contact source/history/items/resources/RNG changed on load");bodies.Add(b);
            }
            p.MoveToWorld(point,Map.Trammel);
            using var resumed=new Host(game);
            foreach(var h in s.Bodies)
            {
                var b=(AmazonValkyrieBody)World.FindMobile((Serial)h.Actor);var t=World.FindMobile((Serial)h.Target)as BaseCreature;NpcItemChainChecks.Place(b,p);Require(SkillHealth.Balance256(t)==h.TargetLife,"Pending original target wounds changed");
                var contact=IronGolemContact.Restore(h.Pending);var result=IronGolemContact.Release(b,p,t,contact,_=>true,TimedEffects.Now.AddMinutes(10));Require(result.Commit!=null&&SkillHealth.Balance256(t)==h.TargetLife-h.Pending.Impact.Packet.Total256,"Loaded original packet lost HP or proc route");Cancel(b);t.Delete();PersistentMinions.Park(b);
            }
            game=resumed.Game;
        }
        else
        {
            p=new PlayerMobile{Name="NpcOriginalContactFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6600,32,3000);point=p.Location;
            using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var cases=raw.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();Require(cases.Length==57,"Original contact corpus");
            using var host=new Host(game);
            foreach(var c in cases)
            {
                var b=ValkyrieDamageChecks.Body(p,c);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);GeneratedNpcLiveResources.ActivateItemEvents(b);ValkyrieEquipmentDefense.Activate(b);
                var provider=NpcOriginalItemProcs.Allocator;NpcOriginalItemProcs.Allocator=null;bool refused=false;string before=b.SavedText;try{ValkyrieEquipmentContact.ActivateOriginal(b);}catch(NotSupportedException){refused=true;}finally{NpcOriginalItemProcs.Allocator=provider;}
                Require(refused&&before==b.SavedText,"Original profile admitted without host or changed state");ValkyrieEquipmentContact.ActivateOriginal(b);bodies.Add(b);PersistentMinions.Park(b);
            }
            game=host.Game;
        }
        using(var host=new Host(game))
        {
            foreach(var b in bodies){NpcItemChainChecks.Place(b,p);MissingHost(b,p,host);MissingChild(b,p,host);Outgoing(b,p,host);Incoming(b,p,host);Lethal(b,p,host);PersistentMinions.Park(b);}
            Contexts(bodies[0],bodies[1],p,host);var held=bodies.Select(b=>Pending(b,p)).ToArray();PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,held,host.Game,generation)));
        }
        Log.Information("Original NPC contact:57unfiltered original loadouts admitted by explicit host/profile;ordinary hit/miss/range/lethal,actual proc allocation and single incoming melee/missile phases,pre-HP hooks,own actor/world streams,both saved event profiles and original pending packets through two reloads;default game service/AI/cast remain gated");
    }
}
