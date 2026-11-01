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

public static class FireBallChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireBallChecks));
    private sealed record Saved(uint Owner,uint Target,uint Peer,int Map,Point3D Point,int Mana,int Life,int PeerLife,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-ball-fixtures.json");
    public static void Configure()
    {
        Register("fire-ball-check",Check);Register("fire-ball-reload-check",()=>FixtureNetwork.Connected("fire-ball-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"fire-ball-quick":"fire-ball-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player Fire Ball saved hit1/copy/gear/input and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6830);p.Name="FireBallFixture";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.fireball",out _),"Learn Fire Ball");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Fireball)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Fireball)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Fireball),"Fire Ball bindings");return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int dx,int dy=0)
    {var t=FireBoltChecks.Target(p,dx);t.MoveToWorld(new(t.X,t.Y+dy,t.Z),t.Map);return t;}
    private static long Create(PlayerMobile p)=>SorceressSpellMissiles.Commit(SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Fireball),TimedEffects.Now));
    private static SorceressSpellFlight State(long id)=>FireBoltChecks.State(id);
    private static DateTime Finish(long id)=>IceBoltChecks.Finish(id);
    private static void NoCold(Mobile t)=>Require(!TimedEffects.TryActor(t,out var a)||a.Cold==null&&a.ChillPercent==0&&a.Control?.Freeze==null,"Fire Ball applied cold/freeze through typed potion");
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Fireball);
        int Basis(int rank,bool max)
        {int n=max?28:12;for(int level=2;level<=rank;level++){n+=(level<=8?13:level<=16?23:level<=22?28:level<=28?33:38)+(max?2:0);}return n*128;}
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k=="Fire Ball"?rank:200,k=>k is "Fire Bolt" or "Meteor"?hard:0,99);
            Require(v.Mana256==1280+128*(rank-1)&&v.DurationFrames==0&&SorceressSpellMissiles.ExplosionRadius(v)==2
                &&v.Damage256(false)==Basis(rank,false)*(100+28*hard)/100&&v.Damage256(true)==Basis(rank,true)*(100+28*hard)/100,"Independent Fire Ball rank/hard suppliers: "+rank+"/"+hard);
        }
    }
    private static void Check()
    {
        Goldens();var p=Owner();var first=Enemy(p,16);var peer=Enemy(p,16,1);var far=Enemy(p,20);try
        {
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(first),other=SkillHealth.Balance256(peer),outside=SkillHealth.Balance256(far);
            Require(SorceressCombat.Release(p,CombatAbility.Fireball,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Learned Fire Ball release");
            var s=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
            Require(s is{Frame:0,Frames:50,VelocityFp8:3840,Vx:0.46875,Vy:0,Minimum256:1751,Maximum256:4085,ColdFrames:0}
                &&NecromancerEffects.ManaBalance256(p)==mana-1280&&SkillHealth.Balance256(first)==life,"Fire Ball factory/prerequisite synergy/payment");
            FireBoltChecks.Through(s.Id,24);Require(State(s.Id)!=null&&SkillHealth.Balance256(first)==life&&SkillHealth.Balance256(peer)==other,"Fire Ball instant hit");
            Finish(s.Id);int loss=life-SkillHealth.Balance256(first);
            Require(loss>=1751&&loss<4085&&other-SkillHealth.Balance256(peer)==loss&&SkillHealth.Balance256(far)==outside,"Fire Ball rerolled recipients/radius or retained12tile cutoff");NoCold(first);NoCold(peer);
            NecromancerEffects.SetMana256(p,1279);Require(!SorceressCombat.Release(p,CombatAbility.Fireball,Serial.Zero,new(p.X+10,p.Y,p.Z))
                &&NecromancerEffects.ManaBalance256(p)==1279&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Lost mana emitted/paid Fire Ball");
        }
        finally{FireBoltChecks.Clean(p,first,peer,far);}
        Capture();Terminal();Mixed();Controls();Corpse();Save();
        Log.Information("Fire Ball:765independent rank/hard suppliers,50frame/20velocity hit1,one captured copied area packet,actual Eschuta/Fire Mastery/hard-point changes,terminal/wall,typed immunity/potion,no cold status,corpse/lifecycle controls pass");
    }
    private static void Capture()
    {
        var p=Owner();var first=Enemy(p,6);var peer=Enemy(p,6,1);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");long id=Create(p);var captured=State(id);
            Require(captured.Rank>1&&captured.Modifiers.FireMastery>0&&captured.ColdFrames==0,"Actual Eschuta Fire Ball capture");
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Require(State(id)==captured,"Fire Ball reread removed gear");SorceressSpellMissiles.Cancel(p);
            var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Fireball),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
            id=SorceressSpellMissiles.Commit(packet);Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.firebolt",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.firemastery",out _),"Train hard damage/mastery during flight");
            Require(p.EquipItem(orb),"Reequip real Eschuta after launch");ClassGear.Refresh(p);Require(State(id)==packet&&NamedElements.MasteryPercent(p,"fire")>0,"Launch snapshot changed or new mastery absent");
            int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(peer);Finish(id);
            Require(a-SkillHealth.Balance256(first)==25600&&b-SkillHealth.Balance256(peer)==25600,"Fire Ball reread damage/gear/mastery/hard synergy at impact");
            long fresh=Create(p);Require(State(fresh).Rank>1&&State(fresh).Modifiers.FireMastery>0&&State(fresh).Minimum256>1751,"Fresh Fire Ball missed current modifiers");
            var pool=SorceressSpellMissiles.Snapshot();bool refused=false;try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==fresh?x with{ColdFrames=1}:x).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&State(fresh).ColdFrames==0,"Malformed Fire Ball cold payload partially restored");
        }
        finally{FireBoltChecks.Clean(p,first,peer);}
    }
    private static void Terminal()
    {
        var p=Owner();var t=Enemy(p,23,1);try
        {
            int life=SkillHealth.Balance256(t);long id=Create(p);FireBoltChecks.Through(id,49);Require(State(id) is{Frame:49}&&SkillHealth.Balance256(t)==life,"Fire Ball terminal control collided/ended early");
            Finish(id);Require(SkillHealth.Balance256(t)<life,"Active natural Fire Ball expiry failed to explode");NoCold(t);
        }
        finally{FireBoltChecks.Clean(p,t);}
        p=Owner();t=Enemy(p,3,1);var hidden=Enemy(p,5);Item wall=null;try
        {
            wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);int life=SkillHealth.Balance256(t),behind=SkillHealth.Balance256(hidden);long id=Create(p);int frame=0;
            while(State(id) is{} s&&frame<50){frame=s.Frame+1;SorceressSpellMissiles.Tick(s.NextUtc,true);}
            // With this faster native projection the wall stops the missile
            // in front of the solid tile,unlike Glacial's inside-wall case.
            Require(State(id)==null&&frame==10&&SkillHealth.Balance256(t)==life&&SkillHealth.Balance256(hidden)<behind,
                "Fire Ball native wall adapter (rounded far-side center; D2 parity unresolved): frame="+frame+" visibleLoss="+(life-SkillHealth.Balance256(t))+" hiddenLoss="+(behind-SkillHealth.Balance256(hidden)));NoCold(t);NoCold(hidden);
        }
        finally{wall?.Delete();FireBoltChecks.Clean(p,t,hidden);}
    }
    private static void Mixed()
    {
        foreach(string mode in new[]{"none","active","after-launch","removed"})
        {
            var p=Owner();var immune=Enemy(p,6);var plain=Enemy(p,6,1);var half=Enemy(p,6,-1);try
            {
                immune.SetResistance(ResistanceType.Fire,100);half.SetResistance(ResistanceType.Fire,50);if(mode is "active" or "removed"){Require(CombatPotions.Apply(p,3),"Actual Fire Ball potion");}
                var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Fireball),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};long id=SorceressSpellMissiles.Commit(packet);
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Fire Ball potion after launch");}if(mode=="removed"){TimedEffects.Clear(p);}
                int a=SkillHealth.Balance256(immune),b=SkillHealth.Balance256(plain),c=SkillHealth.Balance256(half);Finish(id);bool potion=mode is "active" or "after-launch";
                Require(a-SkillHealth.Balance256(immune)==(potion?7680:0)&&b-SkillHealth.Balance256(plain)==(potion?35840:25600)&&c-SkillHealth.Balance256(half)==(potion?21760:12800),"Fire Ball independent immunity/potion lanes or compounding");
                NoCold(immune);NoCold(plain);NoCold(half);
            }
            finally{FireBoltChecks.Clean(p,immune,plain,half);}
        }
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","offline","facet","slow","death","respec","wardrobe"})
        {
            var p=Owner();var t=Enemy(p,6);try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                int life=SkillHealth.Balance256(t);long id=Create(p);
                if(mode=="miss"){t.MoveToWorld(new(t.X,t.Y+3,t.Z),t.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="slow"){Require(State(id) is{VelocityFp8:1920,Minimum256:875,Maximum256:2042,ColdFrames:0},"Fire Ball Slow Missiles capture");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(State(id)==null,"Death retained Fire Ball");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(State(id)==null,"Respec retained Fire Ball");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&State(id)==null,"Wardrobe retained Fire Ball");}
                FireBoltChecks.Through(id,50,mode!="offline");Require(State(id)==null&&(SkillHealth.Balance256(t)<life)==(mode=="slow"),"Fire Ball lifecycle/no offline explosion: "+mode);
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
        var owner=Owner();try
        {
            int mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.Fireball,null,new(owner.X+10,owner.Y,owner.Z),Core.TickCount),"Fire Ball cancel start");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);
            Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==mana&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==owner.Serial.Value),"Stopped Fire Ball paid/emitted after windup cancellation");
        }
        finally{FireBoltChecks.Clean(owner);}
    }
    private static void Corpse()
    {
        var p=Owner();var first=Enemy(p,6);var peer=Enemy(p,6,1);try
        {
            SkillHealth.SetBalance256(first,256);SkillHealth.SetBalance256(peer,256);Finish(Create(p));
            foreach(var t in new[]{first,peer}){Require(!t.Alive&&t.Corpse is Corpse c&&!CorpseSkillRules.Shattered(c)&&CorpseSkillRules.Usable(p,c,20),"Fire Ball wrongly shattered or consumed a normal corpse");}
        }
        finally{FireBoltChecks.Clean(p,first,peer);}
    }
    private static void Save()
    {
        var p=Owner();var t=Enemy(p,6);var peer=Enemy(p,6,1);int mana=NecromancerEffects.ManaBalance256(p);Finish(Create(p));long id=Create(p);var s=State(id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?s:x).ToArray()});
        var saved=new Saved(p.Serial.Value,t.Serial.Value,peer.Serial.Value,p.Map.MapID,p.Location,mana,SkillHealth.Balance256(t),SkillHealth.Balance256(peer),s,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var held=World.FindMobile((Serial)s.Target);var heldPeer=World.FindMobile((Serial)s.Peer);
        Require(s.Generation<2&&State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(held)==s.Life&&SkillHealth.Balance256(heldPeer)==s.PeerLife
            &&SkillBindings.Instance.Get(p).Right==CombatAbility.Fireball,"Saved Fire Ball flight/wounds/resources/binding");NoCold(held);NoCold(heldPeer);
        var account=FixtureNetwork.Authenticate(network,p,"fire-ball-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var point=held.Location;var peerPoint=heldPeer.Location;held.MoveToWorld(new(p.X+6,p.Y+3,p.Z),p.Map);heldPeer.MoveToWorld(new(p.X+6,p.Y+4,p.Z),p.Map);
        var t=Enemy(p,16);var peer=Enemy(p,16,1);int life=SkillHealth.Balance256(t),other=SkillHealth.Balance256(peer);
        void Start()
        {
            var pool=SorceressSpellMissiles.Snapshot();var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});var deadline=TimedEffects.Now.AddSeconds(5);
            void Observe()
            {
                if(SkillHealth.Balance256(peer)==other&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
                try
                {
                    Require(life-SkillHealth.Balance256(t)>0&&life-SkillHealth.Balance256(t)==other-SkillHealth.Balance256(peer)&&State(copy.Id)==null&&State(s.Flight.Id)==s.Flight,"Resumed Fire Ball did not travel/copy one packet");NoCold(t);NoCold(peer);
                    Require(SkillHealth.Balance256(held)==s.Life&&SkillHealth.Balance256(heldPeer)==s.PeerLife,"Reload reset held Fire Ball wounds");
                    t.Delete();peer.Delete();held.MoveToWorld(point,p.Map);heldPeer.MoveToWorld(peerPoint,p.Map);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("fire-ball-reload-result: passed");
                }
                catch(Exception e){Log.Error(e,"fire-ball-reload-result: failed");}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
        }
        FixtureClock.NextLoop("fire-ball-reload-result",Start);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,16);var peer=Enemy(p,16,1);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),other=SkillHealth.Balance256(peer);string label=quick?"fire-ball-quick":"fire-ball-right";
        var account=FixtureNetwork.Authenticate(network,p,"fire-ball-input-");bool began=false;long deadline=Core.TickCount+7000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Fire Ball real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Fireball&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life&&SkillHealth.Balance256(peer)==other,"Fire Ball input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(peer)==other){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-1280&&life-SkillHealth.Balance256(t)>0&&life-SkillHealth.Balance256(t)==other-SkillHealth.Balance256(peer)
                    &&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Actual Fire Ball payment/copy/collision");NoCold(t);NoCold(peer);
                FireBoltChecks.Clean(p,t,peer);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){FireBoltChecks.Clean(p,t,peer);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.Fireball,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
