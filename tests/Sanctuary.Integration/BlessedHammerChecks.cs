// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BlessedHammerChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(BlessedHammerChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,HammerFlight Flight);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blessed-hammer-checks.json");
    public static void Configure(){Register("blessed-hammer-check",Check);Register("blessed-hammer-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Blessed Hammer spiral, damage, mana, and native save/reload checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static SkillValues Golden(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.BlessedHammer),_=>rank,_=>hard,99);
    private static T Place<T>(PlayerMobile owner,T target,int dx,int dy=0) where T:BaseCreature
    {
        target.NoKillAwards=target.Frozen=target.CantWalk=true;target.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);
        target.AIObject.AITimer.Stop();target.SetHits(20000);target.Hits=target.HitsMax;FreezeRecovery(target);return target;
    }
    private static void FreezeRecovery(Mobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static void Clear(PlayerMobile owner)=>BlessedHammers.Restore(BlessedHammers.Snapshot().Where(x=>x.Owner!=owner.Serial.Value).ToArray());
    private static void Replace(HammerFlight flight)=>BlessedHammers.Restore(BlessedHammers.Snapshot().Where(x=>x.Id!=flight.Id).Append(flight).ToArray());
    private static HammerFlight Launch(PlayerMobile owner)
    {
        BlessedHammers.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BlessedHammer));
        return BlessedHammers.Snapshot().Where(x=>x.Owner==owner.Serial.Value).MaxBy(x=>x.Id);
    }
    private static void Source(PlayerMobile owner)
    {
        var one=Golden(1);var twenty=Golden(20,20);
        Require(one.Mana256==1280 && Golden(2).Mana256==1344 && twenty.Mana256==2496
            && one.Damage256(false)==3072 && one.Damage256(true)==4096 && twenty.Damage256(false)==331161 && twenty.Damage256(true)==337920,
            "Pinned Hammer damage tiers, hard synergies or fractional mana");
        var path=BlessedHammers.Waypoints();string encoded=string.Join(";",path.Select(p=>$"{p.X},{p.Y}"));
        Require(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(encoded))).ToLowerInvariant()=="00976c68d4ae06770a2a83cfe817c7b3952265c6bf47404d5177d722a241b89d",
            "All77 original-source waypoints (with explicit reconstructed Fog float trig) changed");
        var flight=Launch(owner);Clear(owner);
        Require(BlessedHammers.Frames==120 && flight.VelocityQ8==3456 && flight.AccelerationQ8==250,"Factory velocity, acceleration or range");
        var first=BlessedHammers.Move(flight);var second=BlessedHammers.Move(first);
        Require(first.XFp16==0 && first.YFp16==55296 && first.Point==0 && second.XFp16==0 && second.YFp16==65536 && second.Point==1,
            "FP16 start, first waypoint snap or discarded remaining distance");
        for(int frame=1;frame<=120;frame++)
        {
            flight=BlessedHammers.Move(flight with{Point=0,XFp16=0,YFp16=0});
            Require(flight.VelocityQ8==Math.Min(7680,3456+frame/5*250),"Original acceleration/clamp frame"+frame);
        }
        Require(flight.AccelerationQ8==0 && flight.AccelerationCounter==0,"Maximum speed did not clear acceleration");
    }
    private static void Species(PlayerMobile anchor)
    {
        // Separate actor: the real native DemonKnight retaliates and can queue bone hazards. Keep those outside the trajectory fixture.
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.MoveToWorld(new(anchor.X+40,anchor.Y,anchor.Z),anchor.Map);FreezeRecovery(owner);
        var beast=Place(owner,new Ogre(),8,3);var undead=Place(owner,new GraveyardSkeleton(),9,3);
        var immune=new LichLord();MonsterVariants.Assign(immune,MonsterDifficulty.Hell);Place(owner,immune,10,3);
        var demon=new DemonKnight();MonsterVariants.Assign(demon,MonsterDifficulty.Hell);Place(owner,demon,11,3);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+12,owner.Y+3,owner.Z),owner.Map);FreezeRecovery(ally);
        Require(NamedElements.MagicResistance(immune)==100 && NamedElements.MagicResistance(demon)==25 && GemCombat.Undead(undead) && NamedCombat.Demon(demon),"Real species/resistance fixtures");
        var flight=Launch(owner) with{Min256=2560,Max256=2560};Clear(owner);int hp=owner.Hits-=20,mp=owner.Mana;
        int bh=beast.Hits,uh=undead.Hits,ih=immune.Hits,dh=demon.Hits,ah=ally.Hits;
        BlessedHammers.Impact(owner,beast,flight);BlessedHammers.Impact(owner,undead,flight);BlessedHammers.Impact(owner,immune,flight);
        BlessedHammers.Impact(owner,ally,flight);BlessedHammers.Impact(owner,owner,flight);
        Require(owner.Hits==hp && owner.Mana==mp,"Hammer healed/leeched its caster or hit self");
        BlessedHammers.Impact(owner,demon,flight);
        Require(beast.Hits==bh-10 && undead.Hits==uh-15 && immune.Hits==ih && demon.Hits==dh-11
            && SkillHealth.Remainder256(demon)==64 && ally.Hits==ah && owner.Mana==mp,
            $"Species bonus, ordinary magic immunity/resistance, fractional damage, friendship or leech: beast{bh-beast.Hits}, undead{uh-undead.Hits}, immune{ih-immune.Hits}, demon{dh-demon.Hits}, debt{SkillHealth.Remainder256(demon)}, ally{ah-ally.Hits}, owner{hp-owner.Hits}, mana{mp-owner.Mana}");
        var roll=BlessedHammers.Roll;int draws=0;BlessedHammers.Roll=range=>{Require(range==256,"Exclusive Hammer range");draws++;return 255;};
        try{bh=beast.Hits;BlessedHammers.Impact(owner,beast,flight with{Max256=2816});Require(draws==2 && beast.Hits==bh-10,"Source fill/reroll or exclusive upper damage endpoint");}
        finally{BlessedHammers.Roll=roll;}
        foreach(var actor in new Mobile[]{beast,undead,immune,demon,ally,owner}){actor.Delete();}
    }
    private static void Pipeline(PlayerMobile owner)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BlessedHammer) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Concentration)
            && ClassAbilities.SelfTarget(CombatAbility.BlessedHammer) && !PaladinCombat.IsAura(CombatAbility.BlessedHammer)
            && PaladinAuras.Selected(owner)==CombatAbility.Concentration && PaladinOffense.HammerBonus(owner)==30,"Hammer slots/self target/Concentration");
        owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());Require(!ClassCombat.Perform(owner,CombatAbility.BlessedHammer,null,owner.Location,Core.TickCount),"Unpaid Hammer began");
        owner.Mana=owner.ManaMax;var session=ClassCombat.For(owner);session.NextAction=Core.TickCount;int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.ContinueAttack(owner,CombatAbility.BlessedHammer,null,owner.Location,Core.TickCount),"Left Hammer held cast");ClassCombat.ReleaseCast(owner);
        Require(NecromancerEffects.ManaBalance256(owner)==mana,"Hammer paid before cast event");
        var original=owner.Location;owner.MoveToWorld(new(original.X,original.Y+1,original.Z),owner.Map);
        Require(!ClassCombat.ResolveAction(owner,session.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana && !BlessedHammers.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Interrupted Hammer paid/launched");
        owner.MoveToWorld(original,owner.Map);session.NextAction=Core.TickCount;
        Require(ClassCombat.ContinueAttack(owner,CombatAbility.BlessedHammer,null,new(owner.X+10,owner.Y,owner.Z),Core.TickCount),"Hammer point cast");ClassCombat.ReleaseCast(owner);
        Require(ClassCombat.ResolveAction(owner,session.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana-1280,"Hammer event launch/payment");
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.MoveToWorld(ClassSkillChecks.Corridor(owner,2240,1000),owner.Map);FreezeRecovery(owner);
        Source(owner);Species(owner);Pipeline(owner);
        var flight=BlessedHammers.Snapshot().Single(x=>x.Owner==owner.Serial.Value);
        Require(flight.Rank==1 && flight.Min256==5111 && flight.Max256==6814 && flight.OriginX==owner.X && flight.OriginY==owner.Y,"Concentration +hard synergies/launch snapshot");
        var first=Place(owner,new GraveyardSkeleton(),0,1);var second=Place(owner,new Ogre(),-1,0);int firstHp=first.Hits,secondHp=second.Hits;
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.blessedhammer",out _) && ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.vigor",out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.blessedaim",out _),"Later Hammer/hard synergy fixture");
        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.None),"Clear Concentration");
        Require(BlessedHammers.Damage(owner,SkillMechanics.Values(owner,CombatAbility.BlessedHammer,1)).Min==4792 && flight.Min256==5111,"Live training/aura removal changed launched damage");
        var roll=BlessedHammers.Roll;BlessedHammers.Roll=_=>0;
        Later(500,"blessed-hammer-result",()=>
        {
            try
            {
                Require(first.Hits<firstHp && second.Hits<secondHp,$"Native spiral failed to pierce enemy types: first{firstHp-first.Hits}, second{secondHp-second.Hits}");
                Clear(owner);Expiry(owner,first,second,()=>{Seed(owner,first,second);_log.Information("blessed-hammer-result: passed");});
            }
            finally{BlessedHammers.Roll=roll;}
        });
    }
    private static void Expiry(PlayerMobile owner,BaseCreature first,BaseCreature second,Action finish)
    {
        second.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);first.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);int hp=first.Hits;
        for(int frame=118;frame<=119;frame++)
        {
            var flight=Launch(owner);Replace(flight with{Frame=frame,Min256=2560,Max256=2560,NextUtc=TimedEffects.Now.AddMilliseconds(-120),UntilUtc=TimedEffects.Now.AddSeconds(1)});
        }
        Later(160,"blessed-hammer-result",()=>
        {
            Require(first.Hits==hp-15 && !BlessedHammers.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Final119 collidable frame, independent concurrent flights or decrement before collision");finish();
        });
    }
    private static void Seed(PlayerMobile owner,BaseCreature first,BaseCreature second)
    {
        // Snapshot inspection hold only; ordinary duration/pulse checks remain separate.
        var flight=Launch(owner);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        flight=flight with{Frame=2,Point=1,XFp16=0,YFp16=65536,VelocityQ8=3706,AccelerationCounter=3,LastCollision=first.Serial.Value,
            Min256=2560,Max256=2560,NextUtc=at,UntilUtc=at.AddSeconds(5)};
        Replace(flight);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,first.Serial.Value,second.Serial.Value,flight)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var flight=BlessedHammers.Snapshot().Single(x=>x.Id==saved.Flight.Id);
        Require(JsonSerializer.Serialize(flight)==JsonSerializer.Serialize(saved.Flight),"Hammer rank/origin/FP16/path cursor/velocity/accel/last collision/damage/deadlines changed");
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(new(flight.OriginX,flight.OriginY,flight.Z),Map.Maps[flight.Map]);FreezeRecovery(owner);
        var first=(BaseCreature)World.FindMobile((Serial)saved.First);first.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);FreezeRecovery(first);
        var second=(BaseCreature)World.FindMobile((Serial)saved.Second);second.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);FreezeRecovery(second);
        first.Frozen=first.CantWalk=second.Frozen=second.CantWalk=true;first.AIObject.AITimer.Stop();second.AIObject.AITimer.Stop();int fh=first.Hits,sh=second.Hits;
        Replace(flight with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(5)});
        Later(120,"blessed-hammer-reload-result",()=>
        {
            Require(first.Hits==fh && second.Hits==sh,"Saved most recent collider was hit again without an intervening target");
            Clear(owner);second.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);
            Replace(flight with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(5)});
            Later(120,"blessed-hammer-reload-result",()=>
            {
                Require(first.Hits==fh-15 && second.Hits==sh-20,$"Resumed path/last-collider history did not permit rehit after a different target: first{fh-first.Hits}, second{sh-second.Hits}");
                Clear(owner);Expiry(owner,first,second,()=>{Replace(saved.Flight);_log.Information("blessed-hammer-reload-result: passed");});
            });
        });
    }
}
