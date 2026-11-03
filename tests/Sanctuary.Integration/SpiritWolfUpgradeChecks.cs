// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SpiritWolfUpgradeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfUpgradeChecks));
    private sealed record Saved(uint Owner,uint Wolf,uint Target,int Life,SpiritWolfSnapshot Snapshot,ulong Seed,int Rate,uint CreditOwner);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"spirit-wolf-upgrades-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("spirit-wolf-upgrades-check",Check);Register("spirit-wolf-upgrades-reload-check",Reload);
        Register("spirit-wolf-upgrades-live-check",()=>FixtureClock.NextLoop("spirit-wolf-upgrades-live-result",()=>FixtureNetwork.Connected("spirit-wolf-upgrades-live-result",Live)));
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Approved Spirit Wolf wounds and breakable cold immunity",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,int rank)=>ClassSkillChecks.Train(p,"druid.summonspiritwolf",rank);
    private static (PlayerMobile Owner,DruidSpiritWolfBody Wolf,GraveyardSkeleton Target) Scene(int rank=9)
    {
        var p=SpiritWolfWorldChecks.Fresh(column:6600);Train(p,rank);PoisonDaggerChecks.Named(p,"Tarnhelm");
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddHours(1),7);
        var target=new GraveyardSkeleton{Name="SpiritWolfUpgradeTarget",CantWalk=true,Frozen=true,NoKillAwards=true};
        target.SetHits(10000);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();target.SetResistance(ResistanceType.Cold,0);
        Shield(target,0);DruidSpiritWolfWorld.Advance(wolf,wolf.Prepared.Controller.NextUtc.AddMilliseconds(24*40),true);
        Require(wolf.Prepared.Controller is{Mode:4,AttackPacket:not null},"Wolf did not capture A1");
        return(p,wolf,target);
    }
    private static void Shield(BaseCreature target,int value)=>SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value)
        .Append(new SourceShield(target.Serial.Value,CreatureProfiles.Definition(target).Reference,value)).ToArray());
    private static ulong Seed(DruidSpiritWolfBody wolf,BaseCreature target,bool hit)
    {
        int chance=DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target);
        for(ulong n=1;n<10000;n++){ulong next=n;if(AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref next))==hit){return n;}}
        throw new InvalidOperationException("No controlled wolf hit seed");
    }
    private static TimedActor Wound(Mobile target)
    {Require(TimedEffects.TryActor(target,out var state)&&state.OpenWounds!=null&&state.BleedMs>0,"Missing approved wolf wound");return state;}
    private static void End(PlayerMobile p,DruidSpiritWolfBody wolf,BaseCreature target)
    {target.Corpse?.Delete();target.Delete();wolf.Delete();p.Delete();}
    private static void Events()
    {
        var(p,wolf,target)=Scene();var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Require(wolf.Prepared.Snapshot.Birth.Rank==10&&SpiritWolfUpgrades.HardRank(wolf)==9&&NpcOpenWounds.Inherent(wolf)==0,"Soft rank unlocked wolf wounds");
            ulong initial=Seed(wolf,target,true),seed=initial;var old=DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed);
            Require(old.Outcome==SpiritWolfHitOutcome.Hit&&old.Cold256>0&&wolf.SkillWeaponDamage==(0,0)&&(!TimedEffects.TryActor(target,out var before)||before.OpenWounds==null),"Baseline cold-only wolf or soft-point refusal");
            ulong baseline=seed,expected=baseline;SourceRandom.Roll(ref expected);Train(p,10);seed=initial;
            int mana=NecromancerEffects.ManaBalance256(p);DateTime at=TimedEffects.Now;
            var hit=DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed,at);var state=Wound(target);
            Require(hit.Outcome==SpiritWolfHitOutcome.Hit&&hit.Cold256==old.Cold256&&seed==expected&&NpcOpenWounds.Inherent(wolf)==100
                &&state.BleedSource==wolf.Serial&&state.OpenWounds.CreditOwner==p.Serial.Value&&state.BleedMs==8000
                &&NpcHitSkills.BloodChance(wolf)==0&&NecromancerEffects.ManaBalance256(p)==mana,"Live hard10 event, one shared-stream draw, cold packet, owner or Blood-only proc boundary");
            int hp=SkillHealth.Balance256(target),rate=state.OpenWounds.Rate256;TimedEffects.Settle(target,at.AddMilliseconds(39));
            Require(SkillHealth.Balance256(target)==hp,"Wolf bleed ticked before its source frame");TimedEffects.Settle(target,at.AddMilliseconds(40));
            Require(hp-SkillHealth.Balance256(target)==rate,"Wolf did not reuse exact NPC wound frames");
            TimedEffects.Clear(target);seed=Seed(wolf,target,false);var miss=DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed);
            Require(miss.Outcome==SpiritWolfHitOutcome.Miss&&(!TimedEffects.TryActor(target,out var nohit)||nohit.OpenWounds==null),"Miss applied wolf wounds");
            Shield(target,1);SourceBlocking.BlockRoll=_=>0;seed=initial;
            var blocked=DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed);
            Require(blocked.Outcome==SpiritWolfHitOutcome.Blocked&&(!TimedEffects.TryActor(target,out var denied)||denied.OpenWounds==null),"Blocked contact applied wolf wounds");
            Shield(target,0);SourceBlocking.BlockRoll=_=>99;target.SetResistance(ResistanceType.Cold,100);seed=initial;hp=SkillHealth.Balance256(target);
            var immune=DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed);
            Require(immune.Outcome==SpiritWolfHitOutcome.Hit&&SkillHealth.Balance256(target)==hp&&Wound(target).BleedSource==wolf.Serial,"Successful cold-immune hit lost the ordinary wound event or gained physical damage");
            TimedEffects.Clear(target);at=TimedEffects.Now.AddMinutes(1);TimedEffects.Bleed(p,target,8000,at);SkillHealth.SetBalance256(target,256);seed=initial;ulong untouched=seed;
            var settled=DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed,at.AddMilliseconds(100));
            Require(settled.Outcome==SpiritWolfHitOutcome.Invalid&&!target.Alive&&seed==untouched,"Overdue lethal drain entered the new contact or consumed attack RNG");
        }
        finally{SourceBlocking.BlockRoll=block;End(p,wolf,target);}
    }
    private static void Immunity()
    {
        var(p,wolf,target)=Scene(19);var caster=ClassSkillChecks.Player(DiabloClass.Paladin);var auras=PaladinConviction.Snapshot();
        try
        {
            int original=wolf.ColdResistance;var snapshot=wolf.Prepared.Snapshot;ulong seed=wolf.Prepared.Controller.Seed;
            SkillHealth.SetBalance256(wolf,wolf.MaximumLife256-257);int life=SkillHealth.Balance256(wolf);
            Require(original<100&&SkillMechanics.Rank(p,CombatAbility.SummonSpiritWolf)==20&&!GearEffects.ApplyChill(wolf,Core.TickCount,1600,target)
                &&NamedElements.Damage(target,wolf,100,"cold",enhance:false)>0,"Soft rank20 unlocked immunity or lost baseline unchillability");
            Train(p,20);Require(wolf.ColdResistance==100&&NamedElements.Damage(target,wolf,100,"cold",enhance:false)==0,"Hard20cold immunity missing");
            Require(PaladinConviction.Apply(caster,wolf,SkillMechanics.Values(caster,CombatAbility.Conviction,12),TimedEffects.Now.AddMinutes(1))
                &&NamedElements.Resistance(target,wolf,"cold",wolf.ColdResistance)==83&&NamedElements.Damage(target,wolf,100,"cold",enhance:false)==17,"Wolf immunity bypassed shared one-fifth Conviction");
            PaladinConviction.Restore(auras);wolf.SetResistance(ResistanceType.Cold,160);Require(wolf.ColdResistance==160,"Immunity floor lowered a stronger resistance");wolf.SetResistance(ResistanceType.Cold,original);
            Require(wolf.Prepared.Snapshot==snapshot&&wolf.Prepared.Controller.Seed==seed&&SkillHealth.Balance256(wolf)==life,"Live unlock recaptured birth, healed or rolled");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
            Require(wolf.ColdResistance==original&&NpcOpenWounds.Inherent(wolf)==0,"Respec retained approved wolf unlocks");Train(p,20);
            Require(wolf.ColdResistance>=100&&NpcOpenWounds.Inherent(wolf)==100&&wolf.Prepared.Snapshot==snapshot,"Relearn lost live unlocks or replaced snapshot");
            var facets=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
            try
            {
                SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
                foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
                {
                    int hp=SkillHealth.Balance256(wolf),max=SkillHealth.Maximum256(wolf);p.MoveToWorld(new(120,120,0),map);SummonDifficulties.Sync(p);
                    Require(wolf.ColdResistance>=100&&NpcOpenWounds.Inherent(wolf)==100&&SkillHealth.Balance256(wolf)==(long)hp*SkillHealth.Maximum256(wolf)/max,
                        "Owner facet rescale lost immunity/wounds or proportional injury");
                }
            }
            finally{SummonDifficulties.SetFacets(facets);}
        }
        finally{PaladinConviction.Restore(auras);caster.Delete();End(p,wolf,target);}
    }
    private static void Save()
    {
        var(p,wolf,target)=Scene(20);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            ulong seed=Seed(wolf,target,true);DateTime inspection=TimedEffects.Now.AddHours(1);
            Require(DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed,inspection).Outcome==SpiritWolfHitOutcome.Hit,"Saved original wolf hit");
            wolf.RestorePrepared(wolf.Prepared with{Controller=wolf.Prepared.Controller with{Seed=seed}});
            wolf.SuspendActions();SkillHealth.SetBalance256(wolf,wolf.MaximumLife256-257);var wound=Wound(target);
            // Held inspection clock only: the real eight-second cadence is
            // checked above; no uninterrupted downtime lifetime is claimed.
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,wolf.Serial.Value,target.Serial.Value,
                SkillHealth.Balance256(wolf),wolf.Prepared.Snapshot,wolf.Prepared.Controller.Seed,wound.OpenWounds.Rate256,wound.OpenWounds.CreditOwner)));
        }
        finally{SourceBlocking.BlockRoll=block;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var wolf=World.FindMobile((Serial)saved.Wolf) as DruidSpiritWolfBody;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(p!=null&&wolf!=null&&target!=null&&wolf.ControlMaster==p&&wolf.Prepared.Snapshot==saved.Snapshot
            &&wolf.Prepared.Controller.Seed==saved.Seed&&SkillHealth.Balance256(wolf)==saved.Life&&wolf.ColdResistance>=100
            &&NpcOpenWounds.Inherent(wolf)==100&&NpcHitSkills.BloodChance(wolf)==0,"Original saved wolf/rank/seed/life/unlocks changed");
        var wound=Wound(target);Require(wound.BleedSource==wolf.Serial&&wound.OpenWounds.Rate256==saved.Rate&&wound.OpenWounds.CreditOwner==saved.CreditOwner,"Saved wound lost original source/rate/credit");
    }
    private static void Check(){Events();Immunity();Save();}
    private static void Live(NetState network)
    {
        var p=SpiritWolfWorldChecks.Fresh(column:6480);Train(p,9);PoisonDaggerChecks.Named(p,"Tarnhelm");
        var account=FixtureNetwork.Authenticate(network,p,"spirit-wolf-upgrades-");
        var target=new GraveyardSkeleton{Name="SpiritWolfUpgradeLiveTarget",CantWalk=true,Frozen=true,NoKillAwards=true};
        target.SetHits(10000);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();target.SetResistance(ResistanceType.Cold,100);Shield(target,0);
        var sourceSeed=DruidSpiritWolfCasting.SeedLow;DruidSpiritWolfBody wolf=null;
        try
        {
            DruidSpiritWolfCasting.SeedLow=()=>173;NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,new(p.X+1,p.Y,p.Z),Core.TickCount),"Learned upgrade wolf start");
            ClassCombat.ReleaseCast(p);ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
            wolf=DruidSpiritWolves.For(p).Single();
            Require(wolf.Prepared.LearnedCast&&NecromancerEffects.ManaBalance256(p)==mana-15*256,"Learned upgrade wolf payment/body");
        }
        catch{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();target.Delete();wolf?.Delete();p.Delete();throw;}
        finally{DruidSpiritWolfCasting.SeedLow=sourceSeed;}
        int before=SkillHealth.Balance256(target);bool trained=false;long deadline=Core.TickCount+8000;
        void Finish(){network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();End(p,wolf,target);}
        void Poll()
        {
            try
            {
                if(!trained&&wolf.Prepared.Controller.Mode==4)
                {Require(NpcOpenWounds.Inherent(wolf)==0,"Live soft rank unlocked wounds");Train(p,10);trained=true;}
                if(TimedEffects.TryActor(target,out var state)&&state.OpenWounds!=null&&SkillHealth.Balance256(target)<before)
                {
                    Require(trained&&wolf.Prepared.Controller.Hits>0&&state.BleedSource==wolf.Serial&&state.OpenWounds.CreditOwner==p.Serial.Value,"Automatic learned cold-only wound source/credit");
                    Train(p,20);Require(wolf.ColdResistance>=100&&wolf.SkillWeaponDamage==(0,0),"Live immunity or cold-only attack");
                    Finish();Log.Information("spirit-wolf-upgrades-live-result: passed");return;
                }
                Require(Core.TickCount<deadline,"Learned wolf did not naturally wound a cold-immune target");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
            }
            catch(Exception e){Finish();Log.Error(e,"spirit-wolf-upgrades-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
    }
}
