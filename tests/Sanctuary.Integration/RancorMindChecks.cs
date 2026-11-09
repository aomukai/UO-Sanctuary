// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class RancorMindChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RancorMindChecks));
    private sealed record Saved(int Generation,uint Owner,uint Target,uint Other,int X,int Y,NecromancerState State,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"rancor-mind-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Rancor shared Confuse selector and independent leases",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("rancor-mind-check",false);Register("rancor-mind-reload-check",true);
    }
    private static ulong Seed(bool monsters)
    {for(int n=0;n<100;n++){ulong s=SourceRandom.InitLow(n),copy=s;if(((SourceRandom.Roll(ref copy)&1)!=0)==monsters){return s;}}throw new InvalidOperationException("No selection seed");}
    private static CleaveActionTarget Enemy(PlayerMobile p,int dx)
    {
        var t=new CleaveActionTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);return t;
    }
    private static void Primary(PlayerMobile p,CleaveActionTarget t)
    {
        var v=new SkillValues(SkillMechanics.For(CombatAbility.SigilRancor),_=>1,_=>0,99);
        Require(SigilRancorPrimary.ApplyPrepared(p,t,v,TimedEffects.Now.Add(FixtureSpace.Retention),true),"Primary setup");
    }
    private static void Select(CleaveActionTarget t)
    {t.Frozen=t.CantWalk=false;t.NextReacquireTime=Core.TickCount;SourceMind.Think(t);t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();}
    private static void Exercise(PlayerMobile p)
    {
        var t=Enemy(p,1);var other=Enemy(p,2);CleaveActionTarget playerTarget=null;
        try
        {
            Primary(p,t);var until=TimedEffects.Now.AddSeconds(30);var curse=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,until,p.X,p.Y);
            NecromancerEffects.Profiles.Store(t,NecromancerEffects.Profiles.Find(t)with{Curse=curse});
            Require(!SigilRancorMind.ApplyPrepared(p,t,until,Seed(true),false),"Unqualified Rancor AI installed");
            Require(SigilRancorMind.ApplyPrepared(p,t,until,Seed(true),true),"Prepared Rancor AI lease");
            ulong expected=Seed(true);SourceRandom.Roll(ref expected);Select(t);var first=NecromancerEffects.Profiles.Find(t);
            Require(t.Combatant==other&&first.RancorMind is{Mode:3,Draws:1}&&first.RancorMind.Seed==expected&&first.RancorMind.Focus==other.Serial.Value
                &&first.Mind==null&&first.Curse==curse&&SourceMind.Neutral(t)&&t.GetMaster()==null&&!t.Controlled&&!t.Summoned
                &&t.IsEnemy(other)&&other.IsEnemy(t)&&!t.IsFriend(p),"Rancor failed shared monster selection/RNG/faction without overwriting Weaken");
            Require(!PaladinConversion.MindBlastEligible(p,t),"Mind Blast could convert a neutral Rancor recipient");
            Require(t.AIObject is LeashedMeleeAI gate&&!gate.CanScanAt(Core.TickCount),"Fixture did not reach the native 200ms scan gate");
            Select(t);Require(NecromancerEffects.Profiles.Find(t)==first,"Closed native scan gate consumed AI randomness or erased focus");
            bool refused=false;try{SigilRancorMind.ApplyPrepared(p,t,until,Seed(false),true);}catch(NotSupportedException){refused=true;}
            Require(refused&&NecromancerEffects.Profiles.Find(t)==first,"Unverified Rancor refresh rewrote stream");

            var attract=curse with{Ability=CombatAbility.Attract};var ordinary=SourceMind.Attract(t,first,other,750,TimedEffects.Now);
            NecromancerEffects.Profiles.Store(t,first with{Curse=attract,Mind=ordinary});Select(t);
            Require(t.Combatant==other&&SourceMind.Current(t)==ordinary&&NecromancerEffects.Profiles.Find(t).RancorMind==first.RancorMind,"Rancor replaced protected Attract or spent its stream under ordinary priority");
            SigilRancorMind.Clear(t);Require(NecromancerEffects.Curse(t)==attract&&NecromancerEffects.Profiles.Find(t).Mind==ordinary,"Rancor clear erased ordinary Attract");

            // Remove the earlier neutral Attract recipient: it legitimately
            // competes with players in the shared selector's faction scan.
            NecromancerEffects.Profiles.Clear(t);t.Delete();playerTarget=Enemy(p,1);
            Primary(p,playerTarget);NecromancerEffects.Profiles.Store(playerTarget,NecromancerEffects.Profiles.Find(playerTarget)with{Curse=curse});
            Require(SigilRancorMind.ApplyPrepared(p,playerTarget,until,Seed(false),true),"Player-faction lease");Select(playerTarget);var player=NecromancerEffects.Profiles.Find(playerTarget);
            Require(playerTarget.Combatant==p&&player.RancorMind.Draws==1&&player.Mind==null&&player.Curse==curse,$"Shared player-faction selection missing:state={JsonSerializer.Serialize(player)},combatant={playerTarget.Combatant?.Serial},owner={p.Serial}");
            var old=player;refused=false;
            try{NecromancerEffects.Profiles.Store(playerTarget,old with{RancorMind=old.RancorMind with{Mode=2}});}catch(InvalidDataException){refused=true;}
            Require(refused&&NecromancerEffects.Profiles.Find(playerTarget)==old,"Malformed secondary Attract mode changed state");
            NecromancerEffects.Profiles.Store(playerTarget,old with{RancorMind=old.RancorMind with{Mode=0,Focus=0}});
            Require(SourceMind.Neutral(playerTarget)&&SourceMind.Current(playerTarget)is{Mode:0},"Empty scan cancelled the still-live confusion lease");
            NecromancerEffects.Profiles.Store(playerTarget,old with{RancorMind=old.RancorMind with{UntilUtc=TimedEffects.Now.AddTicks(-1)}});
            Require(!SourceMind.Neutral(playerTarget)&&SourceMind.Current(playerTarget)==null&&NecromancerEffects.Curse(playerTarget)==curse,"Expired Rancor AI retained alignment or removed Weaken");
            SigilRancorMind.Clear(playerTarget);Require(SigilRancorMind.ApplyPrepared(p,playerTarget,until,Seed(true),true),"Clear setup");SigilRancorPrimary.Clear(playerTarget);
            Require(!SourceMind.Neutral(playerTarget)&&NecromancerEffects.Profiles.Find(playerTarget).RancorMind is{Mode:0,Focus:0},"Cleared primary kept active secondary AI");
            Primary(p,playerTarget);Require(NecromancerEffects.Profiles.Find(playerTarget).RancorMind==null&&!SourceMind.Neutral(playerTarget),"Fresh primary resurrected an old AI lease");
            Require(!JsonSerializer.Serialize(new NecromancerState()).Contains("RancorMind"),"Legacy null JSON changed");
            Require(!WarlockCombat.Implemented(CombatAbility.SigilRancor),"Prepared selector enabled learned Rancor");
        }
        finally{NecromancerEffects.Profiles.Clear(t);if(playerTarget!=null){NecromancerEffects.Profiles.Clear(playerTarget);playerTarget.Delete();}t.Delete();other.Delete();}
        Log.Information("Rancor mind:shared Confuse monster/player targeting and RNG,neutral/MindBlast exclusion,independent Weaken/Attract,refresh refusal/expiry/clear/legacy JSON pass;automatic Rancor remains gated.");
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;CleaveActionTarget target,other;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=World.FindMobile((Serial)s.Owner)as PlayerMobile;target=World.FindMobile((Serial)s.Target)as CleaveActionTarget;other=World.FindMobile((Serial)s.Other)as CleaveActionTarget;
            Require(p!=null&&target!=null&&other!=null&&NecromancerEffects.Profiles.Find(target)==s.State&&SkillHealth.Balance256(target)==s.Life256,"Original saved secondary stream/focus/curse/wounds changed");
            FixtureSpace.Place(p,new(s.X,s.Y,0),Map.Trammel);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);other.MoveToWorld(new(p.X+7,p.Y,p.Z),p.Map);
            target.AIObject.AITimer.Stop();other.AIObject.AITimer.Stop();
        }
        else
        {
            p=CleaveActionChecks.Fresh();target=Enemy(p,6);other=Enemy(p,7);Primary(p,target);
            Require(SigilRancorMind.ApplyPrepared(p,target,TimedEffects.Now.Add(FixtureSpace.Retention),Seed(true),true),"Saved AI lease");
            var s=NecromancerEffects.Profiles.Find(target);var curse=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,TimedEffects.Now.Add(FixtureSpace.Retention),p.X,p.Y);
            NecromancerEffects.Profiles.Store(target,s with{Curse=curse});SkillHealth.SetBalance256(target,target.HitsMax*256-129);
        }
        Exercise(p);var before=NecromancerEffects.Profiles.Find(target);ulong expected=before.RancorMind.Seed;SourceRandom.Roll(ref expected);Select(target);var after=NecromancerEffects.Profiles.Find(target);
        Require(after.RancorMind.Seed==expected&&after.RancorMind.Draws==before.RancorMind.Draws+1&&after.Mind==null&&after.Curse==before.Curse,"Original AI did not continue the next saved draw independently");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,p.Serial.Value,target.Serial.Value,other.Serial.Value,p.X,p.Y,after,SkillHealth.Balance256(target))));
    }
}
