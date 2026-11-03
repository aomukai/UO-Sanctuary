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
public static class InfernoModeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(InfernoModeChecks));
    private sealed record Saved(uint Owner,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"inferno-mode-fixture.json");
    private static bool Creating;
    private static int Scene;
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Rates()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="InfernoModeFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    private static void Register(string n,Action run)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Inferno mode and real timer",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure()
    {
        Rates();Register("inferno-mode-check",Guard);Register("inferno-mode-reload-check",Reload);
        Register("inferno-mode-live-check",()=>FixtureClock.NextLoop("inferno-mode-live-result",()=>FixtureNetwork.Connected("inferno-mode-live-result",Live)));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Guard()
    {
        var p=new PlayerMobile{Name="InfernoModeGuard",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"sorceress.inferno");FixtureSpace.Corridor(p,6280,20,3500);
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);
            Require(!PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,new(p.X+8,p.Y,p.Z),TimedEffects.Now)
                &&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Disconnected prepared channel mutated state/resources");
            Require(!SorceressCombat.Implemented(CombatAbility.Inferno),"Prepared mode enabled learned input");
        }
        finally{p.Delete();}
    }
    private static SorceressSpellFlight[] Own(PlayerMobile p)=>SorceressSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Reset(PlayerMobile p,SorceressSpellSave held,int mana=2000)
    {PlayerInfernoModes.Cancel(p);SorceressSpellMissiles.Restore(held);ClassCombat.Stop(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount-1;NecromancerEffects.SetMana256(p,mana);}
    private static void Cases(PlayerMobile p)
    {
        var held=SorceressSpellMissiles.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);var point=new Point3D(p.X+10,p.Y,p.Z);
        try
        {
            Reset(p,held,1535);Require(!PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at)&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==1535,"Fresh threshold mutated state");
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at)&&NecromancerEffects.ManaBalance256(p)==1904,"Fresh96FP start debit");
            string first=JsonSerializer.Serialize(PlayerInfernoModes.State(p));
            try{PlayerInfernoModes.StartCallback(p,at.AddMilliseconds(40));throw new InvalidOperationException("Early rewind accepted");}catch(NotSupportedException){}
            Require(NecromancerEffects.ManaBalance256(p)==1904&&Own(p).Length==0,"Early reentry touched mana/emissions");
            PlayerInfernoModes.Advance(p,at.AddMilliseconds(400));Require(Own(p).Length==0&&PlayerInfernoModes.State(p).Sequence.Channel.Param1==1,"First do did not suppress");
            Require(PlayerInfernoModes.StartCallback(p,at.AddMilliseconds(400))&&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==1808,"Accepted reentry did not emit/pay");
            Require(Own(p).Single().NextUtc==at.AddMilliseconds(440),"Reentry emission lost its event phase");
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at),"Stop setup");PlayerInfernoModes.StopIntent(p,at.AddMilliseconds(40));PlayerInfernoModes.Advance(p,at.AddMilliseconds(440));
            Require(Own(p).Length==1&&!PlayerInfernoModes.State(p).Sequence.Channel.Active&&NecromancerEffects.ManaBalance256(p)==1904,"Player stop incorrectly used Fury conditional rewind");
            PlayerInfernoModes.Interrupt(p);Require(!PlayerInfernoModes.Active(p)&&PlayerInfernoModes.State(p).Sequence.Channel.Lease&&Own(p).Length==1,"Interrupt erased released stream/lease");
            PlayerInfernoModes.Advance(p,at.AddMilliseconds(1000));Require(PlayerInfernoModes.State(p)==null&&Own(p).Length==1,"Lease expiry cancelled released stream");
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at),"Move setup");var origin=p.Location;
            p.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),p.Map);ClassMovement.Moved(p,Direction.East);p.MoveToWorld(origin,p.Map);
            Require(!PlayerInfernoModes.Active(p)&&PlayerInfernoModes.State(p).Sequence.Channel.Lease,"Move away/return revived channel");
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at),"Selected weapon setup");var sword=new Longsword();ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));p.AddToBackpack(sword);Require(p.EquipItem(sword),"Selected weapon fixture");
            Require(!PlayerInfernoModes.Active(p),"Weapon change retained active mode");p.AddToBackpack(sword);sword.Delete();
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at),"Soft rank setup");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm)&&PlayerInfernoModes.Active(p),"Nonweapon gear cancelled channel");
            PlayerInfernoModes.Advance(p,at.AddMilliseconds(440));Require(Own(p).Single().Rank==2&&Own(p).Single().Minimum256==480,"Emission cached old rank");p.AddToBackpack(helm);helm.Delete();
            Reset(p,held);var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);
            Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,target.Serial,point,at),"Target aim setup");target.MoveToWorld(new(p.X,p.Y+6,p.Z),p.Map);PlayerInfernoModes.Advance(p,at.AddMilliseconds(440));
            Require(Own(p).Single().Vy>0&&Math.Abs(Own(p).Single().Vx)<0.01,"Live target was not requeried");target.Delete();PlayerInfernoModes.Advance(p,at.AddMilliseconds(480));
            Require(Own(p).Last().Vx>0&&Math.Abs(Own(p).Last().Vy)<0.01,"Deleted target did not use retained point");
            Require(PlayerInfernoModes.Aim(p,Serial.Zero,new(p.X,p.Y-6,p.Z)),"Free aim update refused");PlayerInfernoModes.Advance(p,at.AddMilliseconds(520));Require(Own(p).Last().Vy<0,"Updated aim was not consumed");
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at),"FHR setup");long recovery=ClassCombat.For(p).RecoveryUntil;HitRecovery.Block(p,Core.TickCount);Require(PlayerInfernoModes.Active(p)&&ClassCombat.For(p).RecoveryUntil==recovery,"Ordinary pending-mode block policy differs");Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&!PlayerInfernoModes.Active(p),"FHR retained mode");ClassCombat.For(p).RecoveryUntil=Core.TickCount-1;
            Reset(p,held);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,at),"Respec setup");ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(PlayerInfernoModes.State(p)==null,"Respec retained mode");ClassSkillChecks.Train(p,"sorceress.inferno");
            Require(!SorceressCombat.Implemented(CombatAbility.Inferno),"Prepared mode enabled learned channel");
            Log.Information("Prepared Inferno mode:threshold96FP,debit/first suppression,reentry/stop/expiry,phase timestamps,move/equipment/FHR/respec,live rank and target/deletion/free aim pass");
        }
        finally{PlayerInfernoModes.Cancel(p);SorceressSpellMissiles.Restore(held);}
    }
    private static void Reload()
    {var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(World.FindMobile((Serial)s.Owner) is PlayerMobile p&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==s.Mana256,"Transient mode or exact player mana changed on reload");}
    private static void Live(NetState net)
    {
        PlayerMobile p;Creating=true;try{p=new PlayerMobile{Name="InfernoModeFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"sorceress.inferno");FixtureSpace.Corridor(p,6280,20,2100+32*Scene++);}finally{Creating=false;}
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture strength");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Fixture dexterity");}
        net.Seeded=net.SentFirstPacket=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();
        var account=new Account("inferno-mode-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;net.Account=account;
        Cases(p);var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.SetResistance(ResistanceType.Fire,0);enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();int life=SkillHealth.Balance256(enemy);
        NecromancerEffects.SetMana256(p,2000);Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,enemy.Location,TimedEffects.Now),"Natural mode entry");long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(net.Running&&Core.TickCount<deadline,"Automatic mode expiry timeout");
                if(PlayerInfernoModes.State(p)!=null||Own(p).Length!=0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(SkillHealth.Balance256(enemy)<life&&ColdStates.OtherRate(enemy)==0&&NecromancerEffects.ManaBalance256(p)==1904,"Natural source mode fire damage/no cold/start-only payment control differs");enemy.Delete();
                Require(PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,new(p.X+8,p.Y,p.Z),TimedEffects.Now),"Offline mode setup");
#pragma warning disable CS0618
                net.Dispose();
#pragma warning restore CS0618
                Timer.StartTimer(TimeSpan.FromMilliseconds(120),()=>
                {try{Require(PlayerInfernoModes.State(p)==null,"Disconnect retained mode");File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p))));Log.Information("inferno-mode-live-result: passed");}catch(Exception e){Log.Error(e,"inferno-mode-live-result: failed");}});
            }
            catch(Exception e){PlayerInfernoModes.Cancel(p);enemy.Delete();Log.Error(e,"inferno-mode-live-result: failed");}
        }
        Log.Information("inferno-mode-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);
    }
}
