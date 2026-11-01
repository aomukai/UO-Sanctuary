// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ChainDelay(uint Target, DateTime UntilUtc);
public sealed record ChainFlight(long Cast, uint Owner, int Map, int Rank, int Remaining, uint AimTarget,
    double X, double Y, int Z, double Vx, double Vy, int Frame, int Min256, int Max256,
    ChainDelay[] Delays, DateTime UntilUtc, DateTime NextUtc)
{
    // Optional fields keep historical player-flight JSON and persistence v0.
    public uint NpcCaster{get;init;}
    public ulong NpcSeed{get;init;}
    public SlowMissilePayload NpcSlow{get;init;}=SlowMissilePayload.Normal;
}

public sealed class ChainLightningProfiles : GenericPersistence
{
    public ChainLightningProfiles() : base("SanctuaryChainLightning", 120) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(ChainLightning.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Chain Lightning version"); }
        ChainLightning.Restore(JsonSerializer.Deserialize<ChainFlight[]>(reader.ReadString()));
    }
}

public static class ChainLightning
{
    private static readonly Dictionary<long, ChainFlight> _flights = new();
    private static readonly List<long> _keys = new();
    private static long _nextCast;
    public static ChainLightningProfiles Profiles { get; private set; }
    public static int Frames => NecromancerSummons.Source("chainlightning", "Range");
    public static int DelayFrames => NecromancerSummons.Source("chainlightning", "NextDelay");
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    private static (int Min, int Max) Damage(PlayerMobile owner, SkillValues values)
    {
        int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int damage) => (int)Math.Clamp(NamedElements.Enhance(owner, damage * (100 + potion) / 100.0, "ltng"), 0, int.MaxValue);
        return (Scale(values.Damage256(false)), Scale(values.Damage256(true)));
    }
    public static void Cast(PlayerMobile owner, Point3D aim, SkillValues values)
        =>Launch(owner,owner,aim,values,Damage(owner,values),SlowMissilePayload.Normal,TimedEffects.Now,false);
    internal static long CastNpc(NecromancerMinion actor,Point3D aim,SkillValues values,DateTime at)
    {
        if(actor is not{Alive:true,Deleted:false,IsMage:true,ControlMaster:PlayerMobile owner}||actor.Map==null||actor.Map==Map.Internal||owner.Map!=actor.Map
            ||values.Ability!=CombatAbility.ChainLightning||values.Rank!=10||at.Kind!=DateTimeKind.Utc){return 0;}
        var row=SkillMechanics.For(CombatAbility.ChainLightning);
        if(row.Number("localdelay")!=0||row.Number("srvdofunc")!=26||NecromancerSummons.Source("chainlightning","pSrvHitFunc")!=12)
        {throw new NotSupportedException("Unmapped NPC Chain Lightning route/cooldown");}
        var slow=AmazonSlowMissiles.Capture(actor,true);
        if(NecromancerMageMissiles.ActorSeed(actor)==null){NecromancerMageMissiles.SetActorSeed(actor,SourceRandom.InitLow(NecromancerMageMissiles.SeedLow()));}
        return Launch(actor,owner,aim,values,(slow.Damage256(values.Damage256(false)),slow.Damage256(values.Damage256(true))),slow,at,true);
    }
    private static long Launch(Mobile caster,PlayerMobile owner,Point3D aim,SkillValues values,(int Min,int Max) damage,SlowMissilePayload slow,DateTime at,bool npc)
    {
        int dx=aim.X-caster.X,dy=aim.Y-caster.Y;if(dx==0&&dy==0){dx=dy=1;}
        var velocity=npc?NpcVelocity(dx,dy,values.Rank,slow):MissileVectors.Velocity(dx,dy,NecromancerSummons.Source("chainlightning","Vel"));
        long id=++_nextCast;
        _flights.Add(id,new(id,owner.Serial.Value,caster.Map.MapID,values.Rank,Math.Max(1,(int)values.Eval("calc1")),0,
            caster.X,caster.Y,caster.Z,velocity.X,velocity.Y,0,damage.Min,damage.Max,[],at.AddMilliseconds(Frames*40),at)
            {NpcCaster=npc?caster.Serial.Value:0,NpcSeed=npc?SourceRandom.InitLow(NecromancerMageMissiles.SeedLow()):0,NpcSlow=slow});return id;
    }
    private static (double X,double Y) NpcVelocity(int dx,int dy,int rank,SlowMissilePayload slow)
        =>MissileVectors.VelocityFp8(dx,dy,SlowMissilePayload.VelocityFp8(NecromancerSummons.Source("chainlightning","Vel"),0,rank,0x21,0,true,slow.VelocityPercent));
    private static PlayerMobile Owner(ChainFlight flight) => World.FindMobile((Serial)flight.Owner) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == (flight.NpcCaster==0?DiabloClass.Sorceress:DiabloClass.Necromancer)
        && (player.Map == Map.Internal || player.Map.MapID == flight.Map && !SpellHelper.IsTown(player.Location, player)) ? player : null;
    private static bool ActiveNpc(ChainFlight flight,NecromancerMinion actor,PlayerMobile owner,bool? actions)
        =>actor is{Alive:true,Deleted:false,IsMage:true}&&owner!=null&&actor.ControlMaster==owner&&actor.Map==owner.Map&&actor.Map?.MapID==flight.Map
            &&actor.Permitted(owner)&&PersistentMinions.Active(owner,actor)&&!SpellHelper.IsTown(actor.Location,actor)
            &&NecromancerMageMissiles.ActorSeed(actor) is{} seed&&seed.Owner==flight.Owner
            &&(actions??owner.NetState is{Running:true});
    public static bool Delayed(ChainFlight flight, uint target, DateTime at)
    {
        foreach (var delay in flight.Delays) { if (delay.Target == target && delay.UntilUtc > at) { return true; } }
        return false;
    }
    private static Mobile NextTarget(Mobile caster,PlayerMobile owner, ChainFlight flight, Mobile hit, double x, double y, DateTime at)
    {
        double radius = SkillGeometry.Tiles(SkillMechanics.For(CombatAbility.ChainLightning).Parameter(1));
        var point = new Point3D((int)Math.Round(x), (int)Math.Round(y), flight.Z);
        Mobile smallest = null, greater = null;
        foreach (var target in caster.Map.GetMobilesInRange<Mobile>(point, (int)Math.Ceiling(radius)))
        {
            double dx = target.X - x, dy = target.Y - y;
            // Aura filter 0x8A783 traces from the caster. Segment terrain is checked during travel.
            if (target == hit || dx * dx + dy * dy > radius * radius || Math.Abs(target.Z - flight.Z) >= 16
                || Delayed(flight, target.Serial.Value, at) || !ClassCombat.IsHostile(owner, target)
                || SpellHelper.IsTown(target.Location, owner) || !caster.Map.LineOfSight(new Point3D(caster.X, caster.Y, caster.Z + 12), new Point3D(target.X, target.Y, target.Z + 12))) { continue; }
            if (smallest == null || target.Serial.Value < smallest.Serial.Value) { smallest = target; }
            if (target.Serial.Value > hit.Serial.Value && (greater == null || target.Serial.Value < greater.Serial.Value)) { greater = target; }
        }
        return greater ?? smallest;
    }
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>(); foreach (var flight in _flights.Values) { if (flight.Owner == owner.Serial.Value||flight.NpcCaster==owner.Serial.Value) { ids.Add(flight.Cast); } }
        foreach (long id in ids) { _flights.Remove(id); }
    }
    private static void Tick()=>Tick(TimedEffects.Now);
    internal static void Tick(DateTime now,bool? npcActions=null,long? onlyCast=null)
    {
        if(now.Kind!=DateTimeKind.Utc){throw new ArgumentException("Chain clock needs UTC");}
        _keys.Clear(); _keys.AddRange(_flights.Keys);
        foreach (long id in _keys)
        {
            if(onlyCast.HasValue&&id!=onlyCast.Value){continue;}
            if (!_flights.TryGetValue(id, out var flight)) { continue; }
            var owner = Owner(flight);
            if(flight.UntilUtc<=now){_flights.Remove(id);continue;}
            var npc=flight.NpcCaster==0?null:World.FindMobile((Serial)flight.NpcCaster) as NecromancerMinion;
            Mobile caster=flight.NpcCaster==0?owner:npc;
            if(flight.NpcCaster!=0&&!ActiveNpc(flight,npc,owner,npcActions))
            {
                // Offline/parked/invalid NPC sources do not fight or replay
                // skipped travel, while their original lifetime still expires.
                if(flight.NextUtc<=now){if(flight.Frame+1>=Frames){_flights.Remove(id);}else{_flights[id]=flight with{Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40)};}}continue;
            }
            if(owner==null){_flights.Remove(id);continue;}
            if(owner.Map==Map.Internal||flight.NextUtc>now){continue;}
            double x = flight.X + flight.Vx, y = flight.Y + flight.Vy;
            var from = new Point3D((int)Math.Round(flight.X), (int)Math.Round(flight.Y), flight.Z + 12);
            var to = new Point3D((int)Math.Round(x), (int)Math.Round(y), flight.Z + 12);
            if (x < 0 || y < 0 || x >= caster.Map.Width || y >= caster.Map.Height || !caster.Map.LineOfSight(from, to) || flight.Frame + 1 >= Frames)
            { _flights.Remove(id); continue; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var target in SkillCombat.Targets(caster,owner, new(to.X, to.Y, flight.Z), 2))
            {
                if (!Delayed(flight, target.Serial.Value, flight.NextUtc)
                    && CombatHitboxes.Intersect(flight.X, flight.Y, x, y, flight.Z, target.Location, out double entry) && entry < nearest)
                { first = target; nearest = entry; }
            }
            Effects.SendLocationEffect(new(to.X, to.Y, flight.Z), caster.Map, 0x3818, 3, 10, 0x482, 0); // Placeholder chain segment.
            if (first == null)
            { _flights[id] = flight with { X = x, Y = y, Frame = flight.Frame + 1, NextUtc = flight.NextUtc.AddMilliseconds(40) }; continue; }
            var delays = new List<ChainDelay>();
            foreach (var delay in flight.Delays) { if (delay.UntilUtc > flight.NextUtc) { delays.Add(delay); } }
            delays.Add(new(first.Serial.Value, flight.NextUtc.AddMilliseconds(DelayFrames * 40)));
            flight = flight with { Delays = delays.ToArray() };
            var next = flight.Remaining > 1 ? NextTarget(caster,owner, flight, first, x, y, flight.NextUtc) : null;
            if (next == null) { _flights.Remove(id); }
            else
            {
                int dx = (int)Math.Round((next.X - x) * 2), dy = (int)Math.Round((next.Y - y) * 2);
                if (dx == 0 && dy == 0) { dx = dy = 1; }
                var slow=npc==null?SlowMissilePayload.Normal:AmazonSlowMissiles.Capture(npc,true);
                var velocity=npc==null?MissileVectors.Velocity(dx,dy,NecromancerSummons.Source("chainlightning","Vel")):NpcVelocity(dx,dy,flight.Rank,slow);
                // Player children retain live player synergies/mastery. NPC
                // children retain fixed rank and their own current slow state.
                var values=npc==null?SkillMechanics.Values(owner,CombatAbility.ChainLightning,flight.Rank):MagePetBuffs.Values(CombatAbility.ChainLightning,flight.Rank,npc.Level);
                var damage=npc==null?Damage(owner,values):(Min:slow.Damage256(values.Damage256(false)),Max:slow.Damage256(values.Damage256(true)));
                _flights[id] = flight with { Remaining = flight.Remaining - 1, AimTarget = next.Serial.Value, X = x, Y = y,
                    Vx = velocity.X, Vy = velocity.Y, Frame = 0, Min256 = damage.Min, Max256 = damage.Max,
                    UntilUtc = flight.NextUtc.AddMilliseconds(Frames * 40), NextUtc = flight.NextUtc.AddMilliseconds(40),NpcSlow=slow,
                    NpcSeed=npc==null?0:SourceRandom.InitLow(NecromancerMageMissiles.SeedLow()) };
            }
            if(npc!=null)
            {NecromancerMageMissiles.ImpactSpell(npc,first,new("chainlightning","ltng",flight.Rank,flight.Min256,flight.Max256,0),flight.Owner,flight.NpcSeed,flight.NextUtc);}
            else
            {
                SorceressEffects.MissileHit(owner,first,"chainlightning");
                int rolled=flight.Max256<=flight.Min256?flight.Min256:Utility.RandomMinMax(flight.Min256,flight.Max256-1);
                SkillCombat.LightningImpact(owner,first,rolled);
            }
        }
    }
    public static ChainFlight[] Snapshot() => new List<ChainFlight>(_flights.Values).ToArray();
    public static void Restore(ChainFlight[] saved)
    {
        if (saved == null) { throw new InvalidDataException("Missing Chain Lightning flights"); }
        var restored=new Dictionary<long,ChainFlight>();long nextCast=0;
        foreach (var flight in saved)
        {
            if (flight.Cast <= 0 || flight.Rank <= 0 || flight.Remaining <= 0 || flight.Frame < 0 || flight.Frame >= Frames
                || flight.Min256 < 0 || flight.Max256 < flight.Min256 || flight.Delays == null
                || !double.IsFinite(flight.X + flight.Y + flight.Vx + flight.Vy)
                ||flight.NpcCaster!=0&&(!((Serial)flight.NpcCaster).IsMobile||!((Serial)flight.Owner).IsMobile||flight.NpcCaster==flight.Owner
                    ||flight.Rank!=10||flight.Remaining>7||flight.NpcSlow.VelocityPercent is <0 or >100||flight.NpcSlow.DamagePercent is <0 or >100
                    ||flight.NextUtc.Kind!=DateTimeKind.Utc||flight.UntilUtc.Kind!=DateTimeKind.Utc||flight.Map<0||flight.Map>=Map.Maps.Length||Map.Maps[flight.Map] is not{} map||map==Map.Internal)
                ||flight.NpcCaster==0&&flight.NpcSeed!=0||!restored.TryAdd(flight.Cast,flight))
            { throw new InvalidDataException("Invalid Chain Lightning flight"); }
            var targets = new HashSet<uint>();
            foreach (var delay in flight.Delays)
            { if (delay.Target == 0 || !targets.Add(delay.Target)) { throw new InvalidDataException("Invalid Chain Lightning delay"); } }
            nextCast=Math.Max(nextCast,flight.Cast);
        }
        _flights.Clear();foreach(var pair in restored){_flights.Add(pair.Key,pair.Value);}_nextCast=nextCast;
    }
}
