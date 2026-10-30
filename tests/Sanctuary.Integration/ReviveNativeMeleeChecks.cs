// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveNativeMeleeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveNativeMeleeChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("revive-native-melee-live-check","Native proactive class guard and source weapon base",_=>Core.LoopContext.Post(()=>
    {
        try{FixtureClock.NextLoop("revive-native-melee-live-result",()=>FixtureNetwork.Connected("revive-native-melee-live-result",Live));Log.Information("revive-native-melee-live-check: passed");}
        catch(Exception e){Log.Error(e,"revive-native-melee-live-check: failed");}
    }));
    private static Zombie Enemy(PlayerMobile owner,int x,int y)
    {
        var enemy=new Zombie{NoKillAwards=true,Frozen=true,CantWalk=true};MonsterVariants.Assign(enemy,MonsterDifficulty.Hell);
        enemy.MoveToWorld(new(x,y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();enemy.SetHits(60000);return enemy;
    }
    private static void Live(NetState network)
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6550,30,2500);
        var account=new Account("revive-melee-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        while(ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.skeletonmastery")<20)
        {Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.skeletonmastery",out var reason),reason);}
        var corpse=ReviveNativeActivationChecks.Body(owner,false,out var original,MonsterDifficulty.Hell);
        var setup=ReviveLife.Setup(owner,corpse,4,10);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,new(false,false,false,false,false,true),setup,out var actor,out var issue),issue);
        actor.AIObject.AITimer.Stop();var ai=actor.AI;int minimum=actor.DamageMin,maximum=actor.DamageMax;
        var otherOwner=new PlayerMobile{Player=true,Body=0x190};otherOwner.MoveToWorld(new(owner.X+2,owner.Y+2,owner.Z),owner.Map);
        var ordinary=new Skeleton{NoKillAwards=true};ordinary.MoveToWorld(new(owner.X+2,owner.Y+1,owner.Z),owner.Map);ordinary.AIObject.AITimer.Stop();
        Require(ordinary.SetControlMaster(otherOwner),"Ordinary guard ownership failed");ordinary.IssueOrder(OrderType.Guard,null);
        var probe=Enemy(owner,owner.X+4,owner.Y+2);
        int oldStrength=actor.RawStr;double tactics=actor.Skills.Tactics.Base,anatomy=actor.Skills.Anatomy.Base;
        actor.SetDamage(20,20);actor.RawStr=800;actor.Skills.Tactics.Base=actor.Skills.Anatomy.Base=120;
        ordinary.SetDamage(20,20);ordinary.RawStr=800;ordinary.Skills.Tactics.Base=ordinary.Skills.Anatomy.Base=120;
        var weapon=(BaseWeapon)actor.Weapon;var ordinaryWeapon=(BaseWeapon)ordinary.Weapon;
        Require(ClassCombatHooks.SourceWeaponDamage(actor)&&!ClassCombatHooks.SourceWeaponDamage(ordinary),"Raw source base leaked to ordinary NPC");
        for(int roll=0;roll<32;roll++)
        {Require(weapon.ComputeDamageAOS(actor,probe)==20&&ordinaryWeapon.ComputeDamageAOS(ordinary,probe)>20,"Native source base retained UO skill scaling or ordinary NPC lost it");}
        int expected=NamedElements.Resolve256(actor,probe,60*256,"physical",enhance:false).Damage256,before=SkillHealth.Balance256(probe);
        weapon.OnHit(actor,probe);
        int actual=before-SkillHealth.Balance256(probe);
        Require(actual==expected,"Full native OnHit lost unscaled base/physical Mastery/mitigation:"+actual+"/"+expected);
        actor.SetDamage(minimum,maximum);actor.RawStr=oldStrength;actor.Skills.Tactics.Base=tactics;actor.Skills.Anatomy.Base=anatomy;actor.Combatant=null;actor.FocusMob=null;probe.Delete();

        var enemy=Enemy(owner,owner.X+6,owner.Y);int initialLife=SkillHealth.Balance256(enemy),friendlyLife=ordinary.Hits;
        Require(enemy.Combatant==null&&enemy.Aggressors.Count==0&&enemy.Aggressed.Count==0
            &&ClassCombatHooks.GuardTarget(actor,enemy)==true&&ClassCombatHooks.GuardTarget(ordinary,enemy)==null
            &&ClassCombatHooks.GuardTarget(actor,owner)==false&&ClassCombatHooks.GuardTarget(actor,otherOwner)==false
            &&ClassCombatHooks.GuardTarget(actor,ordinary)==false,"Proactive permission ignored idle hostility,ordinary negative control or friend exclusions");
        var origin=actor.Location;long deadline=Core.TickCount+15000;
        Log.Information("revive-native-melee-live: ready");actor.AIObject.AITimer.Start();ordinary.AIObject.AITimer.Start();
        void Cleanup()
        {actor.Delete();ordinary.Delete();enemy.Delete();corpse.Delete();otherOwner.Delete();network.Mobile=null;owner.NetState=null;owner.Delete();}
        void Inspect()
        {
            try
            {
                Require(network.Running&&actor is{Alive:true,Deleted:false}&&actor==original&&actor.AI==ai&&Core.TickCount<deadline,
                    "Natural guard did not acquire/pursue/attack idle hostile: "+actor.Location+" action="+actor.AIObject.Action+" combatant="+actor.Combatant);
                Require(ordinary.Combatant==null&&ordinary.Hits==friendlyLife,"Ordinary pet acquired idle hostile or friendly pet was hurt");
                if(SkillHealth.Balance256(enemy)<initialLife)
                {
                    Require(actor.Combatant==enemy&&actor.Location!=origin&&actor.InRange(enemy,1),"Damage did not result from natural pursuit/contact");
                    int loss=initialLife-SkillHealth.Balance256(enemy);
                    PersistentMinions.Suspend(owner);
                    Require(ClassCombatHooks.GuardTarget(actor,enemy)==false,"Parked class original retained proactive permission");
                    File.WriteAllText(Path.Join(Core.BaseDirectory,"revive-native-melee-result.json"),JsonSerializer.Serialize(new{Actor=actor.Serial.Value,Original=original.Serial.Value,Ai=(int)ai,Minimum=minimum,Maximum=maximum,FixedBase=20,FixedExpected256=expected,FixedActual256=actual,NaturalDamage256=loss,OrdinaryScaled=ordinaryWeapon.ComputeDamageAOS(ordinary,enemy),OrdinaryUntargeted=ordinary.Combatant==null}));
                    Log.Information("Revive native melee:32source/ordinary fixed-base comparisons,full native OnHit physical Mastery delivery,friendly/ordinary exclusions,natural idle-hostile acquisition,pursuit/contact and parked refusal pass;original AI/serial preserved");
                    Log.Information("revive-native-melee-live-result: passed");Cleanup();return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(100),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){Log.Error(e,"revive-native-melee-live-result: failed");Cleanup();}
        }
        Inspect();
    }
}
