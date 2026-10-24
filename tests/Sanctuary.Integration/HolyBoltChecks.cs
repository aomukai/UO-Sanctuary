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

public static class HolyBoltChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(HolyBoltChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Pet,uint Undead,HolyBoltFlight Flight);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"holy-bolt-checks.json");
    public static void Configure(){Register("holy-bolt-check",Check);Register("holy-bolt-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Bolt source/healing/piercing and native reload checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static SkillValues Golden(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.HolyBolt),k=>k=="Holy Bolt"?rank:40,k=>k=="Holy Bolt"?rank:hard,99);
    private static Point3D Space(PlayerMobile owner)
    {
        for(int x=2000;x<2100;x++)
        for(int y=1000;y<1100;y++)
        {
            int z=owner.Map.GetAverageZ(x,y);var p=new Point3D(x,y,z);
            if(Enumerable.Range(0,15).All(i=>owner.Map.CanSpawnMobile(x+i,y,z))
                && owner.Map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+14,y,z+12))){return p;}
        }
        throw new InvalidOperationException("No long holy bolt corridor");
    }
    private static T Place<T>(PlayerMobile owner,T target,int dx) where T:BaseCreature
    {
        target.NoKillAwards=target.Frozen=target.CantWalk=true;target.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);
        target.AIObject.AITimer.Stop();target.SetHits(20000);target.Hits=target.HitsMax;return target;
    }
    private static void FreezeRecovery(Mobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static void Replace(HolyBoltFlight flight)=>HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Id!=flight.Id).Append(flight).ToArray());
    private static void Pipeline(PlayerMobile owner,PlayerMobile ally)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.HolyBolt) && SkillBindings.Instance.Assign(owner,false,CombatAbility.HolyBolt)
            && PaladinAuras.Selected(owner)==CombatAbility.None && ClassAbilities.AllyTarget(CombatAbility.HolyBolt)
            && CombatTiming.Range(owner,CombatAbility.HolyBolt)==16,"Holy Bolt slots/ally/range or accidental aura emitter");
        Require(!PaladinCombat.ValidTarget(owner,CombatAbility.HolyBolt,owner.Serial,owner.Location),"Self healing allowed");
        owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());
        Require(!ClassCombat.Perform(owner,CombatAbility.HolyBolt,ally,ally.Location,Core.TickCount),"Unpaid cast began");
        owner.Mana=owner.ManaMax;var session=ClassCombat.For(owner);session.NextAction=Core.TickCount;
        int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.ContinueAttack(owner,CombatAbility.HolyBolt,ally,ally.Location,Core.TickCount),"Left-slot held allied cast");
        Require(session.PendingAbility==CombatAbility.HolyBolt && NecromancerEffects.ManaBalance256(owner)==mana,"Cast debited before event");
        ClassCombat.ReleaseCast(owner);var original=owner.Location;owner.MoveToWorld(new(original.X,original.Y+1,original.Z),owner.Map);
        Require(!ClassCombat.ResolveAction(owner,session.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana && !HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Interrupted cast paid/launched");
        owner.MoveToWorld(original,owner.Map);session.NextAction=Core.TickCount;
        Require(ClassCombat.ContinueCast(owner,CombatAbility.HolyBolt,ally,ally.Location,Core.TickCount),"Right-slot held allied cast");ClassCombat.ReleaseCast(owner);
        Require(ClassCombat.ResolveAction(owner,session.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana-512,"Cast event/payment/launch");
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.MoveToWorld(Space(owner),owner.Map);
        var rank1=Golden(1);var rank20=Golden(20,20);
        Require(rank1.Damage256(false)==2048 && rank1.Damage256(true)==4096 && rank1.Mana256==512
            && rank20.Damage256(false)==551936 && rank20.Damage256(true)==619520 && rank20.Eval("calc1")==195 && rank20.Eval("calc2")==410
            && rank20.Mana256==816,"Pinned Holy Bolt damage/healing/hard synergies/mana");
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);FreezeRecovery(owner);FreezeRecovery(ally);ally.Hits-=50;
        var pet=Place(owner,new GraveyardSkeleton(),4);Require(pet.SetControlMaster(ally),"Holy Bolt pet owner");FreezeRecovery(pet);pet.Hits-=50;
        var undead=new LichLord();MonsterVariants.Assign(undead,MonsterDifficulty.Hell);Place(owner,undead,6);
        var demon=Place(owner,new Daemon(),8);var beast=Place(owner,new Ogre(),10);var behind=Place(owner,new GraveyardSkeleton(),12);
        foreach(var target in new BaseCreature[]{undead,demon,beast,behind}){FreezeRecovery(target);}
        Require(NamedElements.MagicResistance(undead)==100 && HolyBolts.FullDamage(undead) && HolyBolts.FullDamage(demon) && !HolyBolts.FullDamage(beast) && HolyBolts.Candidate(owner,beast),"Real species and magic immunity fixture");
        owner.Hits-=10;int oh=owner.Hits,ah=ally.Hits,ph=pet.Hits,uh=undead.Hits,dh=demon.Hits,bh=beast.Hits,last=behind.Hits;
        var roll=HolyBolts.Roll;HolyBolts.Roll=_=>0;Pipeline(owner,ally);
        var flight=HolyBolts.Snapshot().Single(x=>x.Owner==owner.Serial.Value);
        Require(flight.Rank==1 && flight.Min256==3072 && flight.Max256==6144 && flight.Vx==.46875 && flight.Vy==0 && HolyBolts.Frames==50,"Cast damage/rank/vector/lifetime snapshot");
        for(int i=0;i<4;i++){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.prayer",out _),"Prayer fixture training");}
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.holybolt",out _) && ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.fistoftheheavens",out _),"Later source ranks");
        Later(2200,"holy-bolt-result",()=>
        {
            try
            {
                Require(ally.Hits==ah+2 && pet.Hits==ph+2 && owner.Hits==oh,$"Current hard Prayer/original bolt rank, ally/pet preference or self healing: ally{ally.Hits-ah}, pet{pet.Hits-ph}, owner{owner.Hits-oh}; heal{HolyBolts.Healing(owner,1)}; flights{HolyBolts.Snapshot().Count(x=>x.Owner==owner.Serial.Value)}");
                Require(undead.Hits==uh-12 && demon.Hits==dh-12 && behind.Hits==last-12 && beast.Hits==bh,$"Piercing damage, undead immunity bypass, demon inclusion, beast exclusion or cast snapshot: undead{uh-undead.Hits}, demon{dh-demon.Hits}, behind{last-behind.Hits}, beast{bh-beast.Hits}");
                Require(!HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Source50-frame bolt did not expire");
                Expiry(owner,undead,()=>{Seed(owner,ally,pet,undead);_log.Information("holy-bolt-result: passed");});
            }
            finally{HolyBolts.Roll=roll;}
        });
    }
    private static void Expiry(PlayerMobile owner,BaseCreature undead,Action finish)
    {
        int hp=undead.Hits;
        for(int frame=48;frame<=49;frame++)
        {
            HolyBolts.Cast(owner,new(owner.X+14,owner.Y,owner.Z),SkillMechanics.Values(owner,CombatAbility.HolyBolt,1));
            var flight=HolyBolts.Snapshot().Where(x=>x.Owner==owner.Serial.Value).MaxBy(x=>x.Id);
            Replace(flight with{Frame=frame,X=owner.X+5,Y=owner.Y,Min256=3072,Max256=3072,NextUtc=TimedEffects.Now.AddMilliseconds(-120),UntilUtc=TimedEffects.Now.AddSeconds(1)});
        }
        Later(160,"holy-bolt-result",()=>
        {
            Require(undead.Hits==hp-12 && !HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Source decrement-before-collision, final collidable frame49 or bounded overdue expiry");
            finish();
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,BaseCreature pet,BaseCreature undead)
    {
        // Snapshot inspection hold only; ordinary duration/pulse checks remain separate.
        HolyBolts.Cast(owner,new(owner.X+14,owner.Y,owner.Z),SkillMechanics.Values(owner,CombatAbility.HolyBolt,1));
        var flight=HolyBolts.Snapshot().Single(x=>x.Owner==owner.Serial.Value);var at=TimedEffects.Now.AddMinutes(10);
        flight=flight with{NextUtc=at,UntilUtc=at.AddSeconds(2),X=owner.X+1,Y=owner.Y,Frame=2,LastCollision=ally.Serial.Value,Min256=3072,Max256=6144};
        Replace(flight);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,pet.Serial.Value,undead.Serial.Value,flight)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var flight=HolyBolts.Snapshot().Single(x=>x.Id==saved.Flight.Id);
        Require(JsonSerializer.Serialize(flight)==JsonSerializer.Serialize(saved.Flight),"Saved holy bolt rank/damage/last collide/vector/frame/deadlines changed");
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(new((int)(flight.X-1),(int)flight.Y,flight.Z),Map.Maps[flight.Map]);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);ally.ClaimAutoStabledPets();
        var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);Require(pet.Controlled && pet.ControlMaster==ally,"Holy Bolt pet claim");pet.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        var undead=(BaseCreature)World.FindMobile((Serial)saved.Undead);undead.MoveToWorld(new(owner.X+6,owner.Y,owner.Z),owner.Map);undead.Frozen=undead.CantWalk=true;undead.AIObject.AITimer.Stop();
        FreezeRecovery(ally);FreezeRecovery(pet);FreezeRecovery(undead);ally.Hits-=20;pet.Hits-=20;int ah=ally.Hits,ph=pet.Hits,uh=undead.Hits;
        var roll=HolyBolts.Roll;HolyBolts.Roll=_=>0;Replace(flight with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(2)});
        Later(900,"holy-bolt-reload-result",()=>
        {
            try
            {
                Require(ally.Hits==ah && pet.Hits==ph+2 && undead.Hits==uh-12,"Reloaded holy bolt failed last-collider exclusion/healing/piercing/immune damage");
                Replace(saved.Flight);_log.Information("holy-bolt-reload-result: passed");
            }
            finally{HolyBolts.Roll=roll;}
        });
    }
}
