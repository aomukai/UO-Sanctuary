// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ConfuseResumeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ConfuseResumeChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Victim,int Map,Point3D Point,
        NecromancerCurse Curse,MindAiState Mind,int Mana,int ActorLife,int OwnerLife,int VictimLife,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"confuse-resume-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("confuse-resume-check",Check);
        Register("confuse-resume-reload-check",()=>FixtureNetwork.Connected("confuse-resume-reload-result",Reload));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Saved Confuse resumes real species acquisition and combat",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Saved Snapshot(PlayerMobile owner,BaseCreature actor,BaseCreature victim,int generation,Point3D point,int map)
    {
        var state=NecromancerEffects.Profiles.Find(actor);
        return new(owner.Serial.Value,actor.Serial.Value,victim.Serial.Value,map,point,state.Curse,state.Mind,
            NecromancerEffects.ManaBalance256(owner),SkillHealth.Balance256(actor),SkillHealth.Balance256(owner),SkillHealth.Balance256(victim),generation);
    }
    private static void Park(PlayerMobile owner,BaseCreature actor,BaseCreature victim)
    {
        actor.Frozen=actor.CantWalk=true;actor.AIObject.AITimer.Stop();
        foreach(var mobile in new Mobile[]{actor,victim,owner}){mobile.Internalize();}
    }
    private static void Check()
    {
        var p=ConfuseCastChecks.Owner(20);var actor=ConfuseCastChecks.Enemy(p,2);var victim=ConfuseCastChecks.Enemy(p,3);
        int mana=NecromancerEffects.ManaBalance256(p);
        Require(NecromancerCombat.Release(p,CombatAbility.Confuse,actor.Serial,actor.Location)
            &&NecromancerEffects.ManaBalance256(p)==mana-3328,"Birth Confuse cast/payment");
        var state=NecromancerEffects.Profiles.Find(actor);
        Require(state.Curse is{Ability:CombatAbility.Confuse,Rank:20}&&state.Mind is{Mode:3,Draws:0},"Birth Confuse state");
        // Hold only the inspection fixture's expiry across the long retained
        // regression. Never rewrite its seed, target, draws or deadline on load.
        DateTime until=TimedEffects.Now.AddDays(1);
        NecromancerEffects.Profiles.Store(actor,state with{Curse=state.Curse with{UntilUtc=until},Mind=state.Mind with{UntilUtc=until}});
        TimedEffects.PreventHealing(actor,TimedEffects.Now);TimedEffects.PreventHealing(victim,TimedEffects.Now);
        var saved=Snapshot(p,actor,victim,0,p.Location,p.Map.MapID);Park(p,actor,victim);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var actor=(BaseCreature)World.FindMobile((Serial)s.Actor);var victim=(BaseCreature)World.FindMobile((Serial)s.Victim);
        var state=NecromancerEffects.Profiles.Find(actor);
        Require(s.Generation<2&&state.Curse==s.Curse&&state.Mind==s.Mind
            &&SkillHealth.Balance256(actor)==s.ActorLife&&SkillHealth.Balance256(p)==s.OwnerLife&&SkillHealth.Balance256(victim)==s.VictimLife
            &&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed captured Confuse state/resources");
        Require(SkillMechanics.Rank(p,CombatAbility.Confuse)==20&&SkillBindings.Instance.Get(p).Right==CombatAbility.Confuse,"Saved rank/binding");
        var account=FixtureNetwork.Authenticate(network,p,"confuse-resume-");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);actor.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);victim.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(actor.Map.GetSector(actor.Location).Active,"Reload did not activate the actual NPC sector");
        actor.Frozen=actor.CantWalk=false;actor.AIObject.AITimer.Prod();var deadline=TimedEffects.Now.AddSeconds(12);
        void Observe()
        {
            if(SkillHealth.Balance256(p)>=s.OwnerLife&&SkillHealth.Balance256(victim)>=s.VictimLife&&TimedEffects.Now<deadline)
            {Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                var current=NecromancerEffects.Profiles.Find(actor);var mind=current.Mind;
                Require(p.Alive&&actor.Alive&&victim.Alive
                    &&(SkillHealth.Balance256(p)<s.OwnerLife||SkillHealth.Balance256(victim)<s.VictimLife),
                    $"Reload did not resume actual Confuse combat: actor={actor.Serial}/{actor.Location}, target={actor.Combatant?.Serial}, mind={JsonSerializer.Serialize(mind)}, life={SkillHealth.Balance256(p)}/{s.OwnerLife};{SkillHealth.Balance256(victim)}/{s.VictimLife}, timer={actor.AIObject.AITimer.Running}, frozen={actor.Frozen}/{actor.CantWalk}, sector={actor.Map.GetSector(actor.Location).Active}");
                Require(current.Curse==s.Curse&&mind is{Mode:3}&&mind.Map==s.Mind.Map&&mind.UntilUtc==s.Mind.UntilUtc
                    &&mind.Draws>s.Mind.Draws&&mind.Draws-s.Mind.Draws<100,"Reload reset the curse/override or failed real acquisition draws");
                ulong expected=s.Mind.Seed;for(long i=s.Mind.Draws;i<mind.Draws;i++){SourceRandom.Roll(ref expected);}
                Require(mind.Seed==expected&&(mind.Focus==p.Serial.Value||mind.Focus==victim.Serial.Value),"Natural acquisition did not continue the saved source-algorithm stream");
                Require(SourceMind.Neutral(actor)&&actor.GetMaster()==null&&!actor.Controlled&&!actor.Summoned
                    &&p.CanBeHarmful(actor,false)&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Resumed Confuse granted ownership/immunity or spent owner mana");
                var next=Snapshot(p,actor,victim,s.Generation+1,s.Point,s.Map);Park(p,actor,victim);
                network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(next));
                Log.Information("Confuse resume: generation={Generation}, natural draws={Draws}, target={Target}, owner life={OwnerLife}, victim life={VictimLife}",next.Generation,mind.Draws-s.Mind.Draws,mind.Focus,next.OwnerLife,next.VictimLife);
                Log.Information("confuse-resume-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"confuse-resume-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
    }
}
