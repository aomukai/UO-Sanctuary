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

public static class DireWolfUpgradeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DireWolfUpgradeChecks));
    private sealed record Saved(uint Owner,uint Wolf,DireWolfSnapshot Snapshot,int Life,ulong Seed,long Order);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dire-wolf-upgrades-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("dire-wolf-upgrades-check",Check);Register("dire-wolf-upgrades-reload-check",Reload);
        Register("dire-wolf-upgrades-live-check",()=>FixtureClock.NextLoop("dire-wolf-upgrades-live-result",()=>FixtureNetwork.Connected("dire-wolf-upgrades-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Dire Wolf direct Extra Fast/Strong",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,int rank)=>ClassSkillChecks.Train(p,"druid.summondirewolf",rank);
    private static PlayerMobile Owner(int rank)
    {var p=SpiritWolfWorldChecks.Fresh(column:6720);Train(p,rank);PoisonDaggerChecks.Named(p,"Tarnhelm");return p;}
    private static GraveyardZombie Target(PlayerMobile p,int dx=2)
    {
        var t=new GraveyardZombie{Name="DireWolfUpgradeTarget",CantWalk=true,Frozen=true,NoKillAwards=true};t.SetHits(20000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){t.SetResistance(r,0);}
        return t;
    }
    private static void Through(DruidDireWolfBody b,int frame)
    {var c=b.Prepared.Controller;DruidDireWolfWorld.Advance(b,c.NextUtc.AddMilliseconds((frame-c.Frame-1)*40L),true);Require(b.Prepared.Controller.Fault==null,"Dire upgrade world fault");}
    private static void Wild()
    {
        foreach(var d in Enum.GetValues<MonsterDifficulty>())
        {
            int i=(int)d;Require(EncounterProfiles.ExtraStrong(d,false)==(new[]{135,112,99}[i],new[]{90,75,66}[i]),"Direct affix constants");
            var basis=new EncounterCreature();var strong=new EncounterCreature();
            try
            {
                var profile=EncounterProfiles.Profile("crypt-sentinel-60");basis.Assign(profile,60,d,EncounterGrade.Ordinary,ChampionKind.Champion,[],EliteAura.None,null,new Random(836));
                strong.Assign(profile,60,d,EncounterGrade.Ordinary,ChampionKind.Champion,[EliteModifier.ExtraStrong],EliteAura.None,null,new Random(836));
                var bonus=EncounterProfiles.ExtraStrong(d,false);
                Require(strong.CombatProfile.MinDamage==basis.CombatProfile.MinDamage*(100+bonus.DamagePercent)/100
                    &&strong.CombatProfile.AttackRating==basis.CombatProfile.AttackRating*(100+bonus.AttackPercent)/100
                    &&strong.CombatProfile.Life==basis.CombatProfile.Life,"Shared wild Extra Strong behavior");
            }
            finally{basis.Delete();strong.Delete();}
        }
        Require(EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast)==100,"Shared movement-only Extra Fast");
    }
    private static void Strength()
    {
        var p=Owner(19);var b=DruidDireWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddHours(1),17);var t=Target(p);var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Through(b,25);var c=b.Prepared.Controller;Require(c is{Mode:4,Feeding:false,AttackPacket:not null},"Upgrade bite context");
            var snapshot=b.Prepared.Snapshot;int original=DruidDireWolfHit.DamagePercent(b,c),rating=ClassStats.AttackRating(b);
            Require(SkillMechanics.Rank(p,CombatAbility.SummonDireWolf)==20&&DireWolfUpgrades.DamagePercent(b)==0&&DireWolfUpgrades.AttackPercent(b)==0,"Soft rank20 unlocked Extra Strong");
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);Train(p,20);
            var added=EncounterProfiles.ExtraStrong(b.SummonDifficulty,false);int basis=CreatureProfiles.AttackRating(b);
            Require(DruidDireWolfHit.DamagePercent(b,c)==original+added.DamagePercent&&ClassStats.AttackRating(b)==rating+(int)((long)basis*added.AttackPercent/100),"Live direct damage/AR pools");
            var birth=snapshot.Birth;var attack=c.AttackPacket.Value;
            Require(DruidDireWolfHit.Chance(b,c,t)==AssassinTrapAttack.Chance(birth.Level,checked(birth.BaseAttack+birth.AttackBonus+attack.Attack),0,added.AttackPercent,ClassStats.Level(t),ClassStats.Defense(t)),"Actual bite omitted or duplicated Extra Strong AR");
            var rage=DruidDireWolfWorld.Rage(c);Require(DruidDireWolfHit.DamagePercent(b,rage)==original+added.DamagePercent+100,"Rage and Extra Strong multiplied instead of adding");
            NecromancerEffects.Profiles.Store(b,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),b.X,b.Y)));
            Require(DruidDireWolfHit.DamagePercent(b,rage)==original+added.DamagePercent+50,"Curse did not join damage pool once");NecromancerEffects.Profiles.Clear(b);
            ulong seed=1;for(;seed<10000;seed++){ulong next=seed;if(AssassinTrapAttack.Hit(DruidDireWolfHit.Chance(b,c,t),SourceRandom.Roll(ref next))){break;}}
            ulong expected=seed;SourceRandom.Roll(ref expected);var packet=DruidDireWolfHit.Packet(attack.Minimum,attack.Maximum,original+added.DamagePercent,0,0,0,0,0,0,0,5,
                n=>(int)SourceRandom.Limited(ref expected,n),()=>SourceRandom.Roll(ref expected));
            int hp=SkillHealth.Balance256(t);var hit=DruidDireWolfHit.Resolve(b,p,c,t,ref seed);
            Require(hit.Outcome==DireWolfHitOutcome.Hit&&hit.Physical256==packet.Physical256&&hp-SkillHealth.Balance256(t)==packet.Physical256&&seed==expected,"Actual extra-strong packet/RNG or health loss");
            Require(b.Prepared.Snapshot==snapshot&&b.Prepared.Controller==c&&SkillHealth.Balance256(b)==life&&BattleCommand.PetOrder(b)==order,"Live affix recaptured/healed/reordered actor");
            b.RestorePrepared(b.Prepared with{Controller=b.Prepared.Controller with{Seed=seed}});
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
            Require(DireWolfUpgrades.MovementPercent(b)==0&&DireWolfUpgrades.DamagePercent(b)==0&&ClassStats.AttackRating(b)==rating,"Respec retained affixes");Train(p,20);
            var facets=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
            try
            {
                SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
                foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
                {
                    int old=SkillHealth.Balance256(b),max=SkillHealth.Maximum256(b);p.MoveToWorld(new(120,120,0),map);SummonDifficulties.Sync(p);var bonus=EncounterProfiles.ExtraStrong(b.SummonDifficulty,false);
                    Require(DireWolfUpgrades.DamagePercent(b)==bonus.DamagePercent&&DireWolfUpgrades.AttackPercent(b)==bonus.AttackPercent&&DireWolfUpgrades.MovementPercent(b)==100
                        &&SkillHealth.Balance256(b)==(long)old*SkillHealth.Maximum256(b)/max,"Facet direct affix branch or proportional life");
                }
            }
            finally{SummonDifficulties.SetFacets(facets);}
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,b.Prepared.Snapshot,SkillHealth.Balance256(b),b.Prepared.Controller.Seed,BattleCommand.PetOrder(b))));
            b=null;p=null;
        }
        finally{SourceBlocking.BlockRoll=blocks;t.Delete();b?.Delete();p?.Delete();}
    }
    private static void Movement()
    {
        var p=Owner(9);var at=TimedEffects.Now.AddHours(1);var b=DruidDireWolfWorld.Create(p,new(p.X+16,p.Y,p.Z),at,23);
        try
        {
            PetOwnerRoutes.Moved(p,at.AddMilliseconds(26));for(int frame=1;frame<=150&&b.Prepared.Controller.Mode is not(2 or 15);frame++){Through(b,frame);}
            var before=b.Prepared.Controller;Require(before.Mode is 2 or 15&&DireWolfUpgrades.MovementPercent(b)==0,"Soft rank10or missing movement context");
            int attackRate=SourceAttackRate.WolfRate(b),oldSpeed=before.Motion.Velocity;var birth=b.Prepared.Snapshot;Train(p,10);
            int percent=checked(200+before.MovingBonus);Require(PaladinVigor.VelocityPercent(b)==0&&GearEffects.MovementSlow(b)==0,"Unexpected movement fixture modifiers");var expected=SourceActorMotion.Speed(NecromancerSummons.Source("fenris","Velocity"),NecromancerSummons.Source("spiritwolf-animation",before.Mode==15?"RNSpeed":"WLSpeed"),percent,0);
            Through(b,before.Frame+1);var moving=b.Prepared.Controller;
            Require(DireWolfUpgrades.MovementPercent(b)==100&&moving.Motion.Velocity==expected.Velocity&&moving.Motion.Velocity>oldSpeed&&SourceAttackRate.WolfRate(b)==attackRate
                &&b.Prepared.Snapshot==birth,"Live Extra Fast omitted movement, sped attacks or recaptured stats");
            NecromancerEffects.Profiles.Store(b,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),b.X,b.Y)));
            Require(DruidDireWolfWorld.MovementPercent(b,before.MovingBonus)==150+before.MovingBonus,"Extra Fast and Decrepify must add before velocity calculation");NecromancerEffects.Profiles.Clear(b);
        }
        finally{b.Delete();p.Delete();}
    }
    private static void Check(){Wild();Movement();Strength();}
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;var b=World.FindMobile((Serial)s.Wolf) as DruidDireWolfBody;
        Require(p!=null&&b!=null&&b.ControlMaster==p&&b.Prepared.Snapshot==s.Snapshot&&SkillHealth.Balance256(b)==s.Life&&b.Prepared.Controller.Seed==s.Seed
            &&BattleCommand.PetOrder(b)==s.Order&&DireWolfUpgrades.MovementPercent(b)==100&&DireWolfUpgrades.DamagePercent(b)==135&&DireWolfUpgrades.AttackPercent(b)==90,"Original Dire Wolf identity/life/birth/stream/unlocks changed");
    }
    private static void Live(NetState network)
    {
        var p=Owner(19);var account=FixtureNetwork.Authenticate(network,p,"dire-wolf-upgrades-");var t=Target(p);DruidDireWolfBody b=null;
        try
        {
            NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.SummonDireWolf,null,new(p.X+1,p.Y,p.Z),Core.TickCount),"Live learned Dire cast");ClassCombat.ReleaseCast(p);ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
            b=DruidDireWolves.For(p).Single();Require(b.Prepared.LearnedCast&&NecromancerEffects.ManaBalance256(p)==mana-20*256,"Live Dire body/payment");
        }
        catch{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();b?.Delete();p.Delete();throw;}
        bool trained=false;int life=SkillHealth.Balance256(t);long deadline=Core.TickCount+8000;
        void End(){network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();b.Delete();p.Delete();}
        void Poll()
        {
            try
            {
                if(!trained&&b.Prepared.Controller is{Mode:4,Feeding:false}){Require(DireWolfUpgrades.DamagePercent(b)==0,"Live soft rank20 unlocked Strong");Train(p,20);trained=true;}
                if(trained&&SkillHealth.Balance256(t)<life)
                {Require(b.Prepared.Controller.LastHit is{Outcome:DireWolfHitOutcome.Hit,Physical256:>0}&&DireWolfUpgrades.DamagePercent(b)==135,"Automatic learned strong bite");End();Log.Information("dire-wolf-upgrades-live-result: passed");return;}
                Require(Core.TickCount<deadline,"Live Dire upgrade bite timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
            }
            catch(Exception e){End();Log.Error(e,"dire-wolf-upgrades-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
    }
}
