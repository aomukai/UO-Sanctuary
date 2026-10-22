// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ConversionChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(ConversionChecks));
    private sealed record Saved(uint Owner,uint Converted,uint Enemy,Point3D Origin,int Map,int Life,int Maximum,ConversionAllegiance State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"conversion-checks.json");
    public static void Configure()
    {
        Register("conversion-check",Check);Register("conversion-reload-check",Reload);
        EventSink.WorldLoad+=()=>
        {
            if(!File.Exists(Manifest)){return;}
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            foreach(uint serial in new[]{saved.Converted,saved.Enemy})
            {if(World.FindMobile((Serial)serial) is BaseCreature creature){Stop(creature);}}
        };
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Conversion supported attack/allegiance paths and explicit target gates",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Stop(BaseCreature creature){creature.Combatant=null;creature.AIObject?.AITimer.Stop();}
    private static Point3D Place(PlayerMobile owner)
    {
        for(int x=2200;x<=2600;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D p;try{p=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(p,20)){if(actor!=owner){clear=false;break;}}
            if(clear && Server.Spells.SpellHelper.CheckTown(p,owner)){return p;}
        }
        throw new InvalidOperationException("No independent Conversion corridor");
    }
    private static void Protection(PlayerMobile owner,Skeleton converted,Ogre enemy)
    {
        Require(PaladinConversion.Active(converted) && ClassCombat.Friendly(owner,converted) && !ClassCombat.IsHostile(owner,converted),"Converted allegiance did not reach class targeting");
        Require(!owner.CanBeHarmful(converted,false) && !converted.CanBeHarmful(owner,false) && !converted.IsEnemy(owner)
            && converted.IsFriend(owner),"Native player/creature friendly gates failed");
        int life=converted.Hits;
        Require(AOS.Damage(converted,owner,10,100,0,0,0,0)==0 && converted.Hits==life,"Native typed attack hurt converted ally");
        Require(SkillCombat.Hit(owner,converted,10,"fire")==0 && converted.Hits==life,"Offensive spell/aura packet hurt converted ally");
        Require(converted.IsEnemy(enemy) && enemy.IsEnemy(converted) && !converted.IsFriend(enemy) && converted.CanBeHarmful(enemy,false),"Converted/wild opposing allegiance failed");
        converted.NextReacquireTime=Core.TickCount; // Force this test poll without changing the live species cadence.
        Require(converted.AIObject.AcquireFocusMob(12,converted.FightMode,false,false,true) && converted.FocusMob==enemy,
            "Native species AI failed to select wild enemy instead of nearer protected player");
        Require(converted.GetDamageMaster(enemy)==owner,"Native damage ownership ignored converted attacker");
        int hp=enemy.Hits;AOS.Damage(enemy,converted,1,100,0,0,0,0);
        bool responsible=false;
        foreach(var entry in enemy.DamageEntries.ByDescending())
        {if(entry.Damager==converted && entry.Responsible?.Any(r=>r.Damager==owner)==true){responsible=true;}}
        Require(enemy.Hits<hp && responsible,
            "Actual converted damage did not retain owner responsibility");
        enemy.Hits=enemy.HitsMax;converted.Combatant=null;converted.FocusMob=null;
    }
    private static void Expiry(PlayerMobile owner,Skeleton converted,Ogre enemy)
    {
        var lease=PaladinConversion.Snapshot().Single(x=>x.Target==converted.Serial.Value);
        converted.Combatant=enemy;converted.FocusMob=enemy;
        PaladinConversion.Restore(PaladinConversion.Snapshot().Select(x=>x.Target==converted.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray());
        Require(!PaladinConversion.Active(converted) && ClassCombat.IsHostile(owner,converted),"Expiry retained friendly grace period");
        PaladinConversion.Prune();Require(converted.Combatant==null && converted.FocusMob==null && converted.IsEnemy(owner),"Expiry failed to clear old opposing target");
        converted.NextReacquireTime=Core.TickCount;
        Require(converted.AIObject.AcquireFocusMob(12,converted.FightMode,false,false,true) && converted.FocusMob==owner,"Native AI failed to reacquire player after expiry");
        Require(PaladinConversion.ApplyAllegiance(owner,converted,400),"Reconversion after expiry failed");
        Require(PaladinConversion.Snapshot().Single(x=>x.Target==converted.Serial.Value).UntilUtc>lease.UntilUtc.AddSeconds(-1),"Reconversion did not refresh source deadline");
    }
    private static void Rewards(PlayerMobile owner)
    {
        var p=owner.Location;var learner=new PlayerMobile{Body=0x190,Player=true,Name="ConversionRewardFixture"};
        ClassCreation.Profiles.Assign(learner,DiabloClass.Paladin);learner.MoveToWorld(new(p.X+8,p.Y,p.Z),owner.Map);
        var servant=new Skeleton();servant.MoveToWorld(new(p.X+9,p.Y,p.Z),owner.Map);Stop(servant);
        var victim=new Zombie();victim.MoveToWorld(new(p.X+10,p.Y,p.Z),owner.Map);Stop(victim);
        try
        {
            Require(ClassStats.Level(learner)<ClassStats.Level(servant) && !PaladinConversion.Eligible(learner,servant),"Unresolved higher-level scaling branch became available");
            ClassCreation.Profiles.AdvanceLevel(learner,20);
            Require(PaladinConversion.ApplyAllegiance(learner,servant,400),"Reward fixture allegiance failed");
            ClassCreation.Profiles.TryGet(learner.Serial,out var before);
            AOS.Damage(victim,servant,30000,100,0,0,0,0);
            ClassCreation.Profiles.TryGet(learner.Serial,out var after);
            Require(!victim.Alive && ClassLoot.Killer(victim)==learner && after.Experience>before.Experience,"Converted kill did not credit normal source reward");
            // Administrative direct damage bypasses the friendly attack gate so this
            // fixture can exercise converted-death entitlement rather than that gate twice.
            servant.Damage(30000,learner);
            ClassCreation.Profiles.TryGet(learner.Serial,out var ended);
            Require(!servant.Alive && ClassLoot.Killer(servant)==learner && ended.Experience==after.Experience,"Converted death awarded owner experience");
        }
        finally
        {
            if(servant.Corpse is{} a){a.Delete();}if(victim.Corpse is{} b){b.Delete();}
            servant.Delete();victim.Delete();learner.Delete();PaladinConversion.Prune();
        }
    }
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Attack(PlayerMobile owner,Skeleton target)
    {
        var oldHit=PaladinConversion.HitRoll;var oldChance=PaladinConversion.ChanceRoll;
        var oldPhysical=PaladinConversion.PhysicalRoll;var oldElement=PaladinConversion.ElementRoll;
        var origin=owner.Location;
        try
        {
            PaladinConversion.PhysicalRoll=_=>0;PaladinConversion.ElementRoll=()=>0;
            Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Conversion) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Conversion)
                && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Conversion),"Conversion L/R/quick bindings unavailable");
            foreach(int scenario in new[]{0,1,2,3})
            {
                ClassCombat.Stop(owner);var session=ClassCombat.For(owner);session.NextAction=session.RecoveryUntil=Core.TickCount;
                owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());
                PaladinConversion.HitRoll=()=>scenario is 2 or 3?1:0;PaladinConversion.ChanceRoll=_=>scenario is 0 or 3?0:99;
                int before=Life(target),mana=NecromancerEffects.ManaBalance256(owner);long now=Core.TickCount;
                bool began=scenario==1?ClassCombat.ContinueAttack(owner,CombatAbility.Conversion,target,target.Location,now)
                    :scenario==2?ClassCombat.ContinueCast(owner,CombatAbility.Conversion,target,target.Location,now)
                    :ClassCombat.Perform(owner,CombatAbility.Conversion,target,target.Location,now);
                ClassCombat.ReleaseCast(owner);
                Require(began && session.Conversion!=null && mana-NecromancerEffects.ManaBalance256(owner)==1024 && Life(target)==before,"Conversion start did not prepare/pay once without early damage");
                Require(!ClassCombat.ResolveAction(owner,session.EffectAt-1) && Life(target)==before,"Conversion committed before attack event");
                Require(ClassCombat.ResolveAction(owner,session.EffectAt) && session.Conversion==null && mana-NecromancerEffects.ManaBalance256(owner)==1024,"Conversion event failed or charged twice");
                if(scenario is 0 or 3)
                {
                    Require(PaladinConversion.Active(target) && Life(target)==before,"Successful Conversion dealt weapon damage or required successful hit flag");
                    PaladinConversion.Restore(PaladinConversion.Snapshot().Select(x=>x.Target==target.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray());
                    PaladinConversion.Prune();
                }
                else{Require(!PaladinConversion.Active(target) && (scenario==1?Life(target)<before:Life(target)==before),"Failed Conversion hit/miss damage changed");}
                Require(!ClassCombat.ResolveAction(owner,session.EffectAt+1),"Conversion event replayed");
                target.Hits=target.HitsMax;SkillHealth.Clear(target);Stop(target);
            }
            ClassCombat.Stop(owner);var state=ClassCombat.For(owner);state.NextAction=state.RecoveryUntil=Core.TickCount;
            owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());int hp=Life(target);
            Require(ClassCombat.Perform(owner,CombatAbility.Conversion,target,target.Location,Core.TickCount),"Conversion interruption fixture start");
            owner.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),owner.Map);
            Require(!ClassCombat.ResolveAction(owner,state.EffectAt) && state.Conversion==null && Life(target)==hp,"Movement failed to cancel prepared Conversion");
            owner.MoveToWorld(origin,owner.Map);ClassCombat.Stop(owner);state.NextAction=Core.TickCount;
            owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());
            Require(!ClassCombat.Perform(owner,CombatAbility.Conversion,target,target.Location,Core.TickCount),"Conversion started without four mana");
        }
        finally
        {
            PaladinConversion.HitRoll=oldHit;PaladinConversion.ChanceRoll=oldChance;PaladinConversion.PhysicalRoll=oldPhysical;PaladinConversion.ElementRoll=oldElement;
            ClassCombat.Stop(owner);owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());
        }
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.Name="ConversionFixture-Paladin";owner.MoveToWorld(Place(owner),owner.Map);
        var p=owner.Location;var converted=new Skeleton{NoKillAwards=true};converted.MoveToWorld(new(p.X+1,p.Y,p.Z),owner.Map);Stop(converted);
        var enemy=new Ogre{NoKillAwards=true};enemy.SetHits(30000);enemy.MoveToWorld(new(p.X+3,p.Y,p.Z),owner.Map);Stop(enemy);
        var ai=converted.AIObject;int life=converted.Hits,maximum=converted.HitsMax;var species=converted.GetType();int team=converted.Team;
        Require(PaladinConversion.Eligible(owner,converted) && !PaladinConversion.Eligible(owner,enemy),"Source eligibility guessed unmapped species");
        Require(ClassCombat.Available(owner,CombatAbility.Conversion),"Supported Conversion paths are unavailable");
        Attack(owner,converted);
        var now=TimedEffects.Now;Require(PaladinConversion.ApplyAllegiance(owner,converted,400),"Source-compatible same/lower-level allegiance failed");
        var lease=PaladinConversion.Snapshot().Single(x=>x.Target==converted.Serial.Value);
        Require(lease.UntilUtc>=now.AddSeconds(16) && lease.UntilUtc<=TimedEffects.Now.AddSeconds(16),"Pinned Conversion duration changed");
        Require(converted.GetType()==species && ReferenceEquals(converted.AIObject,ai) && converted.Hits==life && converted.HitsMax==maximum
            && converted.Team==team && !converted.Controlled && !converted.Summoned && converted.GetMaster()==null,"Conversion replaced species/AI/ownership/stats with generic pet");
        Protection(owner,converted,enemy);Expiry(owner,converted,enemy);Protection(owner,converted,enemy);
        var neutral=new Rabbit();neutral.MoveToWorld(new(p.X+4,p.Y,p.Z),owner.Map);Stop(neutral);
        Require(!converted.IsEnemy(neutral) && !converted.IsFriend(neutral),"Neutral creature was promoted to converted friend or enemy");neutral.Delete();
        Rewards(owner);
        var pet=new Zombie{NoKillAwards=true};pet.MoveToWorld(new(p.X+2,p.Y,p.Z),owner.Map);Stop(pet);pet.SetControlMaster(owner);
        Require(!PaladinConversion.Eligible(owner,pet) && ClassCombat.Friendly(pet,converted) && !converted.CanBeHarmful(pet,false),"Converted ally harmed pet or converted owned pet");pet.Delete();
        // Hold this fixture's absolute deadline across the much longer combined suite.
        // The16-second source duration and real timer expiry have separate checks.
        PaladinConversion.Restore(PaladinConversion.Snapshot().Select(x=>x.Target==converted.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMinutes(10)}:x).ToArray());
        lease=PaladinConversion.Snapshot().Single(x=>x.Target==converted.Serial.Value);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,converted.Serial.Value,enemy.Serial.Value,p,owner.Map.MapID,life,maximum,lease)));
        var elapsed=new Zombie{NoKillAwards=true};elapsed.MoveToWorld(new(p.X+5,p.Y,p.Z),owner.Map);Stop(elapsed);
        Require(PaladinConversion.ApplyAllegiance(owner,elapsed,5),"Natural-expiry fixture start failed");
        Timer.StartTimer(TimeSpan.FromMilliseconds(400),()=>
        {
            try{Require(!PaladinConversion.Active(elapsed) && ClassCombat.IsHostile(owner,elapsed) && elapsed.IsEnemy(owner),"Natural expiry did not restore hostility");
                _log.Information("conversion-result: passed");}
            catch(Exception e){_log.Error(e,"conversion-result: failed");}
            finally{elapsed.Delete();}
        });
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var converted=World.FindMobile((Serial)saved.Converted) as Skeleton;var enemy=World.FindMobile((Serial)saved.Enemy) as Ogre;
        Require(owner!=null && converted!=null && enemy!=null,"Conversion reload lost species actors");
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        Require(PaladinConversion.Snapshot().Single(x=>x.Target==saved.Converted)==saved.State && converted.Hits==saved.Life && converted.HitsMax==saved.Maximum
            && !converted.Controlled && !converted.Summoned && converted.GetMaster()==null,"Reload changed allegiance deadline/identity/ownership/stats");
        Protection(owner,converted,enemy);
        Require(ClassCombat.Available(owner,CombatAbility.Conversion) && ClassCombat.For(owner).Conversion==null,"Reload lost supported Conversion or replayed prepared attack");
    }
}
