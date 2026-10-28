// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class ArcticModeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ArcticModeChecks));
    private sealed record Saved(uint Owner,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"arctic-mode-fixture.json");
    private static bool Creating;
    private static int Scene;
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Rates()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="ArcticModeFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    private static void Register(string n,Action run)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Arctic mode and real timer",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure()
    {
        Rates();Register("arctic-mode-rate-check",RateCheck);Register("arctic-mode-reload-check",Reload);
        Register("arctic-mode-live-check",()=>FixtureClock.NextLoop("arctic-mode-live-result",()=>FixtureNetwork.Connected("arctic-mode-live-result",Live)));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void RateCheck()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"player-inferno-rate-source-probe.json")));int count=0;
        foreach(var c in source.RootElement.EnumerateArray())
        {
            var ability=c[0].GetInt32()==0?CombatAbility.Inferno:CombatAbility.ArcticBlast;
            var s=PlayerInfernoSequence.EnterFresh(1000,ability,1,PlayerInfernoCostModel.CurrentTablePerInvocation,new(()=>4096,_=>true,()=>true,()=>true,_=>true)).State;
            Require(s.Animation.Speed==c[4].GetInt32()&&s.Animation.FrameCount256==c[7].GetInt32()
                &&s.Animation.Events.Single(x=>x.Type==SourceAnimationEvents.ModeChange).Frame==1000+c[8].GetInt32()
                &&s.Animation.Events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame==1000+c[9].GetInt32(),"Current bridge differs from complete rate router");count++;
        }
        Require(count==144,"Missing source rate controls");Log.Information("Arctic/Inferno:144ordinary source rate/scheduler controls match sequence256/action10/end15; no FCR queries");
    }
    private static ArcticMissile[] Own(PlayerMobile p)=>DruidArcticStream.Snapshot().Missiles.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Reset(PlayerMobile p,ArcticStreamSave held,int mana=2000)
    {DruidArcticModes.Cancel(p);DruidArcticStream.Restore(held);ClassCombat.Stop(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount-1;NecromancerEffects.SetMana256(p,mana);}
    private static void Cases(PlayerMobile p)
    {
        var held=DruidArcticStream.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);var point=new Point3D(p.X+10,p.Y,p.Z);
        try
        {
            Reset(p,held,1535);Require(!DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at)&&DruidArcticModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==1535,"Fresh threshold mutated state");
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at)&&NecromancerEffects.ManaBalance256(p)==1904,"Fresh96FP start debit");
            string first=JsonSerializer.Serialize(DruidArcticModes.State(p));
            try{DruidArcticModes.StartCallback(p,at.AddMilliseconds(40));throw new InvalidOperationException("Early rewind accepted");}catch(NotSupportedException){}
            Require(NecromancerEffects.ManaBalance256(p)==1904&&Own(p).Length==0,"Early reentry touched mana/emissions");
            DruidArcticModes.Advance(p,at.AddMilliseconds(400));Require(Own(p).Length==0&&DruidArcticModes.State(p).Sequence.Channel.Param1==1,"First do did not suppress");
            Require(DruidArcticModes.StartCallback(p,at.AddMilliseconds(400))&&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==1808,"Accepted reentry did not emit/pay");
            Require(Own(p).Single().NextUtc==at.AddMilliseconds(440),"Reentry emission lost its event phase");
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at),"Stop setup");DruidArcticModes.StopIntent(p,at.AddMilliseconds(40));DruidArcticModes.Advance(p,at.AddMilliseconds(440));
            Require(Own(p).Length==1&&!DruidArcticModes.State(p).Sequence.Channel.Active&&NecromancerEffects.ManaBalance256(p)==1904,"Player stop incorrectly used Fury conditional rewind");
            DruidArcticModes.Interrupt(p);Require(!DruidArcticModes.Active(p)&&DruidArcticModes.State(p).Sequence.Channel.Lease&&Own(p).Length==1,"Interrupt erased released stream/lease");
            DruidArcticModes.Advance(p,at.AddMilliseconds(1000));Require(DruidArcticModes.State(p)==null&&Own(p).Length==1,"Lease expiry cancelled released stream");
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at),"Move setup");var origin=p.Location;
            p.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),p.Map);ClassMovement.Moved(p,Direction.East);p.MoveToWorld(origin,p.Map);
            Require(!DruidArcticModes.Active(p)&&DruidArcticModes.State(p).Sequence.Channel.Lease,"Move away/return revived channel");
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at),"Selected weapon setup");var sword=new Longsword();ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));p.AddToBackpack(sword);Require(p.EquipItem(sword),"Selected weapon fixture");
            Require(!DruidArcticModes.Active(p),"Weapon change retained active mode");p.AddToBackpack(sword);sword.Delete();
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at),"Soft rank setup");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm)&&DruidArcticModes.Active(p),"Nonweapon gear cancelled channel");
            DruidArcticModes.Advance(p,at.AddMilliseconds(440));Require(Own(p).Single().Rank==2&&Own(p).Single().Damage.Minimum256==416,"Emission cached old rank");p.AddToBackpack(helm);helm.Delete();
            Reset(p,held);var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);
            Require(DruidArcticModes.BeginPrepared(p,target.Serial,point,at),"Target aim setup");target.MoveToWorld(new(p.X,p.Y+6,p.Z),p.Map);DruidArcticModes.Advance(p,at.AddMilliseconds(440));
            Require(Own(p).Single().Vy>0&&Math.Abs(Own(p).Single().Vx)<0.01,"Live target was not requeried");target.Delete();DruidArcticModes.Advance(p,at.AddMilliseconds(480));
            Require(Own(p).Last().Vx>0&&Math.Abs(Own(p).Last().Vy)<0.01,"Deleted target did not use retained point");
            Require(DruidArcticModes.Aim(p,Serial.Zero,new(p.X,p.Y-6,p.Z)),"Free aim update refused");DruidArcticModes.Advance(p,at.AddMilliseconds(520));Require(Own(p).Last().Vy<0,"Updated aim was not consumed");
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at),"FHR setup");long recovery=ClassCombat.For(p).RecoveryUntil;HitRecovery.Block(p,Core.TickCount);Require(DruidArcticModes.Active(p)&&ClassCombat.For(p).RecoveryUntil==recovery,"Ordinary pending-mode block policy differs");Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&!DruidArcticModes.Active(p),"FHR retained mode");ClassCombat.For(p).RecoveryUntil=Core.TickCount-1;
            Reset(p,held);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,point,at),"Respec setup");ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(DruidArcticModes.State(p)==null,"Respec retained mode");RecyclerVineChecks.Train(p,"druid.arcticblast");
            Require(!DruidCombat.Implemented(CombatAbility.ArcticBlast),"Prepared mode enabled learned channel");
            Log.Information("Prepared Arctic mode:threshold96FP,debit/first suppression,reentry/stop/expiry,phase timestamps,move/equipment/FHR/respec,live rank and target/deletion/free aim pass");
        }
        finally{DruidArcticModes.Cancel(p);DruidArcticStream.Restore(held);}
    }
    private static void Reload()
    {var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(World.FindMobile((Serial)s.Owner) is PlayerMobile p&&DruidArcticModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==s.Mana256,"Transient mode or exact player mana changed on reload");}
    private static void Live(NetState net)
    {
        PlayerMobile p;Creating=true;try{p=new PlayerMobile{Name="ArcticModeFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);RecyclerVineChecks.Train(p,"druid.arcticblast");FixtureSpace.Corridor(p,6195,20,2100+32*Scene++);}finally{Creating=false;}
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture strength");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Fixture dexterity");}
        net.Seeded=net.SentFirstPacket=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();
        var account=new Account("arctic-mode-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;net.Account=account;
        Cases(p);var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.SetResistance(ResistanceType.Cold,0);enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();int life=SkillHealth.Balance256(enemy);
        NecromancerEffects.SetMana256(p,2000);Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,enemy.Location,TimedEffects.Now),"Natural mode entry");long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(net.Running&&Core.TickCount<deadline,"Automatic mode expiry timeout");
                if(DruidArcticModes.State(p)!=null||Own(p).Length!=0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(SkillHealth.Balance256(enemy)<life&&ColdStates.OtherRate(enemy)<0&&NecromancerEffects.ManaBalance256(p)==1904,"Natural source mode damage/chill/start-only payment control differs");enemy.Delete();
                Require(DruidArcticModes.BeginPrepared(p,Serial.Zero,new(p.X+8,p.Y,p.Z),TimedEffects.Now),"Offline mode setup");
#pragma warning disable CS0618
                net.Dispose();
#pragma warning restore CS0618
                Timer.StartTimer(TimeSpan.FromMilliseconds(120),()=>
                {try{Require(DruidArcticModes.State(p)==null,"Disconnect retained mode");File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p))));Log.Information("arctic-mode-live-result: passed");}catch(Exception e){Log.Error(e,"arctic-mode-live-result: failed");}});
            }
            catch(Exception e){DruidArcticModes.Cancel(p);enemy.Delete();Log.Error(e,"arctic-mode-live-result: failed");}
        }
        Log.Information("arctic-mode-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);
    }
}
