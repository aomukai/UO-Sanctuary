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

public static class DeathSentryControlChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DeathSentryControlChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Corpse,uint Enemy,SentryBodyState Body,CorpseSkillRecord CorpseRow,int Health,int Mana,Point3D Origin,int Map);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-control-fixtures.json");
    public static void Configure(){Register("death-control-check",Check);Register("death-control-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Death Sentry AI104/mode9/mode14 actual dispatch and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string why){if(!condition){throw new InvalidOperationException(why);}}
    private static void Goldens()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"death-sentry-clock-source-probe.json")));int rows=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var clock=AssassinDeathSentryControl.Clock(V(0));
            Require(clock.Action==V(5) && clock.End==V(6) && AssassinWakeFire.NeutralThink(clock.End,V(1),V(2)!=0)==V(7),"Complete direct-clock source comparison");rows++;
        }
        Require(rows==96,"Incomplete direct S2 source inventory");
        var c=AssassinDeathSentryControl.Start(42);int bolt=0,blast=0;
        for(int frame=1;frame<=86;frame++)
        {
            c=AssassinDeathSentryControl.Advance(c,()=>true,true,true,1,()=>2,()=>new SentryTarget(7,5),
                ()=>new DeathCorpseTarget(1000,c.Frame<30?42:43,0),()=>true,(_,_)=>true,_=>blast++,_=>bolt++);
            AssassinDeathSentryControl.Validate(c);
            Require(JsonSerializer.Deserialize<DeathSentryControlState>(JsonSerializer.Serialize(c))==c,"Controller JSON round-trip");
            if(frame==25){Require(c.Mode==14 && c.Ai.RemainingShots==1 && c.Ai.LastCorpse==42,"Birth-frame remembered GUID did not exclude first corpse");}
            if(frame==28){Require(bolt==1 && blast==0,"Shared sequence action frame");}
            if(frame==56){Require(c.Mode==9 && c.Ai.RemainingShots==0 && c.Ai.LastCorpse==43,"Shared pre-cast shot debit/corpse priority");}
            if(frame==57){Require(blast==1,"Direct S2 action2 deadline");}
        }
        Require(c.Mode==0 && bolt==1 && blast==1,"Shared pool exhaustion/neutral death decision");
        c=AssassinDeathSentryControl.Start(0);int attempts=0;
        for(int i=0;i<27;i++)
        {c=AssassinDeathSentryControl.Advance(c,()=>true,true,true,1,()=>5,()=>null,()=>null,()=>throw new Exception("No enemy rolled chance"),(_,_)=>true,_=>{},_=>{});attempts=c.Thinks;}
        Require(attempts==3 && c.NextThink==28 && c.Ai.RemainingShots==5,"Zero-idle clamp changed shot pool or stalled thinking");
        c=AssassinDeathSentryControl.Start(0);
        for(int i=0;i<25;i++){c=AssassinDeathSentryControl.Advance(c,()=>true,true,true,1,()=>1,()=>new SentryTarget(7,1),()=>null,()=>true,(_,_)=>false,_=>{},_=>{});}
        Require(c.Ai.RemainingShots==0 && c.Mode==1 && c.NextThink==35 && c.Dispatches==0,"Failed sequence start refunded or dispatched a shot");
        var offline=AssassinDeathSentryControl.Inactive(c,100);Require(offline.Ai==c.Ai && offline.Mode==1 && offline.NextThink==126,"Offline time replay/refund");
        Log.Information("Death control:96source clocks,86mixed sequence/direct frames with JSON,zero-idle/failed-start/offline controls passed");
    }
    private static void Frames(AssassinSentryBody body,int until)
    {while(body.Prepared.DeathControl.Frame<until){body.AdvanceController(body.Prepared.NextFrameUtc,true);}}
    private static Saved Seed(PlayerMobile owner)
    {
        int low=0;for(;low<1000;low++){ulong sample=SourceRandom.InitLow(low);if(SourceRandom.Limited(ref sample,100)<50){break;}}
        Require(low<1000,"Deterministic first lightning-chance control");
        var actor=AssassinSentryBody.CreateEmerging(owner,new(owner.X+2,owner.Y,owner.Z),TimedEffects.Now.AddHours(2),SourceRandom.InitLow(low),CombatAbility.DeathSentry);
        var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+12,owner.Y,owner.Z));var enemy=DeathSentryExplosionChecks.Target(corpse,1);
        var chosen=AssassinSentryWorldTargets.Select(actor);Require(chosen is{Serial:>0} && chosen.Value.Serial==enemy.Serial.Value && chosen.Value.Distance>=16,"Corpse-priority outside-lightning-range scene");
        int hp=SkillHealth.Balance256(enemy),mana=NecromancerEffects.ManaBalance256(owner);Frames(actor,25);
        Require(actor.Prepared.DeathControl is{Mode:9,Ai.RemainingShots:4,Dispatches:0} && actor.Prepared.DeathControl.Corpse==corpse.Serial.Value
            && !corpse.SanctuaryConsumed && SkillHealth.Balance256(enemy)==hp,"Automatic corpse selection/pre-dispatch save phase");
        return new(owner.Serial.Value,actor.Serial.Value,corpse.Serial.Value,enemy.Serial.Value,actor.Prepared,CorpseSkillRules.Profiles.Find(corpse),hp,mana,owner.Location,owner.Map.MapID);
    }
    private static void Exercise(Saved saved)
    {
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);
        var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);var enemy=(BladeMissileTarget)World.FindMobile((Serial)saved.Enemy);
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);}
        Require(JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Body) && CorpseSkillRules.Profiles.Find(corpse)==saved.CorpseRow
            && SkillHealth.Balance256(enemy)==saved.Health && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved controller/corpse/health/mana changed");
        Frames(body,26);Require(corpse.SanctuaryConsumed && SkillHealth.Balance256(enemy)<saved.Health && body.Prepared.DeathControl.Dispatches==1,"Actual automatic mode9 corpse damage");
        enemy.MoveToWorld(new(body.X+6,body.Y,body.Z),body.Map);enemy.AIObject?.AITimer.Stop();int hp=SkillHealth.Balance256(enemy);Frames(body,55);
        Require(body.Prepared.DeathControl is{Mode:14,Ai.RemainingShots:3},"No-corpse lightning fallback/shared shot pool");Frames(body,58);
        var bolt=AssassinSentryLightning.Snapshot().Bolts.Single(x=>x.Actor==body.Serial.Value);
        Require(bolt.Ability==CombatAbility.DeathSentry && bolt.Minimum256==AssassinSentryCreation.Lightning(body.Prepared.Creation).Min && bolt.Frames==12,"Actual missile641/source-zero player276 damage mapping");
        for(int i=0;i<12 && SkillHealth.Balance256(enemy)==hp && AssassinSentryLightning.State(bolt.Id) is{} flight;i++){AssassinSentryLightning.TickFrame(flight.NextUtc,true);}
        Require(SkillHealth.Balance256(enemy)<hp,"Actual travelling Death Sentry lightning did no damage");
        while(body.Prepared.DeathControl.Mode!=0 && body.Prepared.DeathControl.Frame<300){Frames(body,body.Prepared.DeathControl.Frame+1);}
        Require(!body.Deleted && body.Prepared.DeathControl is{Mode:0,Ai.RemainingShots:0,Dispatches:5}
            && body.Prepared.ControllerFault==AssassinDeathSentryControl.RetirementFault,"Five shared shots/explicit registered retirement boundary");
        var released=AssassinSentryLightning.Snapshot().Bolts.Where(x=>x.Actor==body.Serial.Value).OrderBy(x=>x.Id).Last();hp=SkillHealth.Balance256(enemy);
        for(int i=0;i<12 && SkillHealth.Balance256(enemy)==hp && AssassinSentryLightning.State(released.Id) is{} flight;i++){AssassinSentryLightning.TickFrame(flight.NextUtc,true);}
        Require(SkillHealth.Balance256(enemy)<hp,"Retirement boundary froze released registered-owner lightning");
        Require(NecromancerEffects.ManaBalance256(owner)==saved.Mana,"NPC dispatch spent player mana");
        body.Delete();corpse.Delete();enemy.Delete();
    }
    private static void Check()
    {
        Goldens();var owner=new PlayerMobile{Body=0x190,Player=true,Name="DeathControlFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(owner,99);owner.RawStr=owner.RawDex=owner.RawInt=200;
        FixtureSpace.Corridor(owner,6460,40,2000);DeathSentryExplosionChecks.Train(owner,CombatAbility.DeathSentry,1);
        Exercise(Seed(owner));File.WriteAllText(Manifest,JsonSerializer.Serialize(Seed(owner)));
        Require(!AssassinCombat.Implemented(CombatAbility.DeathSentry),"Prepared automatic actor enabled an unfinished learned skill");
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Exercise(saved);File.WriteAllText(Manifest,JsonSerializer.Serialize(Seed(owner)));
    }
}
