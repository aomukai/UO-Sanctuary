// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Mobiles;
using Server.Logging;
using Server.Spells;

namespace Sanctuary;

internal sealed record ChainTrace(double X,double Y,SourceStepTrace Steps,Mobile First,bool Removed);

internal sealed record ChainPlayerPacket(FirestormModifiers Modifiers,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,int VelocityFp8);

public sealed record ChainDelay(uint Target, DateTime UntilUtc);
public sealed record LightningStrikeChainContext(uint OpeningPrimary)
{public string Profile{get;init;}="sanctuary-lightning-strike-opening-v1";}
public sealed record ChainFlight(long Cast, uint Owner, int Map, int Rank, int Remaining, uint AimTarget,
    double X, double Y, int Z, double Vx, double Vy, int Frame, int Min256, int Max256,
    ChainDelay[] Delays, DateTime UntilUtc, DateTime NextUtc)
{
    // Optional fields keep historical player-flight JSON and persistence v0.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonInclude]
    internal ChainPlayerPacket PlayerPacket{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LightningStrikeChainContext StrikeContext{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonInclude]
    internal NpcItemChainPacket OriginalNpc{get;init;}
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
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChainLightning));
    internal static Func<NpcChainRequest,NpcChainAllocation> OriginalNpcAllocator;
    private static readonly Dictionary<long, ChainFlight> _flights = new();
    private static readonly List<long> _keys = new();
    private static long _nextCast;
    private static readonly HashSet<long> Loaded=new();
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static ChainLightningProfiles Profiles { get; private set; }
    public static int Frames => NecromancerSummons.Source("chainlightning", "Range");
    public static int DelayFrames => NecromancerSummons.Source("chainlightning", "NextDelay");
    internal static CombatAbility Ability(ChainFlight f)=>f.StrikeContext==null?CombatAbility.ChainLightning:CombatAbility.LightningStrike;
    private static string Missile(ChainFlight f)=>f.StrikeContext==null?"chainlightning":"lightningstrike";
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    private static (int Min, int Max) Damage(PlayerMobile owner, SkillValues values)
    {
        int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int damage) => (int)Math.Clamp(NamedElements.Enhance(owner, damage * (100 + potion) / 100.0, "ltng"), 0, int.MaxValue);
        return (Scale(values.Damage256(false)), Scale(values.Damage256(true)));
    }
    internal static ChainFlight PreparePlayer(PlayerMobile owner,Point3D aim,SkillValues values,DateTime at)
    {
        var row=SkillMechanics.For(CombatAbility.ChainLightning);int M(string key)=>NecromancerSummons.Source("chainlightning",key);
        if(values.Ability!=CombatAbility.ChainLightning||values.Rank is <1 or >255||row.SourceId!=53||row.Number("srvdofunc")!=26||row.Text("srvmissilea")!="chainlightning"
            ||row.Number("SrcDam")!=0||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=12||M("pSrvDmgFunc")!=0||M("Vel")!=30||M("MaxVel")!=30||M("Range")!=25
            ||M("Size")!=2||M("CollideType")!=3||M("CollideKill")!=1||M("LastCollide")!=1||M("NextHit")!=1||M("NextDelay")!=4||M("InitSteps")!=0||M("Activate")!=0
            ||M("CanSlow")!=1||M("SrcDamage")!=0||M("ToHit")!=0||M("Pierce")!=0||M("ReturnFire")!=1||M("GetHit")!=1||M("ResultFlags")!=4||M("HitFlags")!=0
            ||owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc
            ||aim.X<=0||aim.Y<=0||aim.X>=owner.Map.Width||aim.Y>=owner.Map.Height||SpellHelper.IsTown(owner.Location,owner)||SpellHelper.IsTown(aim,owner))
        {throw new NotSupportedException("Unmapped player Chain Lightning route/owner/aim");}
        var (damage,packet)=PlayerDamage(owner,values,new([new(owner.X*2,owner.Y*2)]));
        int dx=aim.X-owner.X,dy=aim.Y-owner.Y;if(dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,packet.VelocityFp8);
        return new(_nextCast+1,owner.Serial.Value,owner.Map.MapID,values.Rank,Math.Max(1,(int)values.Eval("calc1")),0,owner.X,owner.Y,owner.Z,vector.X,vector.Y,0,
            damage.Min,damage.Max,[],at.AddMilliseconds(Frames*40),at.AddMilliseconds(40)){PlayerPacket=packet};
    }
    private static ((int Min,int Max) Damage,ChainPlayerPacket Packet) PlayerDamage(PlayerMobile owner,SkillValues values,SourceStepTrace steps)
    {
        var slow=AmazonSlowMissiles.Capture(owner,true);var mods=DruidFirestorm.Modifiers(owner);var ends=DruidFirestorm.Endpoints(values,mods.LightningMastery,slow);
        int velocity=SlowMissilePayload.VelocityFp8(30,0,values.Rank,33,0,true,slow.VelocityPercent);
        return(ends,new(mods,slow,SourceRandom.InitLow(SeedLow()),steps,velocity));
    }
    internal static long CommitPlayer(ChainFlight flight)
    {
        ValidatePlayer(flight);if(flight.Cast!=_nextCast+1){throw new InvalidOperationException("Stale prepared chain");}
        _flights.Add(flight.Cast,Copy(flight));_nextCast=flight.Cast;return flight.Cast;
    }
    internal static ChainFlight PrepareOriginalNpc(SkeletonGuardian actor,Point3D aim,int rank,DateTime at,Func<NpcChainRequest,NpcChainAllocation> allocate)
        =>NpcItemChainFlights.Prepare(checked(_nextCast+1),actor,aim,rank,at,allocate);
    internal static long CommitValkyrie(ChainFlight flight)
    {
        NpcItemChainFlights.Validate(flight);if(flight.Cast!=_nextCast+1){throw new InvalidOperationException("Stale original NPC chain");}
        _flights.Add(flight.Cast,Copy(flight));_nextCast=flight.Cast;return flight.Cast;
    }
    private sealed class ItemRoot(ChainFlight candidate)
    {
        // This handle is not published or serialized with budget zero.
        internal ChainFlight Flight=candidate with{Remaining=0};
        internal bool BudgetSet;
    }
    internal static NpcItemSpellEmission EmitOriginalItem(SkeletonGuardian body,NpcItemTarget target,Point3D path,int rank,int outgoing,
        DateTime at,Func<NpcItemMissileRequest,NpcChainAllocation> allocate,bool serverDo=false,Action<bool> serverDoChanged=null)
    {
        ArgumentNullException.ThrowIfNull(allocate);var owner=body?.ControlMaster as PlayerMobile;
        if(!NpcItemSpellFlights.Attached(body,owner,body?.Map?.MapID??-1)||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-2000))
        {throw new NotSupportedException("Original item chain needs its active source and UTC factory frame");}
        var point=target?.Point(body.Map);long cast=checked(_nextCast+1);int attempts=0;ItemRoot root=null;
        var frame=new NpcItemSpellOrigins((body.X*2,body.Y*2),point is{} p?(p.X*2,p.Y*2):null,(path.X*2,path.Y*2),OriginalNpcEquipment.Graph(body).Read(19),serverDo,request=>
        {
            if(++attempts!=1){throw new NotSupportedException("Original item chain requested multiple roots");}
            var candidate=NpcItemChainFlights.PrepareFactory(cast,body,request,at);if(candidate==null){return null;}
            var allocation=allocate(request);if(!allocation.Allocated){return null;}
            root=new(candidate with{OriginalNpc=candidate.OriginalNpc with{Seed=allocation.Seed}});return root;
        },(handle,count)=>
        {
            if(handle is not ItemRoot allocated||!ReferenceEquals(allocated,root)||allocated.BudgetSet||allocated.Flight.Remaining!=0
                ||count!=allocated.Flight.OriginalNpc.Source.Packet.Emissions)
            {throw new NotSupportedException("Original item chain budget lost its actual allocation handle/order");}
            allocated.Flight=allocated.Flight with{Remaining=count};NpcItemChainFlights.Validate(allocated.Flight);allocated.BudgetSet=true;
        },serverDoChanged:serverDoChanged);
        int result=frame.Run(53,rank,outgoing);
        if(result==0)
        {
            if(root!=null){throw new InvalidOperationException("Failed item chain retained an allocated root");}
            return new(0,attempts,[]);
        }
        if(root is not{BudgetSet:true}){throw new InvalidOperationException("Item chain succeeded without its root budget");}
        return new(result,attempts,[CommitValkyrie(root.Flight)]);
    }
    public static void Cast(PlayerMobile owner,Point3D aim,SkillValues values)=>CommitPlayer(PreparePlayer(owner,aim,values,TimedEffects.Now));
    internal static void RequireLightningStrikeRoute()
    {
        var row=SkillMechanics.For(CombatAbility.LightningStrike);int M(string key)=>NecromancerSummons.Source("lightningstrike",key);
        if(row.SourceId!=34||row.Number("srvstfunc")!=10||row.Number("srvdofunc")!=14||row.Text("srvmissilea")!="lightningstrike"||row.Number("SrcDam")!=0
            ||row.Number("Param1")!=20||row.Number("Param3")!=2||row.Number("Param4")!=1||row.Number("calc1")!=20
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=12||M("pSrvDmgFunc")!=0||M("Vel")!=30||M("MaxVel")!=30||M("Range")!=25
            ||M("Size")!=2||M("CollideType")!=3||M("CollideKill")!=1||M("LastCollide")!=1||M("NextHit")!=1||M("NextDelay")!=4
            ||M("InitSteps")!=0||M("Activate")!=0||M("CanSlow")!=1||M("SrcDamage")!=0||M("ToHit")!=0||M("Pierce")!=0)
        {throw new NotSupportedException("Unmapped Lightning Strike chain route");}
    }
    internal static long CastLightningStrike(PlayerMobile owner,Mobile primary,Point3D origin,SkillValues values,DateTime at)
    {
        RequireLightningStrikeRoute();
        if(values.Ability!=CombatAbility.LightningStrike||values.Rank is <1 or >255||primary==null||owner is not{Alive:true,Deleted:false}
            ||ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true||profile.Class!=DiabloClass.Amazon
            ||owner.Map==null||owner.Map==Map.Internal||SpellHelper.IsTown(owner.Location,owner)||SpellHelper.IsTown(origin,owner)){return 0;}
        var flight=new ChainFlight(_nextCast+1,owner.Serial.Value,owner.Map.MapID,values.Rank,checked((int)values.Eval("calc2")),0,
            origin.X,origin.Y,origin.Z,0,0,0,0,0,[],at.AddMilliseconds(Frames*40),at.AddMilliseconds(40))
            {StrikeContext=new(primary.Serial.Value)};
        // Approved Sanctuary opening: begin at the struck monster, choose a
        // different nearby enemy and skip the primary until first contact.
        // No hidden first bolt/extra primary damage is simulated.
        var next=NextTarget(owner,owner,flight,primary,origin.X,origin.Y,at);if(next==null){return 0;}
        var (damage,packet)=PlayerDamage(owner,values,new([new(origin.X*2,origin.Y*2)]));
        int dx=next.X-origin.X,dy=next.Y-origin.Y;if(dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,packet.VelocityFp8);
        return CommitPlayer(flight with{AimTarget=next.Serial.Value,Vx=vector.X,Vy=vector.Y,Min256=damage.Min,Max256=damage.Max,PlayerPacket=packet});
    }
    internal static bool Release(PlayerMobile owner,Point3D aim,SkillValues values)
    {
        var flight=PreparePlayer(owner,aim,values,TimedEffects.Now);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        CommitPlayer(flight);owner.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
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
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == (flight.NpcCaster!=0?DiabloClass.Necromancer:flight.StrikeContext!=null?DiabloClass.Amazon:DiabloClass.Sorceress)
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
    internal static Mobile NextTarget(Mobile caster,PlayerMobile owner, ChainFlight flight, Mobile hit, double x, double y, DateTime at)
    {
        double radius = SkillGeometry.Tiles(SkillMechanics.For(Ability(flight)).Parameter(1));
        var point = new Point3D((int)Math.Round(x), (int)Math.Round(y), flight.Z);
        Mobile smallest = null, greater = null;
        foreach (var target in caster.Map.GetMobilesInRange<Mobile>(point, (int)Math.Ceiling(radius)))
        {
            double dx = target.X - x, dy = target.Y - y;
            // Shared hit12 uses filter0x88583, without optional caster LOS.
            // Segment terrain is still checked by its ordinary travel path.
            if (target == hit || dx * dx + dy * dy > radius * radius || Math.Abs(target.Z - flight.Z) >= 16
                || Delayed(flight, target.Serial.Value, at) || !ClassCombat.IsHostile(owner, target)
                ||flight.OriginalNpc!=null&&(ClassCombat.Friendly(caster,target)||!caster.CanBeHarmful(target,false))
                || SpellHelper.IsTown(target.Location, owner)) { continue; }
            if (smallest == null || target.Serial.Value < smallest.Serial.Value) { smallest = target; }
            if (target.Serial.Value > hit.Serial.Value && (greater == null || target.Serial.Value < greater.Serial.Value)) { greater = target; }
        }
        return greater ?? smallest;
    }
    private static void Remove(long id){_flights.Remove(id);Loaded.Remove(id);}
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>(); foreach (var flight in _flights.Values) { if (flight.Owner == owner.Serial.Value||flight.NpcCaster==owner.Serial.Value||flight.OriginalNpc?.Source.Actor==owner.Serial.Value) { ids.Add(flight.Cast); } }
        foreach (long id in ids) { Remove(id); }
    }
    private static void Tick()=>Tick(TimedEffects.Now);
    internal static void Tick(DateTime now,bool? npcActions=null,long? onlyCast=null,Func<NpcChainRequest,NpcChainAllocation> originalAllocate=null)
    {
        if(now.Kind!=DateTimeKind.Utc){throw new ArgumentException("Chain clock needs UTC");}
        _keys.Clear(); _keys.AddRange(_flights.Keys);
        foreach (long id in _keys)
        {
            if(onlyCast.HasValue&&id!=onlyCast.Value){continue;}
            if (!_flights.TryGetValue(id, out var flight)) { continue; }
            if(flight.OriginalNpc!=null)
            {
                bool loaded=flight.NextUtc<=now&&Loaded.Remove(id);
                try
                {
                    var npcNext=NpcItemChainFlights.Advance(flight,now,npcActions,loaded,originalAllocate??OriginalNpcAllocator);
                    if(npcNext==null){Remove(id);}else{_flights[id]=Copy(npcNext);}
                }
                catch(NotSupportedException e)
                {
                    Log.Error(e,"Original NPC chain suspended: {Cast}",id);var suspended=NpcItemChainFlights.Suspend(flight,e.Message);
                    if(suspended==null){Remove(id);}else{_flights[id]=Copy(suspended);}
                }
                continue;
            }
            var owner = Owner(flight);
            if(flight.UntilUtc<=now){Remove(id);continue;}
            var npc=flight.NpcCaster==0?null:World.FindMobile((Serial)flight.NpcCaster) as NecromancerMinion;
            Mobile caster=flight.NpcCaster==0?owner:npc;
            if(flight.NpcCaster!=0&&!ActiveNpc(flight,npc,owner,npcActions))
            {
                // Offline/parked/invalid NPC sources do not fight or replay
                // skipped travel, while their original lifetime still expires.
                if(flight.NextUtc<=now){if(flight.Frame+1>=Frames){Remove(id);}else{_flights[id]=flight with{Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40)};}}continue;
            }
            if(owner==null){Remove(id);continue;}
            if(flight.PlayerPacket!=null)
            {
                if(flight.NextUtc>now){continue;}
                bool loaded=Loaded.Remove(id);
                if(loaded||owner.Map==Map.Internal||!(npcActions??owner.NetState is{Running:true}))
                {
                    int elapsed=loaded?(int)Math.Min(Frames-flight.Frame,(now-flight.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
                    if(flight.Frame+elapsed>=Frames){Remove(id);}else{_flights[id]=flight with{Frame=flight.Frame+elapsed,NextUtc=flight.NextUtc.AddMilliseconds(40L*elapsed)};}continue;
                }
            }
            if(owner.Map==Map.Internal||flight.NextUtc>now){continue;}
            double x=flight.X,y=flight.Y;Mobile first=null;
            if(flight.PlayerPacket is{} prepared)
            {
                var moved=Trace(flight,caster,owner,prepared.Steps,prepared.VelocityFp8);x=moved.X;y=moved.Y;first=moved.First;
                flight=flight with{PlayerPacket=prepared with{Steps=moved.Steps}};
                if(moved.Removed){Remove(id);continue;}
            }
            else
            {
                x=flight.X+flight.Vx;y=flight.Y+flight.Vy;
            var from = new Point3D((int)Math.Round(flight.X), (int)Math.Round(flight.Y), flight.Z + 12);
            var legacyTo = new Point3D((int)Math.Round(x), (int)Math.Round(y), flight.Z + 12);
            if (x < 0 || y < 0 || x >= caster.Map.Width || y >= caster.Map.Height || !caster.Map.LineOfSight(from, legacyTo) || flight.Frame + 1 >= Frames)
            { Remove(id); continue; }
                double nearest=double.MaxValue;
            foreach (var target in SkillCombat.Targets(caster,owner, new(legacyTo.X, legacyTo.Y, flight.Z), 2))
            {
                if (!Delayed(flight, target.Serial.Value, flight.NextUtc)
                    && CombatHitboxes.Intersect(flight.X, flight.Y, x, y, flight.Z, target.Location, out double entry) && entry < nearest)
                { first = target; nearest = entry; }
            }
            }
            var to = new Point3D((int)Math.Round(x),(int)Math.Round(y),flight.Z+12);
            Effects.SendLocationEffect(new(to.X, to.Y, flight.Z), caster.Map, 0x3818, 3, 10, 0x482, 0); // Placeholder chain segment.
            if (first == null)
            { _flights[id] = flight with { X = x, Y = y, Frame = flight.Frame + 1, NextUtc = flight.NextUtc.AddMilliseconds(40) }; continue; }
            var delays = new List<ChainDelay>();
            foreach (var delay in flight.Delays) { if (delay.UntilUtc > flight.NextUtc) { delays.Add(delay); } }
            delays.Add(new(first.Serial.Value, flight.NextUtc.AddMilliseconds(DelayFrames * 40)));
            flight = flight with { Delays = delays.ToArray() };
            var next = flight.Remaining > 1 ? NextTarget(caster,owner, flight, first, x, y, flight.NextUtc) : null;
            if (next == null) { Remove(id); }
            else
            {
                int dx = (int)Math.Round((next.X - x) * 2), dy = (int)Math.Round((next.Y - y) * 2);
                if (dx == 0 && dy == 0) { dx = dy = 1; }
                var slow=npc==null?SlowMissilePayload.Normal:AmazonSlowMissiles.Capture(npc,true);
                var values=npc==null?SkillMechanics.Values(owner,Ability(flight),flight.Rank):MagePetBuffs.Values(CombatAbility.ChainLightning,flight.Rank,npc.Level);
                var packet=flight.PlayerPacket;(int Min,int Max) damage;
                (double X,double Y) velocity;
                if(packet!=null)
                {
                    (damage,packet)=PlayerDamage(owner,values,new([new((int)Math.Round(x*2),(int)Math.Round(y*2))]));
                    velocity=MissileVectors.VelocityFp8(dx,dy,packet.VelocityFp8);
                }
                else{damage=npc==null?Damage(owner,values):(slow.Damage256(values.Damage256(false)),slow.Damage256(values.Damage256(true)));velocity=npc==null?MissileVectors.Velocity(dx,dy,NecromancerSummons.Source("chainlightning","Vel")):NpcVelocity(dx,dy,flight.Rank,slow);}
                _flights[id]=flight with{Remaining=flight.Remaining-1,AimTarget=next.Serial.Value,X=x,Y=y,Vx=velocity.X,Vy=velocity.Y,Frame=0,Min256=damage.Min,Max256=damage.Max,
                    UntilUtc=flight.NextUtc.AddMilliseconds(Frames*40),NextUtc=flight.NextUtc.AddMilliseconds(40),NpcSlow=slow,
                    NpcSeed=npc==null?0:SourceRandom.InitLow(NecromancerMageMissiles.SeedLow()),PlayerPacket=packet,
                    StrikeContext=flight.StrikeContext==null?null:new(0)};

            }
            if(npc!=null)
            {NecromancerMageMissiles.ImpactSpell(npc,first,new("chainlightning","ltng",flight.Rank,flight.Min256,flight.Max256,0),flight.Owner,flight.NpcSeed,flight.NextUtc);}
            else if(flight.PlayerPacket is{} playerPacket)
            {
                int raw=SorceressSpellMissiles.Roll(flight.Min256,flight.Max256,playerPacket.Seed,out _);
                SorceressSpellMissiles.ImpactPrepared(owner,first,Missile(flight),"ltng",raw,playerPacket.Modifiers,flight.NextUtc);
            }
            else
            {
                SorceressEffects.MissileHit(owner,first,"chainlightning");
                int rolled=flight.Max256<=flight.Min256?flight.Min256:Utility.RandomMinMax(flight.Min256,flight.Max256-1);
                SkillCombat.LightningImpact(owner,first,rolled);
            }
        }
    }
    internal static ChainTrace Trace(ChainFlight flight,Mobile caster,PlayerMobile owner,SourceStepTrace captured,int velocity)
    {
        double x=flight.X,y=flight.Y;var steps=captured;Mobile first=null;
        int Move(){x+=flight.Vx;y+=flight.Vy;steps=SourceMissileSteps.Advance(flight.X,flight.Y,flight.Vx,flight.Vy,captured);return x<0||y<0||x>=caster.Map.Width||y>=caster.Map.Height?2:1;}
        bool Wall(SourceMissileCell cell)=>DruidFirestorm.CellBlocked(caster.Map,flight.Z,flight.X,flight.Y,cell.X,cell.Y);
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var t in caster.Map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,flight.Z),3))
            {
                if(t is not{Alive:true,Deleted:false,Blessed:false}||t.Serial.Value==flight.StrikeContext?.OpeningPrimary||Math.Abs(t.Z-flight.Z)>=16||Delayed(flight,t.Serial.Value,flight.NextUtc)
                    ||!ClassCombat.IsHostile(owner,t)||!caster.CanBeHarmful(t,false)||flight.OriginalNpc!=null&&ClassCombat.Friendly(caster,t)||SpellHelper.IsTown(t.Location,owner)){continue;}
                int size=t is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(2,size,cell.X-t.X*2,cell.Y-t.Y*2)){return t;}
            }return null;
        }
        var update=BasicMissileUpdate.Run(new(true,true,velocity,Frames-flight.Frame,2,Frames,true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>steps.Cells,cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Wall(cell)?0:Find(cell)?.Serial.Value??0,
                (serial,terminal)=>{if(!terminal){first=World.FindMobile((Serial)serial);}return 2;}));
        return new(x,y,steps,first,update.Result==2&&first==null);
    }
    private static ChainFlight Copy(ChainFlight f)=>f with{Delays=f.Delays.ToArray(),PlayerPacket=f.PlayerPacket is{} p?p with{Steps=new(p.Steps.Cells.ToArray())}:null,OriginalNpc=f.OriginalNpc is{} n?n with{Steps=new(n.Steps.Cells.ToArray())}:null};
    public static ChainFlight[] Snapshot()=>_flights.Values.OrderBy(x=>x.Cast).Select(Copy).ToArray();
    private static void ValidatePlayer(ChainFlight f)
    {
        if(f.Cast<=0||f.Frame<0||f.Frame>=Frames||f.Min256<0||f.Max256<f.Min256||!double.IsFinite(f.X+f.Y+f.Vx+f.Vy)||f.PlayerPacket is not{} p||f.OriginalNpc!=null||f.NpcCaster!=0||!((Serial)f.Owner).IsMobile||f.Rank is <1 or >255||f.Remaining<1||f.Remaining>(f.StrikeContext==null?(f.Rank+25)/5:f.Rank+1)
            ||f.StrikeContext is{OpeningPrimary:not 0} strike&&(!((Serial)strike.OpeningPrimary).IsMobile||strike.OpeningPrimary==f.Owner)
            ||f.StrikeContext is{} context&&context.Profile!="sanctuary-lightning-strike-opening-v1"
            ||f.Map<0||f.Map>=Map.Maps.Length||Map.Maps[f.Map]==null||Map.Maps[f.Map]==Map.Internal||f.Z is <-128 or >127
            ||f.X<=0||f.Y<=0||f.X>=Map.Maps[f.Map].Width||f.Y>=Map.Maps[f.Map].Height||Math.Abs(f.Vx)>.703125||Math.Abs(f.Vy)>.703125
            ||f.NextUtc.Kind!=DateTimeKind.Utc||f.UntilUtc.Kind!=DateTimeKind.Utc||p.Modifiers==null||p.VelocityFp8 is <0 or >5760
            ||p.Slow.VelocityPercent is <0 or >100||p.Slow.DamagePercent is <0 or >100||p.Steps?.Cells is not{Length:>=1 and <=10}
            ||p.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[f.Map].Width*2||c.Y>=Map.Maps[f.Map].Height*2)
            ||f.Delays.Any(d=>!((Serial)d.Target).IsMobile||d.UntilUtc.Kind!=DateTimeKind.Utc))
        {throw new InvalidDataException("Invalid prepared player chain");}
    }
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
                ||flight.StrikeContext!=null&&(flight.NpcCaster!=0||flight.PlayerPacket==null)
                ||flight.NpcCaster==0&&flight.NpcSeed!=0||!restored.TryAdd(flight.Cast,flight))
            { throw new InvalidDataException("Invalid Chain Lightning flight"); }
            if(flight.PlayerPacket!=null){ValidatePlayer(flight);}
            if(flight.OriginalNpc!=null){NpcItemChainFlights.Validate(flight);}
            var targets = new HashSet<uint>();
            foreach (var delay in flight.Delays)
            { if (delay.Target == 0 || !targets.Add(delay.Target)) { throw new InvalidDataException("Invalid Chain Lightning delay"); } }
            nextCast=Math.Max(nextCast,flight.Cast);
        }
        _flights.Clear();foreach(var pair in restored){_flights.Add(pair.Key,Copy(pair.Value));}_nextCast=nextCast;Loaded.Clear();foreach(var f in restored.Values.Where(f=>f.PlayerPacket!=null||f.OriginalNpc!=null)){Loaded.Add(f.Cast);}
    }
}
