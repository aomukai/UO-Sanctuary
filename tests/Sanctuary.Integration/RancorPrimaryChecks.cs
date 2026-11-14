// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class RancorPrimaryChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RancorPrimaryChecks));
    private sealed record Saved(int Generation,uint Owner,uint Target,int X,int Y,RancorPrimaryState State,NecromancerCurse Curse,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"rancor-primary-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.SigilRancor),_=>rank,_=>0,99);
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Rancor isolated primary stats and old curse persistence",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("rancor-primary-check",false);Register("rancor-primary-reload-check",true);
    }
    private static CleaveActionTarget Enemy(PlayerMobile p,int dx)
    {
        var t=new CleaveActionTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);
        SkillHealth.SetBalance256(t,t.HitsMax*256);t.SetResistance(ResistanceType.Physical,0);return t;
    }
    private static int WeaponHit(BaseCreature attacker,BaseCreature target)
    {
        int before=SkillHealth.Balance256(target);SkillCombat.ResolveHitParts(attacker,target,new(100,0,0,0,0),false,weapon:true,enhanceBase:false,addWeaponElements:false);
        return before-SkillHealth.Balance256(target);
    }
    private static void Exercise(PlayerMobile p)
    {
        foreach(int rank in new[]{1,9,10,19,20,40,255})
        {Require(SigilRancorPrimary.Capture(Values(rank))==new RancorPrimaryValues(rank,50+5*(rank-1),5+rank-1,12,rank>=20?8:rank>=10?6:4,75),"Rancor source primary/tier suppliers");}
        var t=Enemy(p,1);var victim=Enemy(p,2);var marker=new Static(0x1B7A){Movable=false};marker.MoveToWorld(p.Location,p.Map);
        try
        {
            Require(WeaponHit(t,victim)==100*256,"Baseline native/shared weapon damage");
            var oldJson=JsonSerializer.Serialize(new NecromancerState());Require(!oldJson.Contains("RancorPrimary")&&JsonSerializer.Deserialize<NecromancerState>(oldJson).RancorPrimary==null,"Legacy optional JSON changed");
            Require(!SigilRancorPrimary.ApplyPrepared(p,t,Values(1),TimedEffects.Now.AddSeconds(1),false)&&SigilRancorPrimary.State(t)==null,"Unqualified recipient accepted");
            Require(SigilRancorPrimary.ApplyPrepared(p,t,Values(1),TimedEffects.Now.AddSeconds(30),true),"Prepared primary state refused");
            Require(WeaponHit(t,victim)==150*256&&NecromancerEffects.DamageStatPercent(t)==50&&GearEffects.AttackSlowTotal(t)==-5
                &&ClassGearHooks.AttackDelay(t,TimeSpan.FromSeconds(1))==TimeSpan.FromTicks(TimeSpan.TicksPerSecond*100/105)&&SourceAttackRate.WolfRate(t)==105,"Actual damage/rate consumers ignored primary stats");
            Require(!SourceMind.Neutral(t)&&NecromancerEffects.Profiles.Find(t).Mind==null&&NecromancerEffects.Curse(t)==null,"Primary stats invented Confuse AI or occupied curse group");
            var state=NecromancerEffects.Profiles.Find(t);bool refused=false;
            try{SigilRancorPrimary.ApplyPrepared(p,t,Values(20),TimedEffects.Now.AddSeconds(30),true);}catch(NotSupportedException){refused=true;}
            Require(refused&&NecromancerEffects.Profiles.Find(t)==state,"Unverified reapplication invented a transition");
            var curse=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,TimedEffects.Now.AddSeconds(30),p.X,p.Y);
            NecromancerEffects.Profiles.Store(t,state with{Curse=curse});
            Require(WeaponHit(t,victim)==117*256&&NecromancerEffects.Curse(t)==curse,"Rancor failed additive weapon ED or replaced ordinary Weaken");
            Require(SigilLethargy.Apply(p,marker,t,1,TimedEffects.Now)&&GearEffects.AttackSlowTotal(t)==45&&SourceAttackRate.WolfRate(t)==55&&NecromancerEffects.Curse(t)==curse,"Independent Lethargy/curse state failed additive attack rate");SigilLethargy.Clear(t);
            var attract=curse with{Ability=CombatAbility.Attract};NecromancerEffects.Profiles.Store(t,NecromancerEffects.Profiles.Find(t)with{Curse=attract});
            SigilRancorPrimary.Clear(t);Require(NecromancerEffects.Curse(t)==attract&&SigilRancorPrimary.DamagePercent(t)==0,"Clearing primary stats removed Attract");
            NecromancerEffects.Profiles.Store(t,new());Require(SigilRancorPrimary.ApplyPrepared(p,t,Values(20),TimedEffects.Now.AddSeconds(30),true),"Rank20 state");
            state=NecromancerEffects.Profiles.Find(t);refused=false;
            try{NecromancerEffects.Profiles.Store(t,state with{RancorPrimary=state.RancorPrimary with{DamagePercent=0}});}catch(InvalidDataException){refused=true;}
            Require(refused&&NecromancerEffects.Profiles.Find(t)==state,"Malformed primary values changed existing state");
            NecromancerEffects.Profiles.Store(t,state with{RancorPrimary=state.RancorPrimary with{UntilUtc=TimedEffects.Now.AddTicks(-1)}});
            Require(SigilRancorPrimary.State(t)==null&&WeaponHit(t,victim)==100*256&&GearEffects.AttackSlowTotal(t)==0&&SourceAttackRate.WolfRate(t)==100,"Expired primary stats still affect damage/rate");
            Require(SigilRancorPulseProposal.Approved&&WarlockCombat.Implemented(CombatAbility.SigilRancor),"Primary receiver enabled unverified learned Sigil");
        }
        finally{NecromancerEffects.Profiles.Clear(t);SigilLethargy.Clear(t);t.Delete();victim.Delete();marker.Delete();}
        Log.Information("Rancor primary:rank tiers,actual100->150weapon damage/native attack delay,additive Weaken/Lethargy,Attract independence,legacy JSON/refusal/expiry pass;AI and learned route remain gated.");
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;CleaveActionTarget target;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;target=World.FindMobile((Serial)saved.Target)as CleaveActionTarget;
            Require(p!=null&&target!=null&&SigilRancorPrimary.State(target)==saved.State&&NecromancerEffects.Curse(target)==saved.Curse&&SkillHealth.Balance256(target)==saved.Life256,"Original primary/curse/fractional wounds changed");
            Require(NecromancerEffects.DamageStatPercent(target)==saved.State.DamagePercent-33&&GearEffects.AttackSlowTotal(target)==-saved.State.AttackRate,"Reloaded primary consumers missing");
            FixtureSpace.Place(p,new(saved.X,saved.Y,0),Map.Trammel);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        }
        else
        {
            p=CleaveActionChecks.Fresh();target=Enemy(p,6);
            Require(SigilRancorPrimary.ApplyPrepared(p,target,Values(20),TimedEffects.Now.Add(FixtureSpace.Retention),true),"Saved explicitly timed primary context");
            var curse=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,TimedEffects.Now.Add(FixtureSpace.Retention),p.X,p.Y);
            NecromancerEffects.Profiles.Store(target,NecromancerEffects.Profiles.Find(target)with{Curse=curse});SkillHealth.SetBalance256(target,target.HitsMax*256-129);
        }
        Exercise(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,p.Serial.Value,target.Serial.Value,p.X,p.Y,SigilRancorPrimary.State(target),NecromancerEffects.Curse(target),SkillHealth.Balance256(target))));
    }
}
