using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ChainLightningChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(ChainLightningChecks));
    private sealed record Saved(uint Owner, uint First, uint Next, ChainFlight Flight);
    private static string Manifest => Path.Join(Core.BaseDirectory, "chain-lightning-checks.json");
    public static void Configure() { Register("chain-lightning-check", Check); Register("chain-lightning-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Chain Lightning flights, cast-local delay, targeting and persistence",
        _ => Core.LoopContext.Post(() => { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static void Later(int milliseconds, string result, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds), () =>
    { try { action(); } catch (Exception e) { _log.Error(e, "{Name}: failed", result); } });
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int x, int y = 0)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new(owner.X + x, owner.Y + y, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop(); enemy.SetHits(20000); enemy.Hits = enemy.HitsMax;
        enemy.SetResistance(ResistanceType.Energy, 0); return enemy;
    }
    private static Point3D Space(PlayerMobile owner)
    {
        for (int x = 5800; x < 5900; x++)
        for (int y = 1800; y < 1900; y++)
        {
            int z = owner.Map.GetAverageZ(x,y); var start = new Point3D(x,y,z+12);
            if (Enumerable.Range(0,6).All(i => Enumerable.Range(-1,3).All(j => owner.Map.CanSpawnMobile(x+i,y+j,z)
                && owner.Map.LineOfSight(start,new Point3D(x+i,y+j,z+12))))) { return new(x,y,z); }
        }
        throw new InvalidOperationException("No clear Chain Lightning rectangle");
    }
    private static ChainFlight Flight(PlayerMobile owner) => ChainLightning.Snapshot().Single(x => x.Owner == owner.Serial.Value);
    private static void Replace(ChainFlight flight) => ChainLightning.Restore(ChainLightning.Snapshot().Where(x => x.Cast != flight.Cast).Append(flight).ToArray());
    private static void Cast(PlayerMobile owner)
    {
        owner.Mana = owner.ManaMax;
        Require(SorceressCombat.Release(owner, CombatAbility.ChainLightning, Serial.Zero, new(owner.X+4,owner.Y,owner.Z)), "Chain Lightning release failed");
    }
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress); owner.MoveToWorld(Space(owner), owner.Map);
        var row = SkillMechanics.For(CombatAbility.ChainLightning); var values = SkillMechanics.Values(owner, CombatAbility.ChainLightning);
        var high = new SkillValues(row,k => k==row.SourceKey?20:0,_=>0,99);
        // Compiled SKILLS_GetMinElemDamage skips synergy on the fixed1-point minimum.
        Require(values.Mana256 == 9*256 && values.Eval("calc1")==5 && values.Damage256(false)==256 && values.Damage256(true)==11468
            && high.Mana256==28*256 && high.Eval("calc1")==9 && high.Damage256(true)==71936
            && ChainLightning.Frames==25 && ChainLightning.DelayFrames==4, "Pinned chain tiers, synergies, cost, count or delay");
        int mana=owner.Mana;
        Require(!SorceressCombat.Release(owner,CombatAbility.ChainLightning,Serial.Zero,new(-1,-1,0)) && owner.Mana==mana, "Invalid target spent mana");
        owner.Mana=0;
        Require(!SorceressCombat.Release(owner,CombatAbility.ChainLightning,Serial.Zero,new(owner.X+4,owner.Y,owner.Z))
            && !ChainLightning.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Insufficient mana launched a chain"); owner.Mana=owner.ManaMax;
        var first=Enemy(owner,2); var next=Enemy(owner,4); var nearer=Enemy(owner,2,1); int life=first.Hits;
        Cast(owner); var original=Flight(owner);
        Require(owner.Mana==mana-9 && original.Rank==1 && original.Remaining==5 && original.Min256==384 && original.Max256==17202
            && original.Vx==.703125 && original.Vy==0 && first.Hits==life, "Release snapshot, source velocity or delayed travel");
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"sorceress.chainlightning",out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial,"sorceress.lightning",out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial,"sorceress.lightningmastery",out _), "Child snapshot training fixture");
        Later(100,"chain-lightning-result",()=>
        {
            var child=Flight(owner);
            Require(first.Hits<life && next.Hits==life && child.Remaining==4 && child.AimTarget==next.Serial.Value && child.Rank==1
                && child.Min256==414 && child.Max256==19242 && child.Delays.Any(x=>x.Target==first.Serial.Value),
                $"Identity selection or original rank/fresh child synergy/mastery: {child}");
        });
        Later(1000,"chain-lightning-result",()=>
        {
            Require(first.Hits<life && next.Hits<life && nearer.Hits<life && !ChainLightning.Snapshot().Any(x=>x.Owner==owner.Serial.Value),
                "Chain failed to bounce through available identities or terminate its finite count");
            nearer.Delete(); first.SetResistance(ResistanceType.Energy,100); life=first.Hits; int nextLife=next.Hits;
            Cast(owner); Replace(Flight(owner) with { Min256=2560, Max256=2560 });
            Later(250,"chain-lightning-result",()=>
            {
                Require(first.Hits==life && next.Hits<nextLife && !ChainLightning.Snapshot().Any(x=>x.Owner==owner.Serial.Value), "Immune hit failed to bounce or ignored cast delay");
                next.Delete(); first.SetResistance(ResistanceType.Energy,0); Independent(owner,first);
            });
        });
    }
    private static void Independent(PlayerMobile owner, GraveyardSkeleton target)
    {
        int life=target.Hits; Cast(owner); var a=Flight(owner) with { Remaining=1, Min256=2560,Max256=2560 }; Replace(a);
        int mana=owner.Mana;
        Require(SorceressCombat.Release(owner,CombatAbility.ChainLightning,Serial.Zero,new(owner.X+4,owner.Y,owner.Z)) && owner.Mana==mana-10,
            "Independent second cast did not pay its own rank2 cost");
        var b=ChainLightning.Snapshot().Single(x=>x.Owner==owner.Serial.Value && x.Cast!=a.Cast);
        Replace(b with { Remaining=1,Min256=2560,Max256=2560 });
        Later(220,"chain-lightning-result",()=>
        {
            Require(target.Hits==life-20, "Same-caster casts incorrectly share next-hit delay");
            var other=ClassSkillChecks.Player(DiabloClass.Sorceress); other.MoveToWorld(owner.Location,owner.Map); life=target.Hits;
            Cast(owner); Replace(Flight(owner) with { Remaining=1,Min256=2560,Max256=2560 });
            Cast(other); Replace(Flight(other) with { Remaining=1,Min256=2560,Max256=2560 });
            Later(220,"chain-lightning-result",()=>
            {
                Require(target.Hits==life-20 && other.Hits==other.HitsMax && owner.Hits==owner.HitsMax, "Different casters shared delay or hit allies");
                other.Delete(); life=target.Hits; Cast(owner); var at=TimedEffects.Now;
                // Hold a synthetic stationary overlapping segment to observe both sides of the four-frame gate.
                var delayed=Flight(owner) with { Remaining=1,X=target.X,Y=target.Y,Vx=0,Vy=0,Min256=2560,Max256=2560,
                    NextUtc=at,UntilUtc=at.AddSeconds(1),Delays=[new(target.Serial.Value,at.AddMilliseconds(160))] }; Replace(delayed);
                Require(ChainLightning.Delayed(delayed,target.Serial.Value,at.AddMilliseconds(159))
                    && !ChainLightning.Delayed(delayed,target.Serial.Value,at.AddMilliseconds(160)), "Delay boundary is not strict four source frames");
                var range=delayed with { Cast=delayed.Cast+1,X=owner.X,Y=owner.Y,Delays=[] };
                ChainLightning.Restore(ChainLightning.Snapshot().Append(range).ToArray());
                Later(110,"chain-lightning-result",()=>Require(target.Hits==life,"Same cast hit inside its delay"));
                Later(260,"chain-lightning-result",()=>
                {
                    Require(target.Hits==life-10 && !ChainLightning.Snapshot().Any(x=>x.Cast==delayed.Cast), "Same cast did not hit after delay expiry");
                });
                Later(1100,"chain-lightning-result",()=>
                {
                    Require(!ChainLightning.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Uncollided segment did not expire at its source lifetime");
                    Seed(owner,target);
                });
            });
        });
    }
    private static void Seed(PlayerMobile owner, GraveyardSkeleton first)
    {
        var next=Enemy(owner,4); Cast(owner); var at=TimedEffects.Now.AddMinutes(10);
        // Test-only held phase/damage; normal cost, rank, fresh child snapshots and travel are checked separately.
        var held=Flight(owner) with { Rank=1,Remaining=1,Frame=3,AimTarget=next.Serial.Value,X=owner.X+1.5,Y=owner.Y,
            Vx=.703125,Vy=0,Min256=2560,Max256=2560,NextUtc=at,UntilUtc=at.AddSeconds(1),Delays=[new(first.Serial.Value,at.AddMilliseconds(160))] };
        Replace(held); File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,first.Serial.Value,next.Serial.Value,held)));
        _log.Information("chain-lightning-result: passed");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var first=World.FindMobile((Serial)saved.First) as BaseCreature; var next=World.FindMobile((Serial)saved.Next) as BaseCreature;
        Require(owner!=null && first is { Alive:true } && next is { Alive:true },"Saved chain owner/targets missing");
        var snapshot=ChainLightning.Snapshot(); var held=snapshot.Single(x=>x.Cast==saved.Flight.Cast);
        Require(JsonSerializer.Serialize(held)==JsonSerializer.Serialize(saved.Flight),"Saved chain rank/count/vector/damage/phase/cast delay/deadlines changed");
        owner.MoveToWorld(new((int)(held.X-1.5),(int)held.Y,held.Z),Map.Maps[held.Map]);
        first.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map); next.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        first.Frozen=next.Frozen=true; first.CantWalk=next.CantWalk=true; first.AIObject.AITimer.Stop(); next.AIObject.AITimer.Stop();
        first.SetResistance(ResistanceType.Energy,0); next.SetResistance(ResistanceType.Energy,0); int a=first.Hits,b=next.Hits; var at=TimedEffects.Now;
        var copy=held with { Cast=snapshot.Max(x=>x.Cast)+1, NextUtc=at,UntilUtc=at.AddSeconds(1),Delays=[new(first.Serial.Value,at.AddMilliseconds(160))] };
        ChainLightning.Restore(snapshot.Append(copy).ToArray());
        Later(300,"chain-lightning-reload-result",()=>
        {
            Require(first.Hits==a && next.Hits==b-10 && !ChainLightning.Snapshot().Any(x=>x.Cast==copy.Cast)
                && JsonSerializer.Serialize(ChainLightning.Snapshot().Single(x=>x.Cast==held.Cast))==JsonSerializer.Serialize(held),
                "Saved delay phase did not resume exclusion, real damage or finite termination");
            _log.Information("chain-lightning-reload-result: passed");
        });
    }
}
