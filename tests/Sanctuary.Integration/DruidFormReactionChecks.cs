// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class DruidFormReactionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormReactionChecks));
    private sealed record Saved(uint Owner,DruidPlayerForm Form,uint Gloves,uint Armor,DruidFormReaction Hit,DruidFormReaction Block,long InspectionRecoveryUntil);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-form-reaction-fixtures.json");
    public static void Configure()
    {Register("druid-form-reaction-check",Check);Register("druid-form-reaction-reload-check",Reload);Register("druid-form-reaction-snapshot-check",Snapshot);Register("druid-form-reaction-live-check",Live);}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Form FHR/FBR source clocks and native reaction convention",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string reason){if(!ok){throw new InvalidOperationException(reason);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent);
        foreach(var prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="DruidFormReactionFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,CombatAbility.Werewolf);Train(p,CombatAbility.Werebear);Train(p,CombatAbility.Lycanthropy,20);
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        FixtureSpace.Corridor(p,6140,20,2100);Ready(p);return p;
    }
    private static Item Named(PlayerMobile p,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+key);ClassGear.Refresh(p);return item;}
    private static void Drop(PlayerMobile p){ClassCombat.Stop(p);DruidPlayerForms.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Matrix()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-recovery-clock-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int index=row[0].GetInt32();if(index is not(0 or 1 or 13 or 14)){continue;}
            var reaction=DruidFormReactionClock.Calculate(index<2?CombatAbility.Werewolf:CombatAbility.Werebear,index is 0 or 13,row[1].GetInt32(),row[2].GetInt32()!=0);
            Require(reaction.Advance256==row[3].GetInt32()&&reaction.SourceEndFrame==row[4].GetInt32()
                &&reaction.NativeFrames==Math.Max(1,row[4].GetInt32()-1),"Source GH/BL writer/end or explicit native projection mismatch");count++;
        }
        Require(count==2072,"Incomplete form GH/BL source matrix");
        Log.Information("Druid reaction:2072native complete-source GH/BL speed/end comparisons; sourceEND and existing native minus-one projection remain distinct");
    }
    private static void Reaction(PlayerMobile p,bool block)
    {
        Ready(p);long now=Core.TickCount;var actual=DruidFormReactionClock.Current(p,block);Require(actual!=null,"No form reaction");
        if(block){HitRecovery.Block(p,now);}else{Require(HitRecovery.Apply(p,p.HitsMax,now),"Idle form did not recover");}
        var s=ClassCombat.For(p);long end=now+actual.Value.NativeFrames*40;
        Require(s.RecoveryUntil==end&&s.Animation==20&&s.PendingAbility==CombatAbility.None,"Native form recovery/block clock");
        Require(HitRecovery.Active(p,end-1)&&!HitRecovery.Active(p,end)&&!p.Frozen&&!p.Paralyzed,"Reaction expiry boundary/native freeze leak");
    }
    private static void Check()
    {
        Matrix();var p=Fresh();Item gloves=null,armor=null;
        try
        {
            Require(DruidFormReactionClock.Current(p,false)==null&&HitRecovery.Frames(DiabloClass.Druid)==13&&HitRecovery.BlockFrames(DiabloClass.Druid,0)==11,"Human fallback changed");
            foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
            {
                Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Form entry");Ready(p);
                Require(DruidFormReactionClock.Current(p,false)?.NativeFrames==(form==CombatAbility.Werewolf?7:13)
                    &&DruidFormReactionClock.Current(p,true)?.NativeFrames==(form==CombatAbility.Werewolf?9:12),"Zero-gear form clock");
                Reaction(p,false);Reaction(p,true);gloves=Named(p,"Bloodfist");armor=Named(p,"Guardian Angel");
                Require(HitRecovery.Fhr(p)==30&&ClassGear.Bonus(p,"block2")+ClassGear.Bonus(p,"block3")==30,"Real FHR/FBR gear");
                Require(DruidFormReactionClock.Current(p,false)?.NativeFrames==(form==CombatAbility.Werewolf?5:9)
                    &&DruidFormReactionClock.Current(p,true)?.NativeFrames==(form==CombatAbility.Werewolf?6:8),"Live gear did not change form clocks");
                Reaction(p,false);Reaction(p,true);Ready(p);long now=Core.TickCount;
                Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,null,p.Location,now),"Protected block-control attack");
                var pending=ClassCombat.For(p).PendingAbility;long deadline=ClassCombat.For(p).NextAction;HitRecovery.Block(p,now);
                Require(ClassCombat.For(p).PendingAbility==pending&&ClassCombat.For(p).NextAction==deadline&&!HitRecovery.Active(p,now),"Block interrupted active form attack");
                if(form==CombatAbility.Werebear){Require(!HitRecovery.Apply(p,p.HitsMax,now),"Bear concentration lost");}
                p.AddToBackpack(gloves);p.AddToBackpack(armor);ClassGear.Refresh(p);gloves.Delete();armor.Delete();gloves=armor=null;
                DruidPlayerForms.Clear(p);Ready(p);Require(DruidFormReactionClock.Current(p,true)==null,"Unshift retained reaction clock");
            }
        }
        finally{Drop(p);gloves?.Delete();armor?.Delete();}
        var saved=Fresh();DruidPlayerForms.ApplyPrepared(saved,CombatAbility.Werewolf,TimedEffects.Now);var g=Named(saved,"Bloodfist");var a=Named(saved,"Guardian Angel");Ready(saved);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(saved.Serial.Value,DruidPlayerForms.State(saved),g.Serial.Value,a.Serial.Value,DruidFormReactionClock.Current(saved,false).Value,DruidFormReactionClock.Current(saved,true).Value,0)));
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Ready(p);
        long now=Core.TickCount;Require(HitRecovery.Apply(p,p.HitsMax,now),"Pre-save real reaction");var action=ClassCombat.For(p);
        Require(action.RecoveryUntil==now+saved.Hit.NativeFrames*40,"Pre-save natural reaction duration");
        // Hold only the already-verified recovery for restart inspection. The
        // separate natural timer control retains its actual200ms deadline.
        action.RecoveryUntil=now+FixtureSpace.RetentionMilliseconds;
        Require(HitRecovery.Active(p,now),"No active inspection recovery at save");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{InspectionRecoveryUntil=action.RecoveryUntil}));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(DruidPlayerForms.State(p)==saved.Form&&World.FindItem((Serial)saved.Gloves).Parent==p&&World.FindItem((Serial)saved.Armor).Parent==p,"Saved form/gear changed");
        Require(DruidFormReactionClock.Current(p,false)==saved.Hit&&DruidFormReactionClock.Current(p,true)==saved.Block,"Reloaded live gear/form reaction differs");
        Require(saved.InspectionRecoveryUntil>0&&!HitRecovery.Active(p,Core.TickCount),"Restart replayed held reaction");Reaction(p,false);Reaction(p,true);Ready(p);
    }
    private static void Live()
    {
        var p=Fresh();DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now);Named(p,"Bloodfist");Ready(p);
        FixtureClock.NextLoop("druid-form-reaction-live-result",()=>
        {
            long start=Core.TickCount;Require(HitRecovery.Apply(p,p.HitsMax,start),"Natural wolf recovery");long end=ClassCombat.For(p).RecoveryUntil;
            Require(end-start==200,"Natural FHR30wolf duration");p.Direction=Direction.East;var origin=p.Location;
            Require(!p.Move(Direction.East)&&p.Location==origin,"Movement accepted during form recovery");
            Timer.StartTimer(TimeSpan.FromMilliseconds(250),()=>
            {try{Require(!HitRecovery.Active(p,Core.TickCount)&&ClassCombatHooks.MovementAllowed(p)&&p.Move(Direction.East)&&p.Location!=origin,"Natural form recovery did not release movement");Drop(p);Log.Information("druid-form-reaction-live-result: passed");}
                catch(Exception e){Drop(p);Log.Error(e,"druid-form-reaction-live-result: failed");}});
        });
    }
}
