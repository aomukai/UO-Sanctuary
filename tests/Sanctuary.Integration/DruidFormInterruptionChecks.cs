// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DruidFormInterruptionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormInterruptionChecks));
    private sealed record Saved(uint Owner,DruidPlayerForm Form,int Mana,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-form-interruption-fixtures.json");
    public static void Configure()
    {
        Rates();Register("druid-form-interruption-check",Check);Register("druid-form-interruption-reload-check",Reload);
        Register("druid-form-interruption-snapshot-check",Snapshot);
        Register("bear-interrupt-quick-check",()=>FixtureNetwork.Connected("bear-interrupt-quick-result",Input));
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Scoped modern Werebear interruption",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>p.Name?.StartsWith("DruidBearInterruptFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Require(bool ok,string message){if(!ok){throw new InvalidOperationException(message);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent);
        foreach(var prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Start(PlayerMobile p,CombatAbility ability)
    {Ready(p);Require(ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount),"Start "+ability);}
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="DruidBearInterruptFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var ability in new[]{CombatAbility.Werewolf,CombatAbility.Werebear,CombatAbility.Raven,CombatAbility.CycloneArmor}){Train(p,ability);}
        FixtureSpace.Corridor(p,6140,20,2100);Ready(p);return p;
    }
    private static void Drop(PlayerMobile p)
    {foreach(var bird in DruidRavens.For(p)){bird.Delete();}DruidPlayerForms.Clear(p);ClassCombat.Stop(p);p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        var p=Fresh();
        try
        {
            DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now);
            Require(!DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Idle bear immunity");
            foreach(var ability in new[]{CombatAbility.BasicAttack,CombatAbility.Raven,CombatAbility.CycloneArmor})
            {
                Start(p,ability);var s=ClassCombat.For(p);var pending=s.PendingAbility;long effect=s.EffectAt,end=s.NextAction;int mana=NecromancerEffects.ManaBalance256(p);
                Require(DruidFormInterruption.PreventRecovery(p,effect-1)&&!DruidFormInterruption.PreventRecovery(p,end),"Action interval boundary");
                Require(!HitRecovery.Apply(p,p.HitsMax,effect-1)&&s.PendingAbility==pending&&s.NextAction==end&&NecromancerEffects.ManaBalance256(p)==mana,"Bear windup disturbed or paid");
                Require(ClassCombat.ResolveAction(p,effect),"Protected action lost its effect");
                Require(s.PendingAbility==CombatAbility.None&&end>effect&&DruidFormInterruption.PreventRecovery(p,end-1)
                    &&!HitRecovery.Apply(p,p.HitsMax,end-1),"Post-impact animation tail interrupted");
                Require(HitRecovery.Apply(p,p.HitsMax,end),"Expired bear action retained immunity");
            }
            Start(p,CombatAbility.BasicAttack);var action=ClassCombat.For(p);long deadline=action.NextAction;ClassCombat.Stop(p);
            Require(action.NextAction==deadline&&!DruidFormInterruption.PreventRecovery(p,deadline-1)&&HitRecovery.Apply(p,p.HitsMax,deadline-1),"Stop kept immunity behind old deadline");
            Start(p,CombatAbility.BasicAttack);var origin=p.Location;
            p.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);p.MoveToWorld(origin,p.Map);
            Require(!DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Out-and-back retained protection");
            Start(p,CombatAbility.BasicAttack);DruidPlayerForms.Clear(p);
            Require(!DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Unshift retained protection");
            DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now);Start(p,CombatAbility.BasicAttack);
            DruidPlayerForms.Clear(p);DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now);
            Require(!DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Replacement form inherited prior action");
            DruidPlayerForms.Clear(p);DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now);Start(p,CombatAbility.BasicAttack);
            Require(!DruidFormInterruption.PreventRecovery(p,Core.TickCount)&&HitRecovery.Apply(p,p.HitsMax,ClassCombat.For(p).EffectAt-1),"Wolf incorrectly immune");
            DruidPlayerForms.Clear(p);Ready(p);
            Require(ClassCombat.Perform(p,CombatAbility.Werebear,null,p.Location,Core.TickCount),"Human bear cast");long at=ClassCombat.For(p).EffectAt;
            Require(!DruidFormInterruption.PreventRecovery(p,at-1),"Human entry windup incorrectly immune");
            Require(ClassCombat.ResolveAction(p,at)&&DruidFormInterruption.PreventRecovery(p,at),"New bear animation tail unprotected");
            DruidPlayerForms.Clear(p);Ready(p);p.Kill();Require(!p.Alive,"Bear protection leaked into human death");
            Log.Information("Modern bear:windup/effect/tail/expiry,Stop,first movement,form replacement,wolf/human entry/death controls pass");
        }
        finally{Drop(p);}
    }
    private static void Snapshot()
    {
        var p=File.Exists(Manifest)?(PlayerMobile)World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Owner):Fresh();
        if(DruidPlayerForms.State(p)==null)
        {
            while(ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(CombatAbility.Lycanthropy).TalentId)<20)
            {Require(ClassCreation.Profiles.TryTrain(p.Serial,SkillMechanics.For(CombatAbility.Lycanthropy).TalentId,out _),"Saved Lycanthropy");}
            Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"Saved bear entry");
        }
        Start(p,CombatAbility.BasicAttack);Require(DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Missing pre-save action context");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,DruidPlayerForms.State(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(DruidPlayerForms.State(p)==saved.Form&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life,"Reload changed original form/resources");
        Require(ClassCombat.For(p).FormAction==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None
            &&!DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Reload revived transient immunity/action");
        Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Reloaded idle bear retained immunity");Ready(p);
    }
    private static void Input(NetState network)
    {
        const string label="bear-interrupt-quick";var p=Fresh();Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"Input bear");Ready(p);
        var enemy=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;
        enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");
        field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Raven),"Input Raven binding");
        var deal=typeof(GraveyardUndead).GetMethod("Deal",BindingFlags.Instance|BindingFlags.NonPublic);Require(deal!=null,"Production monster damage method");
        int mana=NecromancerEffects.ManaBalance256(p),phase=0;long deadline=Core.TickCount+5000;
        void Damage(bool protectedAction)
        {
            SkillHealth.SetBalance256(p,p.HitsMax*256);int before=p.Hits;var action=ClassCombat.For(p);var pending=action.PendingAbility;long next=action.NextAction;
            deal.Invoke(enemy,new object[]{p,p.HitsMax/2,false});
            Require(p.Alive&&p.Hits<before&&HitRecovery.Chance(before-p.Hits,p.HitsMax)==8,"Production melee did not deliver guaranteed-recovery damage");
            Require(HitRecovery.Active(p,Core.TickCount)!=protectedAction&&action.NextAction==next
                &&(!protectedAction||action.PendingAbility==pending),"Incoming damage recovery/action boundary");
        }
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Bear interruption input timeout");var action=ClassCombat.For(p);
                if(phase==0&&action.PendingAbility==CombatAbility.Raven)
                {
                    Require(DruidFormInterruption.PreventRecovery(p,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana,"Input windup not protected/unpaid");
                    Damage(true);phase=1;Log.Information("{Label}: windup",label);
                }
                if(phase==1&&DruidRavens.For(p).Length==1)
                {
                    Require(action.PendingAbility==CombatAbility.None&&DruidFormInterruption.PreventRecovery(p,Core.TickCount)
                        &&NecromancerEffects.ManaBalance256(p)==mana-1536,"Effect/payment/tail");Damage(true);phase=2;
                }
                if(phase==2&&Core.TickCount>=action.NextAction)
                {
                    Require(!DruidFormInterruption.PreventRecovery(p,Core.TickCount),"Expired input protection");Damage(false);
                    Drop(p);enemy.Delete();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);
            }
            catch(Exception e){Drop(p);enemy.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.Raven,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Inspect();});
    }
}
