// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class RavenSnapshotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RavenSnapshotChecks));
    private sealed record Actor(uint Owner,uint Body,int Hard,int OwnerHard,int Life,int Mana,long Order,string State);
    private sealed record Saved(Actor[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"raven-snapshot-fixtures.json");
    private static bool Creating;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="RavenSnapshotOwner"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();Register("raven-snapshot-check",Check);Register("raven-snapshot-reload-check",Reload);
        Register("raven-snapshot-live",()=>FixtureClock.NextLoop("raven-snapshot-live-result",()=>FixtureNetwork.Connected("raven-snapshot-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved Raven birth-stat policy and historical JSON",_=>Core.LoopContext.Post(()=>
    {try{action();if(name!="raven-snapshot-live"){Log.Information("{Name}: passed",name);}}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int hard)
    {
        Creating=true;
        try
        {
            var p=SpiritWolfWorldChecks.Fresh(column:6320);p.Name="RavenSnapshotOwner";
            ClassSkillChecks.Train(p,"druid.raven",hard);PoisonDaggerChecks.Named(p,"Tarnhelm");
            Require(Mobile.GetManaRegenRate(p)==TimeSpan.FromHours(1),"Raven fixture mana hold");return p;
        }
        finally{Creating=false;}
    }
    private static void TrainAll(PlayerMobile p)
    {foreach(string id in new[]{"raven","summonspiritwolf","summondirewolf","summongrizzly"}){ClassSkillChecks.Train(p,"druid."+id,20);}}
    private static DruidRavenBody Cast(PlayerMobile p)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);var seed=DruidRavenCasting.SeedLow;
        try
        {
            DruidRavenCasting.SeedLow=()=>1;
            Require(ClassCombat.Perform(p,CombatAbility.Raven,null,p.Location,Core.TickCount),"Public Raven windup");ClassCombat.ReleaseCast(p);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Once-only Raven release");
        }
        finally{DruidRavenCasting.SeedLow=seed;}
        var b=DruidRavens.For(p).Single();
        Require(NecromancerEffects.ManaBalance256(p)==mana-1536&&b.Prepared.StatProfile==DruidRavenBody.SnapshotStats,"Raven paid snapshot birth");return b;
    }
    private static GraveyardZombie Target(PlayerMobile p)
    {
        var t=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};t.SetHits(20000);t.MoveToWorld(p.Location,p.Map);t.AIObject?.AITimer.Stop();
        t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Hold(DruidRavenBody b)=>b.RestorePrepared(b.Prepared with{NextFrameUtc=TimedEffects.Now.AddDays(3)});
    private static void NextAttack(DruidRavenBody b)
    {
        int attempts=b.Prepared.Attempts;
        for(int i=0;i<400;i++)
        {
            b.Advance(b.Prepared.NextFrameUtc,true);
            Require(!b.Deleted&&b.Prepared.Fault==null,"Raven attack entry deleted/faulted");
            if(b.Prepared.Attempts>attempts&&b.Prepared.Mode==4){return;}
        }
        throw new InvalidOperationException("Raven did not enter next attack: "+b.SavedText);
    }
    // Construct an older JSON input with the added property absent. This is
    // explicit compatibility coverage, not a separate old-binary writer run.
    internal static void Legacy(DruidRavenBody b)
    {
        var node=JsonNode.Parse(b.SavedText).AsObject();node.Remove("StatProfile");node["Remaining"]=-1;
        var state=JsonSerializer.Deserialize<RavenBodyState>(node.ToJsonString());Require(state.StatProfile==DruidRavenBody.LegacyStats,"Missing historical policy default");b.RestorePrepared(state);
    }
    private static void Historical()
    {
        var p=Owner(1);var b=Cast(p);var t=Target(p);Legacy(b);Hold(b);
        try
        {
            TrainAll(p);int budget=DruidRavenStats.InitialBudget(SkillMechanics.Values(p,CombatAbility.Raven));NextAttack(b);
            Require(b.Prepared.StatProfile==DruidRavenBody.LegacyStats&&b.Prepared.Birth.Rank==2&&b.Prepared.Packet.Rank==21&&b.Prepared.Remaining==budget-1,"Historical first-think/mode owner interpretation changed");
        }
        finally{t.Delete();PersistentMinions.Dismiss(p);p.Delete();}
    }
    private static void Check()
    {
        Historical();var rows=new List<Actor>();
        foreach(int hard in new[]{1,7,20})
        {
            var p=Owner(hard);if(hard==20){TrainAll(p);}var b=Cast(p);Hold(b);var t=Target(p);
            try
            {
                var birth=b.Prepared;int budget=birth.Remaining;Require(birth.Packet.Rank==hard+1&&budget==DruidRavenStats.InitialBudget(SkillMechanics.Values(p,CombatAbility.Raven)),"Birth effective packet/budget");
                TrainAll(p);p.Backpack.DropItem(p.FindItemOnLayer(Layer.Helm));ClassGear.Refresh(p);NextAttack(b);
                Require(b.Prepared.Birth==birth.Birth&&b.Prepared.Packet==birth.Packet&&b.Prepared.Remaining==budget-b.Prepared.Attempts,"Owner changes before first think rewrote born packet/budget");
                int ownerHard=20;
                if(hard==20)
                {
                    ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);ClassSkillChecks.Train(p,"druid.raven",1);ownerHard=1;
                }
                NextAttack(b);Require(b.Prepared.Packet==birth.Packet&&b.Prepared.Birth==birth.Birth&&b.Prepared.Remaining==budget-b.Prepared.Attempts,"Next mode or respec recaptured packet/finite budget");
                var before=b.Prepared;string text=b.SavedText;
                foreach(var invalid in new[]{before with{StatProfile="unknown"},before with{Remaining=-1},before with{Packet=before.Packet with{Rank=before.Packet.Rank+1}}})
                {
                    bool refused=false;try{b.RestorePrepared(invalid);}catch(InvalidDataException){refused=true;}
                    Require(refused&&b.SavedText==text,"Invalid captured stat profile restore was not atomic");
                }
                Hold(b);PersistentMinions.Suspend(p);p.Internalize();
                rows.Add(new(p.Serial.Value,b.Serial.Value,hard,ownerHard,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),BattleCommand.PetOrder(b),b.SavedText));
            }
            finally{t.Delete();}
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(rows.ToArray())));
        Log.Information("Raven snapshots:3 public births;born rank/synergy packet/budget across pre-think gear/training and respec,atomic profiles and separate historical JSON pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2&&saved.Actors.Length==3,"Wrong Raven saved cohort/generation");
        foreach(var row in saved.Actors)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var b=(DruidRavenBody)World.FindMobile((Serial)row.Body);
            Require(b.ControlMaster==p&&b.SavedText==row.State&&b.Prepared.StatProfile==DruidRavenBody.SnapshotStats&&SkillHealth.Balance256(b)==row.Life
                &&NecromancerEffects.ManaBalance256(p)==row.Mana&&BattleCommand.PetOrder(b)==row.Order&&ClassCreation.Profiles.GetRank(p.Serial,"druid.raven")==row.OwnerHard,"Original Raven stats/partial budget/RNG/order/resources changed on reload");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(1);var account=FixtureNetwork.Authenticate(network,p,"raven-snapshot-");var t=Target(p);DruidRavenBody b=null;
        void End(){network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();PersistentMinions.Dismiss(p);p.Delete();}
        try
        {
            b=Cast(p);var packet=b.Prepared.Packet;int budget=b.Prepared.Remaining,life=SkillHealth.Balance256(t);Require(packet.Rank==2&&budget==13,"Natural snapshot birth context");TrainAll(p);p.Backpack.DropItem(p.FindItemOnLayer(Layer.Helm));ClassGear.Refresh(p);
            int mana=NecromancerEffects.ManaBalance256(p);long deadline=Core.TickCount+29000;
            void Poll()
            {
                try
                {
                    Require(network.Running&&Core.TickCount<deadline&&b.Prepared.Fault==null,"Natural Raven snapshot transport/clock/fault: "+b.SavedText);
                    Require(b.Prepared.Packet==packet&&b.Prepared.Remaining+b.Prepared.Attempts==budget&&NecromancerEffects.ManaBalance256(p)==mana,"Natural next attack recaptured stats/budget or charged owner");
                    if(!b.Deleted){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Poll);return;}
                    Require(b.Prepared.Attempts==budget&&b.Prepared.Remaining==0&&b.Prepared.Hits>0&&SkillHealth.Balance256(t)<life,"Natural born-budget exhaustion/damage failed");End();Log.Information("raven-snapshot-live-result: passed");
                }
                catch(Exception e){End();Log.Error(e,"raven-snapshot-live-result: failed");}
            }
            Log.Information("raven-snapshot-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(30),Poll);
        }
        catch{End();throw;}
    }
}
