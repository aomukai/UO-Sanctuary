// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BindDemonLearnedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BindDemonLearnedChecks));
    private sealed record WireSave(uint Owner,uint Body,uint Helmet,int Source,bool Female,BoundDemonState State,int Life,int Mana,int ExpectedRank);
    private sealed record Saved(int Generation,uint Owner,uint Helmet,int Life,int Mana,WireSave[] Wires);
    private sealed class Wire(PlayerMobile owner,NetState network,Account account,Daemon body,Daemon other,Item helmet,int source,Func<int,int> oldRoll)
    {
        internal readonly PlayerMobile Owner=owner;internal readonly NetState Network=network;internal readonly Account Account=account;
        internal readonly Daemon Body=body,Other=other;internal readonly Item Helmet=helmet;internal readonly int Source=source;
        internal readonly Func<int,int> OldRoll=oldRoll;internal int Phase,Rolls;internal long FirstId;
        internal int ExpectedRank=>Source==1?20:21;
        internal int ExpectedMana=>(Source==1?72:81)*256;
        internal string Label=>"bind-learned-wire-"+Source;
        internal long Deadline=Core.TickCount+12000;
    }
    private static readonly Dictionary<int,Wire> Active=new();
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bind-demon-learned-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BindLearnedFixture"?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>Creating||m.Name=="BindLearnedFixture"?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bind-demon-learned-reload-check":"bind-demon-learned-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved ordinary Bind training/bindings/saved native wire hosts",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
        foreach(int key in new[]{1,9})
        {
            int source=key;string label="bind-learned-wire-"+source;
            ConsoleInputHandler.RegisterCommand(label+"-check","Real approved Bind channel packets and original body commit",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(label+"-result",n=>
            {try{StartWire(n,source);}catch(Exception e){Log.Error(e,"{Name}: failed",label+"-result");}})));
            ConsoleInputHandler.RegisterCommand(label+"-finish","Verify late release/stale heartbeat and save original native binding",_=>Core.LoopContext.Post(()=>FinishWire(source)));
        }
    }
    private static PlayerMobile Fresh()
    {
        Creating=true;PlayerMobile p;try{p=new PlayerMobile{Body=0x190,Name="BindLearnedFixture",Player=true};ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);}finally{Creating=false;}
        p.AddItem(new Backpack());FixtureSpace.Corridor(p,6950,24,3600);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static void Bindings(PlayerMobile p)
    {
        Require(p.AccessLevel==AccessLevel.Player&&WarlockCombat.Implemented(CombatAbility.BindDemon)&&WarlockCombat.Available(p,CombatAbility.BindDemon),"Approved ordinary Bind availability missing");
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.BindDemon)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BindDemon)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BindDemon),"Approved pinned mouse/quick assignments failed");
        Require(ClassCombat.ActionInterval(p,CombatAbility.BindDemon)==1000&&CombatTiming.Calculate(DiabloClass.Warlock,CombatAbility.BindDemon,CombatTiming.Weapon(null),fcr:500).DurationMs==1000,"Approved fixed channel HUD clock changed with FCR");
        Require(!ClassCombat.Perform(p,CombatAbility.BindDemon,null,p.Location,Core.TickCount)&&!WarlockCombat.Begin(p,CombatAbility.BindDemon,Serial.Zero,p.Location,Core.TickCount)
            &&!WarlockCombat.Release(p,CombatAbility.BindDemon,Serial.Zero),"Legacy SC/one-shot path bypassed explicit channel");
    }
    private static void Scene(NetState network)
    {
        var p=Fresh();var account=FixtureNetwork.Authenticate(network,p,"bind-learned-");bool kept=false;
        try
        {
            Require(!WarlockCombat.Available(p,CombatAbility.BindDemon)&&!SkillBindings.Instance.Assign(p,false,CombatAbility.BindDemon),"Untrained Bind was available");
            var helmet=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);Require(!WarlockCombat.Available(p,CombatAbility.BindDemon),"Soft item bonuses bypassed required hard training");
            ClassSkillChecks.Train(p,"warlock.binddemon");Bindings(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-17);NecromancerEffects.SetMana256(p,90*256);
            Require(!EngorgeEffects.Approved&&!BloodBoilPrepared.Approved&&!WarlockCombat.Implemented(CombatAbility.Consume)&&!WarlockCombat.Implemented(CombatAbility.BloodOath),"Bind approval enabled another pending skill");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helmet.Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),[])));kept=true;
            Log.Information("Bind learned:ordinary hard training and approved availability enable right/quick assignments;left/untrained/one-shot/start-release bypasses refuse;four other pending skills remain gated;original owner/gear/resources staged");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many learned Bind reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"bind-learned-reload-");
        try
        {
            Require(SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&World.FindItem((Serial)saved.Helmet)?.Parent==p
                &&SkillBindings.Instance.Get(p).Right==CombatAbility.BindDemon&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==CombatAbility.BindDemon&&BindDemonChannel.State(p)==null,"Original owner/gear/bindings/resources changed on reload");
            foreach(var wire in saved.Wires)
            {
                var owner=(PlayerMobile)World.FindMobile((Serial)wire.Owner);var body=(Daemon)World.FindMobile((Serial)wire.Body);
                Require(owner.Female==wire.Female&&BoundDemonHosts.State(body)==wire.State&&body.ControlMaster==owner&&body.Map==Map.Internal&&body.GetType()==typeof(Daemon)
                    &&SkillHealth.Balance256(body)==wire.Life&&NecromancerEffects.ManaBalance256(owner)==wire.Mana&&World.FindItem((Serial)wire.Helmet)?.Parent==owner.Backpack
                    &&SkillMechanics.Rank(owner,CombatAbility.BindDemon)==20&&wire.State.Birth.Rank==wire.ExpectedRank&&BindDemonChannel.State(owner)==null
                    &&SkillBindings.Instance.Get(owner).Right==CombatAbility.BindDemon&&SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Warlock,7)==CombatAbility.BindDemon,"Saved real wire body/birth/current gear/role/bindings/mana changed or held intent replayed");
            }
            Require(saved.Wires.Length==2*(saved.Generation+1),"Missing preceding native wire cohort");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Bind learned:original owner and all preceding real right/quick native wire bodies preserve serial/type/captured rank/wounds/mana/roles/backpacked gear/bindings;no held intent or offline debit on reload");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static Daemon Body(PlayerMobile p,int offset)
    {
        Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}b.Name="BindLearnedFixture";b.SetHits(10000);b.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();
        MonsterVariantBaselines.Restore(MonsterVariantBaselines.Snapshot().Where(r=>r.Actor!=b.Serial.Value).ToArray());SkillHealth.SetBalance256(b,4000*256-7);return b;
    }
    private static void StartWire(NetState network,int source)
    {
        Require(!Active.ContainsKey(source),"Previous wire still active");var p=Fresh();p.Female=source==9;FixtureSpace.Corridor(p,source==1?6950:7000,24,3700);
        ClassSkillChecks.Train(p,"warlock.binddemon",20);var helmet=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);Bindings(p);
        var account=FixtureNetwork.Authenticate(network,p,"bind-real-wire-");ClassCombat.For(p).NextAction=Core.TickCount-1;NecromancerEffects.SetMana256(p,90*256);
        var body=Body(p,4);var other=Body(p,8);if(source==1){SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body));}
        var wire=new Wire(p,network,account,body,other,helmet,source,BindDemonChannel.Roll);Active.Add(source,wire);BindDemonChannel.Roll=n=>{wire.Rolls++;return 0;};
        Log.Information("{Label}: ready ability={Ability} target={Target} other={Other} x={X} y={Y} z={Z}",wire.Label,(int)CombatAbility.BindDemon,body.Serial.Value,other.Serial.Value,body.X,body.Y,body.Z);
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Observe(wire));
    }
    private static void Observe(Wire w)
    {
        if(!Active.TryGetValue(w.Source,out var active)||!ReferenceEquals(active,w)){return;}
        try
        {
            Require(Core.TickCount<w.Deadline&&w.Network.Running,"Real Bind wire timed out/disconnected");var state=BindDemonChannel.State(w.Owner);int mana=NecromancerEffects.ManaBalance256(w.Owner);
            if(w.Phase==0&&state!=null)
            {
                Require(state.InputBound&&state.Source==w.Source&&state.Target==w.Body.Serial.Value&&state.Attempts==0&&mana==90*256&&!w.Body.Controlled,"First press/negative packets mutated resources or target");w.FirstId=state.Id;w.Phase=1;Log.Information("{Label}: pressed-free",w.Label);
            }
            if(w.Phase==1&&state==null)
            {Require(mana==90*256&&!w.Body.Controlled&&w.Rolls==0,"Release before windup paid/rolled/bound");w.Phase=2;Log.Information("{Label}: released-free",w.Label);}
            if(w.Phase==2&&state!=null)
            {Require(state.Id!=w.FirstId&&state.Source==w.Source&&state.Target==w.Body.Serial.Value&&state.Attempts==0&&mana==90*256,"Fresh press reused prior windup or hovered target");w.Phase=3;Log.Information("{Label}: pressed",w.Label);}
            if(w.Phase==3&&w.Source==1&&state is{Attempts:1})
            {
                Require(mana==81*256&&!w.Body.Controlled&&w.Rolls==0,"Healthy target skipped payment or rolled above strict threshold");
                w.Owner.Backpack.DropItem(w.Helmet);ClassGear.Refresh(w.Owner);Require(BindDemonChannel.State(w.Owner)?.Id==state.Id&&SkillMechanics.Rank(w.Owner,CombatAbility.BindDemon)==20,"Nonweapon gear changed/cancelled the defined channel incorrectly");
                SkillHealth.SetBalance256(w.Body,4000*256-7);w.Phase=4;Log.Information("{Label}: healthy-paid",w.Label);
            }
            if(w.Phase is 3 or 4&&BoundDemonHosts.State(w.Body)is{} born)
            {
                w.Body.AIObject.AITimer.Stop();if(w.Body.Spell is Server.Spells.Spell spell){spell.Disturb(Server.Spells.DisturbType.Kill);}Require(state==null&&w.Rolls==1&&mana==w.ExpectedMana&&born.Birth.Rank==w.ExpectedRank&&born.Normal.Life256==10000*256
                    &&SkillHealth.Balance256(w.Body)==4000*256-7&&!w.Other.Controlled&&BoundDemonHosts.For(w.Owner).Single()==w.Body,"Real bind duplicated debit/RNG,retargeted,healed or captured wrong live rank");
                if(w.Source==9){w.Owner.Backpack.DropItem(w.Helmet);ClassGear.Refresh(w.Owner);Require(BoundDemonHosts.State(w.Body).Birth==born.Birth,"Postbirth gear rewrote captured supplier");}
                w.Phase=5;Log.Information("{Label}: bound",w.Label);
            }
            if(w.Phase==5){Require(state==null&&mana==w.ExpectedMana&&!w.Other.Controlled,"Stale heartbeat restarted channel or charged after binding");}
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Observe(w));
        }
        catch(Exception e){Cleanup(w,false);Log.Error(e,"{Name}: failed",w.Label+"-result");}
    }
    private static void FinishWire(int source)
    {
        if(!Active.TryGetValue(source,out var w)){Log.Error("{Name}: failed", "bind-learned-wire-"+source+"-result");return;}
        try
        {
            Require(w.Phase==5&&ClassCombat.For(w.Owner).Sequence>=20&&BindDemonChannel.State(w.Owner)==null&&NecromancerEffects.ManaBalance256(w.Owner)==w.ExpectedMana&&w.Rolls==1,"Late release/stale/replay sequence changed completed bind");
            PersistentMinions.Suspend(w.Owner);var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var born=BoundDemonHosts.State(w.Body);
            var row=new WireSave(w.Owner.Serial.Value,w.Body.Serial.Value,w.Helmet.Serial.Value,source,w.Owner.Female,born,SkillHealth.Balance256(w.Body),NecromancerEffects.ManaBalance256(w.Owner),w.ExpectedRank);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Wires=saved.Wires.Append(row).ToArray()}));Cleanup(w,true);
            Log.Information("Bind learned wire:source{Source} actual packets reject left/legacy routes,replay/duplicate and free release;fresh windup/locked target/healthy gate,current summon snapshot/one successful commit and original saved native body verified",source);
            Log.Information("{Name}: passed",w.Label+"-result");
        }
        catch(Exception e){Cleanup(w,false);Log.Error(e,"{Name}: failed",w.Label+"-result");}
    }
    private static void Cleanup(Wire w,bool keep)
    {
        if(!Active.Remove(w.Source)){return;}
        BindDemonChannel.Roll=w.OldRoll;BindDemonChannel.Cancel(w.Owner);w.Other.Delete();w.Network.Mobile=null;w.Owner.NetState=null;w.Account[0]=null;w.Network.Account=null;w.Account.Delete();
        if(!keep){w.Body.Delete();w.Owner.Delete();BoundDemonHosts.Tick();}
    }
}
