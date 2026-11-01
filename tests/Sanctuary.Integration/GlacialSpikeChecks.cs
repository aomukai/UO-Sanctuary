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

public static class GlacialSpikeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GlacialSpikeChecks));
    private sealed record Saved(uint Owner,uint Target,uint Peer,int Map,Point3D Point,int Mana,int Life,int PeerLife,
        SorceressSpellFlight Flight,NamedControlState Control,NamedControlState PeerControl,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"glacial-spike-fixtures.json");
    public static void Configure()
    {
        Register("glacial-spike-check",Check);Register("glacial-spike-reload-check",()=>FixtureNetwork.Connected("glacial-spike-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"glacial-spike-quick":"glacial-spike-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player Glacial Spike travel,copied area,freeze,input and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        string id=SkillMechanics.For(ability).TalentId;TalentCatalog.TryGet(DiabloClass.Sorceress,id,out var row);
        foreach(string pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==pre).Ability);}}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.IceBolt,6950);p.Name="GlacialSpikeFixture";Train(p,CombatAbility.GlacialSpike);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.GlacialSpike)&&SkillBindings.Instance.Assign(p,false,CombatAbility.GlacialSpike)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.GlacialSpike),"Glacial bindings");return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int dx,int dy=0)
    {var t=FireBoltChecks.Target(p,dx);t.MoveToWorld(new(t.X,t.Y+dy,t.Z),t.Map);return t;}
    private static long Create(PlayerMobile p)=>SorceressSpellMissiles.Commit(SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.GlacialSpike),TimedEffects.Now));
    private static SorceressSpellFlight State(long id)=>FireBoltChecks.State(id);
    private static DateTime Finish(long id)=>IceBoltChecks.Finish(id);
    private static NamedControlState Frozen(Mobile t)
    {var c=NamedControl.State(t);Require(c?.Freeze is{Removed:false},"Missing Glacial source freeze");Require(!TimedEffects.TryActor(t,out var a)||a.Cold==null&&a.ChillPercent==0,"Glacial added duplicate chill");return c;}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.GlacialSpike);string[] damage=["Ice Bolt","Ice Blast","Frozen Orb"];
        int Basis(int rank,bool max)
        {int n=max?48:32;for(int level=2;level<=rank;level++){n+=(level<=8?14:level<=16?26:level<=22?28:level<=28?30:32)+(max?1:0);}return n*128;}
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k=="Glacial Spike"?rank:200,k=>damage.Contains(k)||k=="Blizzard"?hard:0,99);
            Require(v.Mana256==2560+128*(rank-1)&&v.DurationFrames==0&&GlacialSpikes.Radius(v)==2
                &&GlacialSpikes.FreezeFrames(v)==(50+3*(rank-1))*(100+3*hard)/100
                &&v.Damage256(false)==Basis(rank,false)*(100+15*hard)/100&&v.Damage256(true)==Basis(rank,true)*(100+15*hard)/100,"Independent Glacial rank/hard suppliers: "+rank+"/"+hard);
        }
    }
    private static void Check()
    {
        Goldens();var p=Owner();var first=Enemy(p,14);var peer=Enemy(p,14,1);var far=Enemy(p,18);try
        {
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(first),other=SkillHealth.Balance256(peer),outside=SkillHealth.Balance256(far);
            Require(SorceressCombat.Release(p,CombatAbility.GlacialSpike,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Learned Glacial ground release");
            var s=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
            Require(s is{Frame:0,Frames:40,VelocityFp8:3072,Vx:0.375,Vy:0,Minimum256:4505,Maximum256:6758,ColdFrames:0}
                &&NecromancerEffects.ManaBalance256(p)==mana-2560&&SkillHealth.Balance256(first)==life,"Glacial factory/prerequisite synergies/payment");
            FireBoltChecks.Through(s.Id,24);Require(State(s.Id)!=null&&SkillHealth.Balance256(first)==life&&SkillHealth.Balance256(peer)==other,"Glacial instant/faster hit");
            DateTime at=Finish(s.Id);int loss=life-SkillHealth.Balance256(first);
            Require(loss>=4505&&loss<6758&&other-SkillHealth.Balance256(peer)==loss&&SkillHealth.Balance256(far)==outside
                &&Math.Abs((Frozen(first).FreezeUntil-at).TotalMilliseconds-2000)<2&&Frozen(peer).Freeze.Source==p.Serial.Value,"Glacial did not share one raw area roll/radius/freeze or retained12tile cutoff");
            NecromancerEffects.SetMana256(p,2559);Require(!SorceressCombat.Release(p,CombatAbility.GlacialSpike,Serial.Zero,new(p.X+10,p.Y,p.Z))
                &&NecromancerEffects.ManaBalance256(p)==2559&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Lost mana emitted/paid Glacial");
        }
        finally{FireBoltChecks.Clean(p,first,peer,far);}
        Capture();Terminal();Mixed();Controls();Corpse();Save();
        Log.Information("Glacial Spike:765independent rank/hard suppliers,40frame/16velocity,captured damage/single raw area roll,live hit13 owner length,actual gear/hard changes,terminal/wall/offline,typed immunity,predamage freeze/corpse/lifecycle controls pass");
    }
    private static void Capture()
    {
        var p=Owner();var first=Enemy(p,6);var peer=Enemy(p,6,1);try
        {
            var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");long id=Create(p);var captured=State(id);
            Require(captured.Rank==3&&captured.ColdFrames==0&&captured.Modifiers.ColdMastery>0,"Actual Glacial Nightwing capture");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(State(id)==captured,"Glacial gear removal rewrote packet");SorceressSpellMissiles.Cancel(p);
            var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.GlacialSpike),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
            id=SorceressSpellMissiles.Commit(packet);Train(p,CombatAbility.Blizzard);Train(p,CombatAbility.FrozenOrb);Require(p.EquipItem(helm),"Reequip real Nightwing after launch");ClassGear.Refresh(p);
            Require(State(id)==packet&&SkillMechanics.Rank(p,CombatAbility.GlacialSpike)==3,"Launch snapshot changed or current soft rank absent");
            int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(peer);DateTime at=Finish(id);
            Require(a-SkillHealth.Balance256(first)==25600&&b-SkillHealth.Balance256(peer)==25600
                &&Math.Abs((Frozen(first).FreezeUntil-at).TotalMilliseconds-2040)<2,"Glacial reread damage/gear or failed live hard Blizzard at retained rank");
            long fresh=Create(p);Require(State(fresh).Rank==3&&State(fresh).Modifiers.ColdMastery>0&&State(fresh).Minimum256>4505,"Fresh Glacial missed current gear/hard damage synergy");
            var pool=SorceressSpellMissiles.Snapshot();bool refused=false;
            try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==fresh?x with{Frames=50}:x).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&State(fresh).Frames==40,"Malformed Glacial duration partially restored");
        }
        finally{FireBoltChecks.Clean(p,first,peer);}
    }
    private static void Terminal()
    {
        var p=Owner();var t=Enemy(p,15,1);try
        {
            int life=SkillHealth.Balance256(t);long id=Create(p);FireBoltChecks.Through(id,39);
            Require(State(id) is{Frame:39}&&SkillHealth.Balance256(t)==life,"Glacial terminal control collided/ended early");
            Finish(id);Require(SkillHealth.Balance256(t)<life&&Frozen(t).Freeze.Source==p.Serial.Value,"Active natural Glacial expiry did not explode/freeze");
        }
        finally{FireBoltChecks.Clean(p,t);}
        p=Owner();t=Enemy(p,4,1);Item wall=null;try
        {
            wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);int life=SkillHealth.Balance256(t);long id=Create(p);int frame=0;
            while(State(id) is{} s&&frame<40){frame=s.Frame+1;SorceressSpellMissiles.Tick(s.NextUtc,true);}
            Require(State(id)==null&&frame<20&&SkillHealth.Balance256(t)==life&&NamedControl.State(t)?.Freeze==null,"Glacial wall removal/inside-wall native area occlusion");
        }
        finally{wall?.Delete();FireBoltChecks.Clean(p,t);}
    }
    private static void Mixed()
    {
        foreach(string mode in new[]{"none","active","after-launch","removed"})
        {
            var p=Owner();var immune=Enemy(p,6);var plain=Enemy(p,6,1);var half=Enemy(p,6,-1);try
            {
                immune.SetResistance(ResistanceType.Cold,100);half.SetResistance(ResistanceType.Cold,50);
                if(mode is "active" or "removed"){Require(CombatPotions.Apply(p,3),"Actual Glacial potion");}
                var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.GlacialSpike),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};long id=SorceressSpellMissiles.Commit(packet);
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Glacial potion after launch");}if(mode=="removed"){TimedEffects.Clear(p);}
                int a=SkillHealth.Balance256(immune),b=SkillHealth.Balance256(plain),c=SkillHealth.Balance256(half);DateTime at=Finish(id);bool potion=mode is "active" or "after-launch";
                Require(a-SkillHealth.Balance256(immune)==(potion?7680:0)&&b-SkillHealth.Balance256(plain)==(potion?35840:25600)&&c-SkillHealth.Balance256(half)==(potion?21760:12800),"Glacial per-recipient immunity/potion mitigation or compounding");
                Require(NamedControl.State(immune)?.Freeze==null&&Math.Abs((Frozen(plain).FreezeUntil-at).TotalMilliseconds-2000)<2
                    &&Math.Abs((Frozen(half).FreezeUntil-at).TotalMilliseconds-1000)<2,"Glacial immune/resisted recipient freeze");
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
                if(mode=="slow"){Require(State(id) is{VelocityFp8:1536,Minimum256:2252,Maximum256:3379,ColdFrames:0},"Glacial Slow Missiles capture");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(State(id)==null,"Death retained Glacial");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(State(id)==null,"Respec retained Glacial");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&State(id)==null,"Wardrobe retained Glacial");}
                FireBoltChecks.Through(id,50,mode!="offline");Require(State(id)==null&&(SkillHealth.Balance256(t)<life)==(mode=="slow"),"Glacial lifecycle/no offline explosion: "+mode);
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Corpse()
    {
        var p=Owner();var first=Enemy(p,6);var peer=Enemy(p,6,1);try
        {
            SkillHealth.SetBalance256(first,256);SkillHealth.SetBalance256(peer,256);Finish(Create(p));
            foreach(var t in new[]{first,peer}){Require(!t.Alive&&t.Corpse is Corpse c&&CorpseSkillRules.Shattered(c)&&!CorpseSkillRules.Usable(p,c,20),"Glacial per-recipient freeze followed lethal damage");}
        }
        finally{FireBoltChecks.Clean(p,first,peer);}
    }
    private static NamedControlState Hold(Mobile t,DateTime until)
    {var c=Frozen(t);var held=c with{FreezeUntil=until,Freeze=c.Freeze with{AiUntilUtc=until.AddMilliseconds(40)}};TimedEffects.Actor(t,TimedEffects.Now).Control=held;return held;}
    private static void Save()
    {
        var p=Owner();var t=Enemy(p,6);var peer=Enemy(p,6,1);int mana=NecromancerEffects.ManaBalance256(p);Finish(Create(p));DateTime until=TimedEffects.Now.Add(FixtureSpace.Retention);
        var a=Hold(t,until);var b=Hold(peer,until);long id=Create(p);var s=State(id) with{NextUtc=until};var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?s:x).ToArray()});
        var saved=new Saved(p.Serial.Value,t.Serial.Value,peer.Serial.Value,p.Map.MapID,p.Location,mana,SkillHealth.Balance256(t),SkillHealth.Balance256(peer),s,a,b,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var held=World.FindMobile((Serial)s.Target);var heldPeer=World.FindMobile((Serial)s.Peer);
        Require(s.Generation<2&&State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(held)==s.Life&&SkillHealth.Balance256(heldPeer)==s.PeerLife
            &&NamedControl.State(held)==s.Control&&NamedControl.State(heldPeer)==s.PeerControl&&SkillBindings.Instance.Get(p).Right==CombatAbility.GlacialSpike,"Saved Glacial flight/area wounds/freezes/resources/binding");
        var account=FixtureNetwork.Authenticate(network,p,"glacial-spike-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var point=held.Location;var peerPoint=heldPeer.Location;
        held.MoveToWorld(new(p.X+6,p.Y+3,p.Z),p.Map);heldPeer.MoveToWorld(new(p.X+6,p.Y+4,p.Z),p.Map);var t=Enemy(p,13);var peer=Enemy(p,13,1);int life=SkillHealth.Balance256(t),other=SkillHealth.Balance256(peer);
        void Start()
        {
            var pool=SorceressSpellMissiles.Snapshot();var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});var deadline=TimedEffects.Now.AddSeconds(5);
            void Observe()
            {
                if(SkillHealth.Balance256(peer)==other&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
                try
                {
                    Require(life-SkillHealth.Balance256(t)>0&&life-SkillHealth.Balance256(t)==other-SkillHealth.Balance256(peer)&&Frozen(t).Freeze.Source==p.Serial.Value&&Frozen(peer).Freeze.Source==p.Serial.Value
                        &&State(copy.Id)==null&&State(s.Flight.Id)==s.Flight,"Post-load Glacial did not travel/copy one packet/freeze");
                    Require(SkillHealth.Balance256(held)==s.Life&&SkillHealth.Balance256(heldPeer)==s.PeerLife&&NamedControl.State(held)==s.Control&&NamedControl.State(heldPeer)==s.PeerControl,"Reload refreshed held wounds/freezes");
                    t.Delete();peer.Delete();held.MoveToWorld(point,p.Map);heldPeer.MoveToWorld(peerPoint,p.Map);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("glacial-spike-reload-result: passed");
                }
                catch(Exception e){Log.Error(e,"glacial-spike-reload-result: failed");}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
        }
        FixtureClock.NextLoop("glacial-spike-reload-result",Start);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,14);var peer=Enemy(p,14,1);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),other=SkillHealth.Balance256(peer);string label=quick?"glacial-spike-quick":"glacial-spike-right";
        var account=FixtureNetwork.Authenticate(network,p,"glacial-spike-input-");bool began=false;long deadline=Core.TickCount+7000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Glacial real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.GlacialSpike&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life&&SkillHealth.Balance256(peer)==other,"Glacial input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(peer)==other){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-2560&&life-SkillHealth.Balance256(t)>0&&life-SkillHealth.Balance256(t)==other-SkillHealth.Balance256(peer)
                    &&Frozen(t).Freeze.Source==p.Serial.Value&&Frozen(peer).Freeze.Source==p.Serial.Value&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Actual Glacial payment/one area packet/freeze");
                FireBoltChecks.Clean(p,t,peer);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){FireBoltChecks.Clean(p,t,peer);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.GlacialSpike,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
