// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GoatmanNpcBodyActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNpcBodyActionChecks));
    private sealed record Row(uint Owner,uint Body,string State,int Life,int Maximum,int Mana,DateTime Until,int Bonus);
    private sealed record Saved(int Generation,Row[] Rows);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-npc-body-action-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-npc-body-action-reload-check":"goatman-npc-body-action-check";
            ConsoleInputHandler.RegisterCommand(name,"Own Goatman action binding and original durable speed state",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Start();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static Row Scene(bool female)
    {
        var p=new PlayerMobile{Name="Goatman body action fixture",Player=true,Female=female,Body=female?0x191:0x190};
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",5);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);ClassSkillChecks.Train(p,"warlock.bloodoath",5);
        FixtureSpace.Corridor(p,6750,24,female?3660:3620);PoisonDaggerChecks.Named(p,"Tarnhelm");
        NecromancerEffects.SetMana256(p,p.ManaMax*256);WarlockGoatmanBody b=null;EncounterCreature enemy=null;
        try
        {
            DateTime at=TimedEffects.Now;b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);
            var original=b.Prepared.Snapshot;string old=b.SavedText;
            using var legacy=JsonDocument.Parse(old);
            Require(b.Prepared.Frenzy==null&&!legacy.RootElement.TryGetProperty("Frenzy",out _)&&JsonSerializer.Serialize(JsonSerializer.Deserialize<GoatmanBodyState>(old))==old,"Absent optional state changed legacy JSON");
            enemy=new EncounterCreature{Name="NPC body contact fixture",CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;
            enemy.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);enemy.AIObject?.AITimer.Stop();
            var world=new GoatmanNpcActionChecks.World();
            var first=GoatmanNpcBodyActions.OpenPrepared(b,enemy,417,100,at);int rank=original.Birth.Suppliers.FrenzyRank;
            Require(first.Rank==rank&&first.Speed.State==null,"Action inherited live owner rank or another demon's charges");
            GoatmanNpcActionProposal.Advance(first,first.ReleaseFrame,world);var one=b.Prepared.Frenzy;
            int duration=200+25*(rank-1);
            Require(one is{Charges:1}&&one.Rank==rank&&one.UntilUtc==at.AddMilliseconds((first.ReleaseFrame-100+duration)*40),"Contact did not save exact rank/charge/deadline");
            var helm=p.FindItemOnLayer(Layer.Helm);p.Backpack.DropItem(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.summongoatman",20);
            DateTime nextAt=at.AddMilliseconds((first.EndFrame-100)*40);
            var next=GoatmanNpcBodyActions.OpenPrepared(b,enemy,417,first.EndFrame,nextAt);
            Require(next.Rank==rank&&next.Speed.State.Charges==1&&b.Prepared.Snapshot==original,"New action recaptured gear/skills or reset charges");
            GoatmanNpcActionProposal.Advance(next,next.ReleaseFrame,world);var two=b.Prepared.Frenzy;
            Require(two.Charges==2&&two.UntilUtc==at.AddMilliseconds((next.ReleaseFrame-100+duration)*40),"Second action lost its original source-frame epoch");
            int bonus=checked((int)GoatmanNpcSkills.Values(417,2).Eval("aurastatcalc2"));
            Require(GoatmanNpcBodyActions.Bonus(b,nextAt)==bonus&&GoatmanNpcBodyActions.AttackRate(b,nextAt)==SourceAttackRate.WolfRate(b)+original.Birth.Suppliers.AttackSpeed+bonus,"Captured birth/common attack rates/Frenzy did not aggregate once");
            var cleave=GoatmanNpcBodyActions.OpenPrepared(b,enemy,419,300,nextAt);
            var expected=GoatmanNpcActionProposal.Schedule(419,original.Birth.Suppliers.CleaveRank,GoatmanNpcBodyActions.AttackRate(b,nextAt),300);
            Require(cleave.ReleaseFrame==expected.Events.Single(e=>e.Type==SourceAnimationEvents.ModeChange).Frame,"Live body Cleave clock omitted or doubled Frenzy");
            var movement=GoatmanNpcBodyActions.Movement(b,nextAt);int percent=100+original.Birth.Suppliers.VelocityPercent+bonus+PaladinVigor.VelocityPercent(b)-GearEffects.MovementSlow(b);
            Require(movement==SourceActorMotion.Speed(8,256,percent,0)&&GoatmanNpcBodyActions.OtherAnimation(b,160,false,nextAt)==SourceAttackRate.Speed(160,0,100+bonus)
                &&GoatmanNpcBodyActions.OtherAnimation(b,160,true,nextAt)==160,"Body movement/other-animation used owner IAS or double-counted movement rate");
            var motion=DruidRavenMotion.Center(b.X*2,b.Y*2) with{Points=[new(b.X*2+10,b.Y*2)],FinalTarget=new(b.X*2+10,b.Y*2),Flags=0x20,Distance=5,Velocity=movement.Velocity,MaximumVelocity=movement.Velocity};
            motion=SourceActorMotion.DirectionToNext(motion);var advanced=SourceActorMotion.Advance(motion,(_,_)=>0);
            Require(advanced.Moving&&advanced.State.X>motion.X,"Live body movement rate did not drive the existing source motion kernel");
            var fractional=two with{UntilUtc=nextAt.AddSeconds(3).AddTicks(1)};b.RestorePrepared(b.Prepared with{Frenzy=fractional});
            var miss=GoatmanNpcBodyActions.OpenPrepared(b,enemy,417,400,nextAt);world.Hit=false;
            GoatmanNpcActionProposal.Advance(miss,miss.ReleaseFrame,world);
            Require(b.Prepared.Frenzy==fractional&&GoatmanNpcBodyActions.Bonus(b,fractional.UntilUtc.AddTicks(-1))==bonus&&GoatmanNpcBodyActions.Bonus(b,fractional.UntilUtc)==0,"Read/miss/source projection extended or truncated subframe expiry");
            world.Hit=true;var fresh=GoatmanNpcBodyActions.OpenPrepared(b,enemy,417,500,fractional.UntilUtc);
            GoatmanNpcActionProposal.Advance(fresh,fresh.ReleaseFrame,world);Require(b.Prepared.Frenzy.Charges==1,"Expired prior charges carried into a fresh attack");
            b.RestorePrepared(b.Prepared with{Frenzy=two});
            var before=b.Prepared;string text=b.SavedText;bool refused=false;
            try{b.RestorePrepared(before with{Frenzy=two with{Charges=rank+1}});}catch(InvalidDataException){refused=true;}
            Require(refused&&b.SavedText==text,"Invalid charge state partially replaced the body");
            int life=b.MaximumLife256-257;SkillHealth.SetBalance256(b,life);int mana=NecromancerEffects.ManaBalance256(p);
            PersistentMinions.Suspend(p);Require(b.Map==Map.Internal&&SkillHealth.Balance256(b)==life&&b.Prepared.Frenzy==two,"Parking healed/reset/refreshed Frenzy");
            b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
            var saved=new Row(p.Serial.Value,b.Serial.Value,b.SavedText,life,b.MaximumLife256,mana,two.UntilUtc,bonus);b=null;p=null;return saved;
        }
        finally{enemy?.Delete();b?.Delete();p?.Delete();}
    }
    private static void Start()
    {
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,[Scene(false),Scene(true)])));
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"State binding enabled unfinished summon host");
        Log.Information("Goatman body actions:actual Warlock/Witch snapshots,contacts,successive charges,source attack/motion rates,exact subframe expiry,miss,parking and atomic invalid-state refusal");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var row in saved.Rows)
        {
            var b=World.FindMobile((Serial)row.Body) as WarlockGoatmanBody;var p=World.FindMobile((Serial)row.Owner) as PlayerMobile;
            Require(b!=null&&p!=null&&b.ControlMaster==p&&b.Map==Map.Internal&&b.SavedText==row.State&&SkillHealth.Balance256(b)==row.Life
                &&b.MaximumLife256==row.Maximum&&NecromancerEffects.ManaBalance256(p)==row.Mana&&b.Prepared.Frenzy.UntilUtc==row.Until,"Original body/speed deadline/wounds/resources changed on reload");
            Require(GoatmanNpcBodyActions.Bonus(b,row.Until.AddTicks(-1))==row.Bonus&&GoatmanNpcBodyActions.Bonus(b,row.Until)==0,"Restored absolute deadline was extended or discarded");
            b.AdvanceRecovery(TimedEffects.Now,false);Require(SkillHealth.Balance256(b)==row.Life&&b.Prepared.Frenzy.UntilUtc==row.Until,"Offline recovery reset the parked state");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
