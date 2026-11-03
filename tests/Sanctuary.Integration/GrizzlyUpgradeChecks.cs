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

public static class GrizzlyUpgradeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlyUpgradeChecks));
    private sealed record Saved(uint Owner,uint Bear,uint Target,GrizzlySnapshot Snapshot,int Life,int TargetLife,ulong Seed,long Order,DateTime StunUntil);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grizzly-upgrades-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("grizzly-upgrades-check",Check);Register("grizzly-upgrades-reload-check",Reload);
        Register("grizzly-upgrades-live-check",()=>FixtureClock.NextLoop("grizzly-upgrades-live-result",()=>FixtureNetwork.Connected("grizzly-upgrades-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved ordinary-hit Grizzly stun and shared Crushing Blow",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,int rank)=>ClassSkillChecks.Train(p,"druid.summongrizzly",rank);
    private static PlayerMobile Owner(int rank)
    {var p=SpiritWolfWorldChecks.Fresh(column:6880);Train(p,rank);PoisonDaggerChecks.Named(p,"Tarnhelm");return p;}
    private static GraveyardZombie Target(PlayerMobile p)
    {
        var t=new GraveyardZombie{Name="GrizzlyUpgradeTarget",CantWalk=true,NoKillAwards=true};t.SetHits(20000);t.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Through(DruidGrizzlyBody b,int frame)
    {var c=b.Prepared.Controller;DruidGrizzlyWorld.Advance(b,c.NextUtc.AddMilliseconds((frame-c.Frame-1)*40L),true);Require(b.Prepared.Controller.Fault==null,"Grizzly upgrade world fault");}
    private static GrizzlyWorldState Attack(DruidGrizzlyBody b,PlayerMobile p,bool smite)=>b.Prepared.Controller with
    {AttackPacket=DruidBeastAttackStats.Capture(SkillMechanics.Values(p,CombatAbility.SummonGrizzly),b.SummonDifficulty,smite?14:4)};
    private static BearSmitePrepared Packet(DruidGrizzlyBody b,PlayerMobile p,BaseCreature t,bool smite,out GrizzlyWorldState state)
    {
        state=Attack(b,p,smite);
        for(ulong n=1;n<10000;n++)
        {ulong next=n;var packet=DruidGrizzlyHit.Prepare(b,p,state,t,smite,1,ref next,out _,out _);if((packet.ResultFlags&1)!=0){return packet;}}
        throw new InvalidOperationException("No deterministic Grizzly hit packet");
    }
    private static ulong Seed(int first,int? second=null)
    {
        for(ulong n=1;n<1000000;n++)
        {ulong next=n;if(SourceRandom.Limited(ref next,100)==first&&(second==null||SourceRandom.Limited(ref next,100)==second)){return n;}}
        throw new InvalidOperationException("No deterministic Grizzly event stream");
    }
    private static void Stuns()
    {
        var p=Owner(9);var b=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddHours(1),11);var t=Target(p);
        try
        {
            int draws=0;Require(SkillMechanics.Rank(p,CombatAbility.SummonGrizzly)==10&&!GrizzlyUpgrades.Stun(b,t,false,_=>{draws++;return 0;})&&draws==0,"Soft rank10 unlocked stun");Train(p,10);
            foreach(bool boss in new[]{false,true})
            {
                BaseCreature recipient=t;
                if(boss)
                {var e=new EncounterCreature();e.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Unique,ChampionKind.Champion,[],EliteAura.None,null,new Random(71));e.MoveToWorld(t.Location,t.Map);recipient=e;}
                try
                {
                    // The existing unique encounter profile has its real Boss flag.
                    Require(CombatStuns.Rules(recipient,out bool actualBoss,out _)&&actualBoss==boss,"Stun boss classification fixture");
                    foreach(int roll in new[]{0,9,10,49,50,99})
                    {
                        CombatStuns.Clear(recipient);draws=0;DateTime start=TimedEffects.Now;bool result=GrizzlyUpgrades.Stun(b,recipient,false,_=>{draws++;return roll;});
                        bool success=roll<(boss?10:50);Require(result==success&&draws==1,"Approved stun chance boundary");
                        if(success)
                        {var effect=CombatStuns.Snapshot().Single(x=>x.Target==recipient.Serial.Value);Require(effect.UntilUtc>=start.AddSeconds(boss?1:3)&&effect.UntilUtc<=TimedEffects.Now.AddSeconds(boss?1:3),"Approved stun duration");}
                    }
                    CombatStuns.Clear(recipient);draws=0;Require(!GrizzlyUpgrades.Stun(b,recipient,true,_=>{draws++;return 0;})&&draws==0,"Knockback swat applied stun or rolled");
                }
                finally{CombatStuns.Clear(recipient);if(boss){recipient.Delete();}}
            }
        }
        finally{CombatStuns.Clear(t);t.Delete();b.Delete();p.Delete();}
    }
    private static void Contacts()
    {
        foreach(bool smite in new[]{false,true})
        {
            var p=Owner(19);var b=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddHours(1),17);var t=Target(p);
            try
            {
                var packet=Packet(b,p,t,smite,out var state);var birth=b.Prepared.Snapshot;int life=SkillHealth.Maximum256(b)-257;SkillHealth.SetBalance256(b,life);long order=BattleCommand.PetOrder(b);
                Require(SkillMechanics.Rank(p,CombatAbility.SummonGrizzly)==20&&NpcCrushing.Chance(b)==0,"Soft rank20 unlocked Crushing Blow");Train(p,20);
                foreach(int cbRoll in new[]{9,10})
                {
                    t.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);SourceKnockbacks.Clear(t);CombatStuns.Clear(t);int hp=20000*256;SkillHealth.SetBalance256(t,hp);ulong seed=Seed(cbRoll,0),expected=seed;
                    SourceRandom.Limited(ref expected,100);if(!smite){SourceRandom.Limited(ref expected,100);}
                    var hit=DruidGrizzlyHit.Release(b,p,state,t,packet,95,false,smite,ref seed);
                    int cb=cbRoll<10?hp/4:0;
                    Require(hit.Outcome==GrizzlyHitOutcome.Hit&&SkillHealth.Balance256(t)==hp-cb-packet.Total256&&seed==expected,"Actual Grizzly CB/chance/RNG/pre-health damage");
                    Require((CombatStuns.Remaining(t)>0)==!smite,"Ordinary/swat stun selection");
                }
                foreach(int flags in new[]{0,16})
                {CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);int hp=SkillHealth.Balance256(t);ulong seed=Seed(0,0),before=seed;var hit=DruidGrizzlyHit.Release(b,p,state,t,packet with{ResultFlags=flags},95,false,smite,ref seed);Require(hit.Outcome==(flags==0?GrizzlyHitOutcome.Miss:GrizzlyHitOutcome.Blocked)&&SkillHealth.Balance256(t)==hp&&seed==before&&CombatStuns.Remaining(t)==0,"Miss/block applied upgrade or consumed RNG");}
                SourceKnockbacks.Clear(t);t.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);ulong invalid=Seed(0,0),same=invalid;int prior=SkillHealth.Balance256(t);var rejected=DruidGrizzlyHit.Release(b,p,state,t,packet,95,false,smite,ref invalid);
                Require(rejected.Outcome==GrizzlyHitOutcome.Invalid&&invalid==same&&SkillHealth.Balance256(t)==prior,"Out-of-range contact consumed upgrade");
                Require(b.Prepared.Snapshot==birth&&SkillHealth.Balance256(b)==life&&BattleCommand.PetOrder(b)==order,"Live upgrade recaptured/healed/reordered Grizzly");
                ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);int calls=0;
                Require(NpcCrushing.Chance(b)==0&&!GrizzlyUpgrades.Stun(b,t,false,_=>{calls++;return 0;})&&calls==0,"Respec retained unlocks");
            }
            finally{CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Delete();b.Delete();p.Delete();}
        }
    }
    private static void Stage()
    {
        var p=Owner(20);var b=DruidGrizzlyWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2),29);var t=Target(p);
        ulong seed=Seed(9,0);b.RestorePrepared(b.Prepared with{Controller=b.Prepared.Controller with{Seed=seed}});SkillHealth.SetBalance256(b,b.MaximumLife256-257);SkillHealth.SetBalance256(t,1025);
        // Held inspection deadline permits both process reloads; normal three-
        // second duration and natural expiry are checked separately above.
        DateTime until=TimedEffects.Now.AddHours(1);CombatStuns.Restore(CombatStuns.Snapshot().Append(new CombatStunState(t.Serial.Value,until)).ToArray());PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,b.Prepared.Snapshot,SkillHealth.Balance256(b),SkillHealth.Balance256(t),b.Prepared.Controller.Seed,BattleCommand.PetOrder(b),until)));
    }
    private static void Check(){Stuns();Contacts();Stage();}
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;var b=World.FindMobile((Serial)s.Bear) as DruidGrizzlyBody;var t=World.FindMobile((Serial)s.Target) as BaseCreature;
        Require(p!=null&&b!=null&&t!=null&&b.ControlMaster==p&&b.Prepared.Snapshot==s.Snapshot&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife
            &&b.Prepared.Controller.Seed==s.Seed&&BattleCommand.PetOrder(b)==s.Order&&NpcCrushing.Chance(b)==10&&GrizzlyUpgrades.HardRank(b)==20
            &&CombatStuns.Snapshot().Single(x=>x.Target==s.Target).UntilUtc==s.StunUntil,"Original Grizzly/stun identity/life/birth/stream/unlocks changed");
    }
    private static void Live(NetState network)
    {
        var p=Owner(19);var account=FixtureNetwork.Authenticate(network,p,"grizzly-upgrades-");var t=Target(p);DruidGrizzlyBody b=null;
        try
        {
            NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,null,new(p.X+1,p.Y,p.Z),Core.TickCount),"Learned Grizzly cast");ClassCombat.ReleaseCast(p);ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
            b=DruidGrizzlies.For(p).Single();Require(b.Prepared.LearnedCast&&NecromancerEffects.ManaBalance256(p)==mana-40*256,"Learned Grizzly body/payment");
        }
        catch{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();b?.Delete();p.Delete();throw;}
        bool trained=false,ordinary=false,hitChecked=false;int life=SkillHealth.Balance256(t),afterHit=0;long deadline=Core.TickCount+20000;ulong expected=0;DateTime stunUntil=default;
        void End(){network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Delete();b.Delete();p.Delete();}
        void Poll()
        {
            try
            {
                if(!trained&&b.Prepared.Controller is{Mode:4} c&&c.ActionAt>c.Frame)
                {
                    Require(NpcCrushing.Chance(b)==0,"Live soft rank20 unlocked CB");Train(p,20);ordinary=true;life=SkillHealth.Balance256(t);bool found=false;
                    for(ulong n=1;n<10000;n++)
                    {
                        ulong stream=n;var packet=DruidGrizzlyHit.Prepare(b,p,c,t,false,1,ref stream,out _,out _);
                        if((packet.ResultFlags&1)!=0&&SourceRandom.Limited(ref stream,100)<10&&SourceRandom.Limited(ref stream,100)<50)
                        {expected=stream;b.RestorePrepared(b.Prepared with{Controller=c with{Seed=n}});found=true;break;}
                    }
                    Require(found,"Live ordinary hit/CB/stun fixture stream");trained=true;
                }
                if(trained&&!hitChecked&&SkillHealth.Balance256(t)<life)
                {Require(b.Prepared.Controller.LastHit is{Outcome:GrizzlyHitOutcome.Hit} hit&&life-SkillHealth.Balance256(t)==life/4+hit.Total256&&b.Prepared.Controller.Seed==expected
                    &&ordinary&&CombatStuns.Remaining(t)>0,"Automatic learned CB/stun/stream: "+JsonSerializer.Serialize(new{life,CurrentLife=SkillHealth.Balance256(t),expected,ordinary,State=b.Prepared.Controller,Stun=CombatStuns.Remaining(t)}));
                    stunUntil=CombatStuns.Snapshot().Single(x=>x.Target==t.Serial.Value).UntilUtc;afterHit=SkillHealth.Balance256(t);hitChecked=true;b.Delete();}
                if(hitChecked)
                {Require(SkillHealth.Balance256(t)==afterHit,"Removed Grizzly stun-expiry recipient changed HP: "+afterHit+" -> "+SkillHealth.Balance256(t));if(TimedEffects.Now>=stunUntil){Require(CombatStuns.Remaining(t)==0,"Natural three-second stun did not expire");End();Log.Information("grizzly-upgrades-live-result: passed");return;}}
                Require(Core.TickCount<deadline,"Live Grizzly upgrade hit timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
            }
            catch(Exception e){End();Log.Error(e,"grizzly-upgrades-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
    }
}
