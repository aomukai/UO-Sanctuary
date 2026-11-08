// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record SorceressChargedMotion(int OriginX,int OriginY,Point2D[] Path,int Next,int Ordinal)
{
    public bool Equals(SorceressChargedMotion other)=>other!=null&&OriginX==other.OriginX&&OriginY==other.OriginY
        &&Next==other.Next&&Ordinal==other.Ordinal&&Path.SequenceEqual(other.Path);
    public override int GetHashCode(){var h=new HashCode();h.Add(OriginX);h.Add(OriginY);h.Add(Next);h.Add(Ordinal);foreach(var p in Path){h.Add(p);}return h.ToHashCode();}
}
internal sealed record SorceressSpellFlight(long Id,uint Owner,int Map,int Z,CombatAbility Ability,int Rank,
    double X,double Y,double Vx,double Vy,int VelocityFp8,int Frame,int Frames,int Minimum256,int Maximum256,
    FirestormModifiers Modifiers,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,DateTime NextUtc,string Fault=null)
{
    // Optional V0 extension: historical Fire Bolt records retain zero length.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcItemSpellSource Npc{get;init;}
    public int ColdFrames{get;init;}
    // Optional V0 ring extension. Historical straight flights use defaults.
    public long Cast{get;init;}
    public int RadialIndex{get;init;}=-1;
    public int AccelerationCounter{get;init;}
    public uint LastCollision{get;init;}
    // Optional V0 extension; historical straight/radial flights have no path.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SorceressChargedMotion Charged{get;init;}
}
internal sealed record SorceressSpellSave(SorceressSpellFlight[] Flights,long NextId,WeaponFlightLease[] Leases=null);
internal readonly record struct NpcItemSpellAllocation(bool Allocated,ulong Seed);
internal sealed record NpcItemSpellEmission(int Result,int Attempts,long[] Flights);

public sealed class SorceressSpellProfiles:GenericPersistence
{
    private SorceressSpellSave _snapshot;
    public SorceressSpellProfiles():base("SanctuarySorceressSpellMissiles",226){}
    public override void Serialize(){_snapshot=SorceressSpellMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Sorceress spell flight version");}SorceressSpellMissiles.Restore(JsonSerializer.Deserialize<SorceressSpellSave>(reader.ReadString()));}
}

// Player straight spells reuse the existing checked kernels,not a pet actor's
// ownership/AI or a weapon packet. Native room/cell order,initial dispatch and
// creator RNG allocation remain declared world adapters.
internal static class SorceressSpellMissiles
{
    private sealed record Pool(ImmutableDictionary<long,SorceressSpellFlight> Flights,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,SorceressSpellFlight>.Empty,0);
    private static readonly HashSet<long> Loaded=new();
    private static Dictionary<(long,uint),WeaponFlightLease> Leases=new();
    internal static bool Ring(CombatAbility ability)=>ability is CombatAbility.Nova or CombatAbility.FrostNova;
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SorceressSpellMissiles));
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<bool> AllocateInferno=()=>true;
    public static void Configure()=>_ = new SorceressSpellProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));

    private sealed record SpellRoute(string Key,string Element,int Source,int Velocity,int DamageFunction,
        int Frames=50,int HitFunction=0,int ResultFlags=4,int HitFlags=0,int Size=1,int CollideKill=1,int InitSteps=1,int DamageRate=0);
    private static SpellRoute Profile(CombatAbility ability)=>ability switch
    {
        CombatAbility.FireBolt=>new("firebolt","fire",36,20,0),
        CombatAbility.Inferno=>new("infernoflame1","fire",41,12,0,30,CollideKill:0,DamageRate:82),
        CombatAbility.ChargedBolt=>new("chargedbolt","ltng",38,12,0,77),
        CombatAbility.Lightning=>new("lightningbolt","ltng",49,30,0,25,Size:2,CollideKill:0,InitSteps:0),
        CombatAbility.IceBolt=>new("icebolt","cold",39,12,0),
        CombatAbility.IceBlast=>new("iceblast","cold",45,12,4),
        CombatAbility.GlacialSpike=>new("glacialspike","cold",55,16,0,40,13,5,2),
        CombatAbility.Fireball=>new("fireball","fire",47,20,0,50,1,5,2),
        CombatAbility.Nova=>new("nova","ltng",48,24,0,13),
        CombatAbility.FrostNova=>new("frostnova","cold",44,24,0,14),
        CombatAbility.ChillingArmor=>new("chillingarmorbolt","cold",60,18,0,25,InitSteps:0),
        _=>throw new NotSupportedException("Unmapped player straight spell")
    };
    internal static void Route(SkillValues values)
    {
        bool ring=Ring(values.Ability),charged=values.Ability==CombatAbility.ChargedBolt,armor=values.Ability==CombatAbility.ChillingArmor,inferno=values.Ability==CombatAbility.Inferno;var profile=Profile(values.Ability);var row=SkillMechanics.For(values.Ability);int M(string key)=>NecromancerSummons.Source(profile.Key,key);
        if(values.Rank is <1 or >255||row.SourceId!=profile.Source
            ||row.Number("srvstfunc")!=(inferno?11:0)||row.Number("srvdofunc")!=(inferno?19:armor?18:ring?22:charged?17:0)||row.Text(ring||charged||armor||inferno?"srvmissilea":"srvmissile")!=profile.Key
            ||row.Text("EType")!=profile.Element||row.Number("SrcDam")!=0
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=profile.HitFunction||M("pSrvDmgFunc")!=profile.DamageFunction||M("Vel")!=profile.Velocity||M("MaxVel")!=profile.Velocity
            ||M("VelLev")!=0||M("Accel")!=(ring?-1000:0)||M("Range")!=(charged?98:profile.Frames)||M("Size")!=profile.Size||M("CollideType")!=3
            ||M("CollideKill")!=(ring?0:profile.CollideKill)||M("LastCollide")!=1||M("ToHit")!=0||M("NextHit")!=(ring?1:0)||M("NextDelay")!=(ring?4:0)||M("Pierce")!=0
            ||M("SrcDamage")!=0||M("CanSlow")!=(ring?0:1)||M("InitSteps")!=(ring?0:profile.InitSteps)||M("Activate")!=0
            ||ring&&(values.Eval("calc1")!=0||M("HitClass")!=(values.Ability==CombatAbility.Nova?64:48))
            ||M("ReturnFire")!=(armor||inferno?0:1)||M("GetHit")!=1||M("ResultFlags")!=profile.ResultFlags||M("HitFlags")!=profile.HitFlags||M("DamageRate")!=profile.DamageRate
            ||inferno&&(row.Number("Param1")!=35||row.Number("Param2")!=3||row.Number("Param8")!=16||row.Text("calc1")!="ln12/2"||M("HitClass")!=32)
            ||profile.HitFunction==13&&(M("sHitPar1")!=0||M("sHitPar2")!=0||new MissileValues(profile.Key,values).Text("EType")!="frze")
            ||profile.HitFunction==1&&(M("sHitPar1")!=0||new MissileValues(profile.Key,values).Text("EType")!="fire"))
        {throw new NotSupportedException("Unmapped player straight spell route");}
    }
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id) is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Sorceress?p:null;
    internal static SorceressSpellFlight Prepare(PlayerMobile owner,Point3D point,SkillValues values,DateTime at,int radialIndex=-1,long id=0,long cast=0,int ordinal=-1,ColdArmorState reaction=null)
    {
        if(values.Ability==CombatAbility.Inferno){throw new NotSupportedException("Inferno requires its channel-supplied source-coordinate factory");}
        Route(values);bool ring=Ring(values.Ability),charged=values.Ability==CombatAbility.ChargedBolt;
        bool armor=values.Ability==CombatAbility.ChillingArmor;
        if(armor!=(reaction!=null)||reaction!=null&&(reaction.Ability!=CombatAbility.ChillingArmor||reaction.Rank is <0 or >255
            ||reaction.Rank>0&&values.Rank!=reaction.Rank)) {throw new NotSupportedException("Chilling bolt requires its armor reaction context");}
        if(ring&&(radialIndex is <0 or >63||id<=0||cast<=0||ordinal!=-1)||charged&&(radialIndex!=-1||id<=0||cast!=0||ordinal<0||ordinal>=values.Eval("calc1"))||!ring&&!charged&&(radialIndex!=-1||id!=0||cast!=0||ordinal!=-1)){throw new NotSupportedException("Unmapped Sorceress radial launch");}
        if(owner==null||Owner(owner.Serial.Value)!=owner||owner.Map==null||owner.Map==Map.Internal
            ||point.X<=0||point.Y<=0||point.X>=owner.Map.Width||point.Y>=owner.Map.Height||at.Kind!=DateTimeKind.Utc
            ||SpellHelper.IsTown(owner.Location,owner)||SpellHelper.IsTown(point,owner))
        {throw new NotSupportedException("Unsupported player straight spell owner/aim");}
        var profile=Profile(values.Ability);
        var slow=AmazonSlowMissiles.Capture(owner,!ring);var mods=DruidFirestorm.Modifiers(owner);
        var ends=reaction is{Rank:0}?DruidFirestorm.Endpoints(reaction.Min256,reaction.Max256,mods.ColdMastery,slow)
            :DruidFirestorm.Endpoints(values,profile.Element=="cold"?mods.ColdMastery:profile.Element=="ltng"?mods.LightningMastery:mods.FireMastery,slow);
        // Straight casts supply33; do22 supplies7; armor event01 supplies32.
        // These are spell packets, without weapon AR/damage/ammo.
        int velocity=SlowMissilePayload.VelocityFp8(profile.Velocity,0,values.Rank,armor?32:ring?7:33,ring?profile.Velocity:0,!ring,slow.VelocityPercent);
        int dx=point.X-owner.X,dy=point.Y-owner.Y;if(ring){var direction=FrozenOrbs.Direction(radialIndex);dx=direction.X;dy=direction.Y;}else if(dx==0&&dy==0){dx=dy=1;} // Existing shared zero-vector projection.
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        // Unit allocation still draws once. Charged initialization then
        // replaces that seed and path generation advances the missile stream.
        ulong seed=SourceRandom.InitLow(SeedLow());SorceressChargedMotion motion=null;
        if(charged)
        {
            int ax=point.X*2,cx=point.X-owner.X,cy=point.Y-owner.Y;
            if(cx==0&&cy==0&&velocity!=0){ax++;cx=cy=1;}
            var path=ChargedBolts.Path(ChargedBolts.SeedSubtile(ax,ordinal),ChargedBolts.Heading(cx,cy),ChargedBolts.Frames,out seed);
            motion=new(owner.X,owner.Y,path,1,ordinal);
        }
        var flight=new SorceressSpellFlight(ring||charged?id:checked(_pool.NextId+1),owner.Serial.Value,owner.Map.MapID,owner.Z,
            values.Ability,reaction?.Rank??values.Rank,owner.X,owner.Y,vector.X,vector.Y,velocity,0,profile.Frames,ends.Min,ends.Max,mods,slow,
            seed,new([new(owner.X*2,owner.Y*2)]),at.AddMilliseconds(40))
            {ColdFrames=profile.Element=="cold"&&profile.HitFunction!=13?reaction is{Rank:0}?reaction.ColdFrames:values.DurationFrames:0,Cast=cast,RadialIndex=radialIndex,LastCollision=ring?owner.Serial.Value:0,
             Charged=motion};
        Validate(flight);return flight;
    }
    internal static (int Min,int Max,int Frames) CaptureInferno(SkillValues values,int mastery)
    {
        if(values.Ability!=CombatAbility.Inferno||mastery<0){throw new NotSupportedException("Invalid Inferno packet supplier");}
        Route(values);var ends=DruidFirestorm.Endpoints(values,mastery,SlowMissilePayload.Normal);
        return(ends.Min,ends.Max,Math.Max(1,(int)values.Eval("calc1")));
    }
    // Explicit do19 emission only. Payment/accepted intents belong to the
    // channel. Historical identity119 flames retain their original layout.
    internal static SorceressSpellFlight PrepareInferno(PlayerMobile owner,Point2D aim,SkillValues values,DateTime at)
    {
        if(owner==null||Owner(owner.Serial.Value)!=owner||owner.Map==null||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Unsupported Inferno stream owner");}
        var mods=DruidFirestorm.Modifiers(owner);var packet=CaptureInferno(values,mods.FireMastery);
        var slow=AmazonSlowMissiles.Capture(owner,true);
        if(slow.DamagePercent!=100){throw new NotSupportedException("Modern Inferno Slow Missiles damage order remains unverified");}
        if(aim.X is <=0 or >65535||aim.Y is <=0 or >65535){return null;}
        int dx=aim.X-owner.X*2,dy=aim.Y-owner.Y*2;
        if(Math.Abs(dx)>=100||Math.Abs(dy)>=100){return null;}
        if(dx==0&&dy==0){dx=dy=1;}
        if(!AllocateInferno()){return null;}
        int velocity=SlowMissilePayload.VelocityFp8(12,0,values.Rank,0x8020,0,true,slow.VelocityPercent);
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        var flight=new SorceressSpellFlight(checked(_pool.NextId+1),owner.Serial.Value,owner.Map.MapID,owner.Z,CombatAbility.Inferno,values.Rank,
            owner.X,owner.Y,vector.X,vector.Y,velocity,0,packet.Frames,packet.Min,packet.Max,mods,slow,
            SourceRandom.InitLow(SeedLow()),new([new(owner.X*2,owner.Y*2)]),at.AddMilliseconds(40)){LastCollision=owner.Serial.Value};
        Validate(flight);return flight;
    }
    internal static bool ReactiveArmor(PlayerMobile owner,Point3D point,ColdArmorState armor)
    {
        if(owner is not{Alive:true,Deleted:false}||Owner(owner.Serial.Value)!=owner||owner.Map==null||owner.Map==Map.Internal
            ||point.X<=0||point.Y<=0||point.X>=owner.Map.Width||point.Y>=owner.Map.Height
            ||SpellHelper.IsTown(owner.Location,owner)||SpellHelper.IsTown(point,owner)){return false;}
        // Rankless V0 armor has only captured base endpoints/length. The fixed
        // route has VelLev0 and constant lifetime; use rank1 only to validate
        // that route, retaining rank0 in the saved packet without guessing it.
        var values=armor.Rank>0?SkillMechanics.Values(owner,CombatAbility.ChillingArmor,armor.Rank)
            :new SkillValues(SkillMechanics.For(CombatAbility.ChillingArmor),_=>1,_=>0,ClassStats.Level(owner));
        var prepared=Prepare(owner,point,values,TimedEffects.Now,reaction:armor);
        Commit(prepared);return true; // Reactive emission has no additional mana.
    }
    // The child-seed factory is explicit: current original-item callback RNG
    // allocation is not inferred from the player/Mage SeedLow adapters.
    internal static SorceressSpellFlight[] PrepareOriginalNpc(SkeletonGuardian actor,Point3D point,int skill,int rank,DateTime at,Func<int,ulong> childSeed)
    {
        ArgumentNullException.ThrowIfNull(childSeed);
        var owner=actor?.ControlMaster as PlayerMobile;
        if(!NpcItemSpellFlights.Attached(actor,owner,actor?.Map?.MapID??-1)||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-4000)
            ||point.X<=0||point.Y<=0||point.X>=actor.Map.Width||point.Y>=actor.Map.Height||SpellHelper.IsTown(point,owner)||skill==53)
        {throw new NotSupportedException("Original NPC spell needs its active caster, supported emission, aim and UTC release context");}
        _=NpcEquipmentRandom.Require(actor);
        var packet=NpcItemSpellDamage.CaptureOriginal(actor,skill,rank);var ability=NpcItemSpellDamage.Ability(skill);
        var values=new SkillValues(SkillMechanics.For(ability),_=>rank,_=>0,OriginalNpcEquipment.Level(actor));Route(values);
        bool ring=Ring(ability);
        var slow=AmazonSlowMissiles.Capture(actor,!ring);long first=checked(_pool.NextId+1);
        var context=new NpcItemSpellSource(actor.Serial.Value,OriginalNpcEquipment.Binding(actor),packet);
        var flights=new SorceressSpellFlight[packet.Emissions];
        for(int i=0;i<flights.Length;i++)
        {
            flights[i]=PrepareOriginal(actor,actor.Location,point,context,slow,checked(first+i),ring?first:0,i,childSeed(i),at);
        }
        return flights;
    }
    internal static int NovaFrames=>Profile(CombatAbility.Nova).Frames;
    internal static SorceressSpellFlight[] PrepareDeathNova(AssassinShadowBody actor,NpcItemSpellSource context,DateTime at)
    {
        var owner=actor.ControlMaster as PlayerMobile;
        if(!context.DeathNova||!NpcItemSpellFlights.AttachedSource(actor,owner,actor.Map.MapID,context)||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Death nova needs its retained actual original source");}
        Route(new SkillValues(SkillMechanics.For(CombatAbility.Nova),_=>context.Packet.Rank,_=>0,actor.Prepared.Capture.Level));
        var slow=AmazonSlowMissiles.Capture(actor,false);long first=checked(_pool.NextId+1);var flights=new SorceressSpellFlight[64];
        for(int i=0;i<flights.Length;i++){flights[i]=PrepareOriginal(actor,actor.Location,actor.Location,context,slow,checked(first+i),first,i,NpcWorldAllocation.UnitSeed(),at,7);}
        return flights;
    }
    // Shared packet/velocity/path constructor. Ordinary casts and synchronous
    // item requests differ in origin and allocation,not their movement kernel.
    private static SorceressSpellFlight PrepareOriginal(SkeletonGuardian actor,Point3D origin,Point3D aim,NpcItemSpellSource context,
        SlowMissilePayload slow,long id,long cast,int ordinal,ulong seed,DateTime at,int flags=0)
    {
        var packet=context.Packet;int rank=packet.Rank;var ability=NpcItemSpellDamage.Ability(packet.Skill);var profile=Profile(ability);
        bool ring=Ring(ability),charged=ability==CombatAbility.ChargedBolt;
        if(flags==0){flags=ring?7:33;}
        int velocity=SlowMissilePayload.VelocityFp8(profile.Velocity,0,rank,flags,ring?profile.Velocity:0,!ring,slow.VelocityPercent);
        int dx=aim.X-origin.X,dy=aim.Y-origin.Y;
        if(ring){var direction=FrozenOrbs.Direction(ordinal);dx=direction.X;dy=direction.Y;}else if(dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        SorceressChargedMotion motion=null;
        if(charged)
        {
            int ax=aim.X*2,cx=aim.X-origin.X,cy=aim.Y-origin.Y;
            if(cx==0&&cy==0&&velocity!=0){ax++;cx=cy=1;}
            var path=ChargedBolts.Path(ChargedBolts.SeedSubtile(ax,ordinal),ChargedBolts.Heading(cx,cy),ChargedBolts.Frames,out seed);
            motion=new(origin.X,origin.Y,path,1,ordinal);
        }
        var flight=new SorceressSpellFlight(id,actor.ControlMaster.Serial.Value,actor.Map.MapID,origin.Z,ability,rank,origin.X,origin.Y,
            vector.X,vector.Y,velocity,0,profile.Frames,slow.Damage256(packet.Minimum256),slow.Damage256(packet.Maximum256),new(0,0,0,0,0),slow,seed,
            new([new(origin.X*2,origin.Y*2)]),at.AddMilliseconds(40))
            {Npc=context,ColdFrames=packet.ColdFrames,Cast=ring?cast:0,RadialIndex=ring?ordinal:-1,LastCollision=ring?actor.Serial.Value:0,
             Charged=motion};
        Validate(flight);return flight;
    }
    // Explicit world host for qualified original item frames. The supplied
    // allocator owns each actual attempt and its seed;there is no RNG default.
    // Internal IDs reserve attempted ring slots so partial rings retain the
    // existing Cast+RadialIndex persistence identity without fabricated rays.
    internal static NpcItemSpellEmission EmitOriginalItem(SkeletonGuardian actor,NpcItemTarget target,Point3D path,
        int skill,int rank,int outgoing,DateTime at,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,bool serverDo=false,Action<bool> serverDoChanged=null)
    {
        ArgumentNullException.ThrowIfNull(allocate);
        var owner=actor?.ControlMaster as PlayerMobile;
        if(skill is not(36 or 38 or 39 or 44 or 48 or 49)||!NpcItemSpellFlights.Attached(actor,owner,actor?.Map?.MapID??-1)
            ||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-4000))
        {throw new NotSupportedException("Original item emission needs its qualified spell,active source and UTC context");}
        _=NpcEquipmentRandom.Require(actor);
        var packet=NpcItemSpellDamage.CaptureOriginal(actor,skill,rank);var ability=NpcItemSpellDamage.Ability(skill);
        Route(new SkillValues(SkillMechanics.For(ability),_=>rank,_=>0,OriginalNpcEquipment.Level(actor)));
        var point=target?.Point(actor.Map);bool ring=Ring(ability),charged=skill==38;var slow=AmazonSlowMissiles.Capture(actor,!ring);
        var context=new NpcItemSpellSource(actor.Serial.Value,OriginalNpcEquipment.Binding(actor),packet);
        var before=_pool;long first=checked(before.NextId+1);int attempts=0;var flights=new List<SorceressSpellFlight>();
        var frame=new NpcItemSpellOrigins((actor.X*2,actor.Y*2),point is{} p?(p.X*2,p.Y*2):null,(path.X*2,path.Y*2),
            OriginalNpcEquipment.Graph(actor).Read(19),serverDo,request=>
        {
            int attempt=attempts++,ordinal=charged?request.ChargedOrdinal:attempt;
            if(request.Skill!=skill||request.Rank!=rank||request.Missile!=ItemSkillExecution.MissileId(packet.Missile)
                ||ordinal<0||ordinal>=packet.Emissions||(request.X&1)!=0||(request.Y&1)!=0
                ||ring&&(request.Flags!=7||request.Velocity!=24||request.AttackRating!=0)
                ||!ring&&((request.Flags&~0x1000)!=33||request.Velocity!=0))
            {throw new NotSupportedException("Original item factory request does not match its qualified world route");}
            var origin=new Point3D(request.X/2,request.Y/2,actor.Z);
            // Full source factory validates its origin room before allocation.
            // Use existing UO world/height rules,not the projected destination.
            if(origin.X<=0||origin.Y<=0||origin.X>=actor.Map.Width||origin.Y>=actor.Map.Height
                ||SpellHelper.IsTown(origin,owner)) {return null;}
            int dx=ring?request.TargetX:request.TargetX-request.X,dy=ring?request.TargetY:request.TargetY-request.Y;
            if(!ring&&(Math.Abs(dx)>=100||Math.Abs(dy)>=100)){return null;}
            var allocation=allocate(request);if(!allocation.Allocated){return null;}
            var aim=ring?origin:new Point3D(request.TargetX/2,request.TargetY/2,actor.Z);
            var flight=PrepareOriginal(actor,origin,aim,context,slow,checked(first+attempt),ring?first:0,ordinal,allocation.Seed,at,request.Flags);
            flights.Add(flight);return flight;
        },(_,_)=>throw new NotSupportedException("Original Chain root needs its separate live budget host"),chargedAim:_=>
        {
            var p=target?.Point(actor.Map);return p is{} current?(current.X*2,current.Y*2):(path.X*2,path.Y*2);
        },serverDoChanged:serverDoChanged);
        int result=frame.Run(skill,rank,outgoing);
        if(!ReferenceEquals(before,_pool)){throw new InvalidOperationException("Original item allocator changed the flight pool reentrantly");}
        var pool=before.Flights;
        foreach(var flight in flights){pool=pool.Add(flight.Id,Copy(flight));}
        if(attempts!=0){_pool=new(pool,checked(before.NextId+attempts));}
        return new(result,attempts,flights.Select(f=>f.Id).ToArray());
    }
    internal static void CommitValkyrie(SorceressSpellFlight[] flights)
    {
        if(flights is not{Length:>=1 and<=64}||flights[0]?.Npc is not{} source||flights.Length!=source.Packet.Emissions)
        {throw new NotSupportedException("Original NPC spell needs its complete prepared emission batch");}
        long first=checked(_pool.NextId+1);var pool=_pool.Flights;
        for(int i=0;i<flights.Length;i++)
        {
            var f=flights[i];Validate(f);
            if(f.Id!=checked(first+i)||f.Npc!=source||f.Owner!=flights[0].Owner||f.Map!=flights[0].Map||f.NextUtc!=flights[0].NextUtc
                ||Ring(f.Ability)&&(f.Cast!=first||f.RadialIndex!=i)||f.Charged!=null&&f.Charged.Ordinal!=i)
            {throw new InvalidOperationException("Stale, reordered or mixed original NPC spell batch");}
            pool=pool.Add(f.Id,Copy(f));
        }
        _pool=new(pool,flights[^1].Id);
    }
    internal static long Commit(SorceressSpellFlight flight)
    {
        if(flight.Npc!=null){throw new NotSupportedException("Original NPC emissions require atomic batch commit");}
        Validate(flight);if(flight.Id!=_pool.NextId+1){throw new InvalidOperationException("Stale prepared Sorceress flight");}
        _pool=new(_pool.Flights.Add(flight.Id,Copy(flight)),flight.Id);return flight.Id;
    }
    internal static SorceressSpellFlight[] PrepareRing(PlayerMobile owner,SkillValues values,DateTime at)
    {
        if(!Ring(values.Ability)){throw new NotSupportedException("Expected Sorceress Nova ring");}
        long first=checked(_pool.NextId+1);
        return Enumerable.Range(0,64).Select(i=>Prepare(owner,owner.Location,values,at,i,checked(first+i),first)).ToArray();
    }
    internal static SorceressSpellFlight[] PrepareCharged(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {
        if(values.Ability!=CombatAbility.ChargedBolt){throw new NotSupportedException("Expected player Charged Bolt");}
        int count=checked((int)values.Eval("calc1"));if(count is <1 or >24){throw new NotSupportedException("Unsupported charged count");}
        long first=checked(_pool.NextId+1);
        return Enumerable.Range(0,count).Select(i=>Prepare(owner,point,values,at,id:checked(first+i),ordinal:i)).ToArray();
    }
    internal static bool CastCharged(PlayerMobile owner,Point3D point,SkillValues values)
    {
        var prepared=PrepareCharged(owner,point,values,TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        foreach(var flight in prepared){Commit(flight);}owner.PlaySound(0x1E5);return true;
    }
    internal static bool CastRing(PlayerMobile owner,SkillValues values)
    {
        var prepared=PrepareRing(owner,values,TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        foreach(var flight in prepared){Commit(flight);}owner.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
    internal static bool Cast(PlayerMobile owner,Point3D point,SkillValues values)
    {
        // Preparation publishes no flight;lost mana cannot emit a packet.
        var flight=Prepare(owner,point,values,TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Commit(flight);owner.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
    private static SorceressSpellFlight Copy(SorceressSpellFlight s)=>s with{Steps=new(s.Steps.Cells.ToArray()),Charged=s.Charged is{} c?c with{Path=c.Path.ToArray()}:null};
    internal static SorceressSpellSave Snapshot()
    {var pool=_pool;return new(pool.Flights.Values.OrderBy(s=>s.Id).Select(Copy).ToArray(),pool.NextId,Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray());}
    private static void Remove(long id){_pool=_pool with{Flights=_pool.Flights.Remove(id)};Loaded.Remove(id);}
    internal static void Cancel(Mobile owner)
    {if(owner!=null){foreach(long id in _pool.Flights.Values.Where(s=>s.Owner==owner.Serial.Value||s.Npc?.Actor==owner.Serial.Value).Select(s=>s.Id).ToArray()){Remove(id);}}}

    internal static bool Ready(long cast,uint target,DateTime at)=>!Leases.TryGetValue((cast,target),out var lease)||lease.UntilUtc<=at;
    internal static int Impact(PlayerMobile owner,Mobile target,SorceressSpellFlight flight,out ulong seed)
    {
        if(flight.Npc!=null){return NpcItemSpellFlights.Impact(owner,target,flight,out seed);}
        int raw=Roll(flight,out seed);
        var profile=Profile(flight.Ability);
        return ImpactPrepared(owner,target,profile.Key,profile.Element,raw,flight.Modifiers,flight.NextUtc,flight.ColdFrames,profile.DamageFunction==4,profile.DamageRate);
    }
    internal static int ImpactPrepared(PlayerMobile owner,Mobile target,string key,string element,int raw,FirestormModifiers captured,DateTime at,int coldFrames=0,bool freeze=false,int damageRate=0)
    {
        SorceressEffects.MissileHit(owner,target,key);
        if(ClassAvoidance.Missile(target,key)){return 0;}
        owner.DoHarmful(target,true);
        var modifiers=captured with{Potion=CombatPotions.BonusAt(owner,"magical-penetration",at)};
        var packet=element=="cold"?new GearDamage(0,0,raw/256.0,0,0):element=="ltng"?new(0,0,0,raw/256.0,0):new(0,raw/256.0,0,0,0);
        var parts=SkillCombat.MitigatePrepared(owner,target,DruidMoltenBoulder.Potion(modifiers,packet),damageRate);
        if(freeze){FreezeStates.Apply(owner,target,coldFrames,TimedEffects.TickAt(at));}
        else if(element=="cold"){ColdStates.Apply(owner,target,coldFrames,TimedEffects.TickAt(at));}
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,owner,loss);}
        CombatDamage.Finish(target,owner);return loss;
    }
    internal static int Roll(SorceressSpellFlight flight,out ulong seed)=>Roll(flight.Minimum256,flight.Maximum256,flight.Seed,out seed);
    internal static int Roll(int minimum,int maximum,ulong initial,out ulong seed)
    {ulong stream=initial;int raw=WeaponMissileDamage.RollValue(minimum,maximum,0,n=>(int)SourceRandom.Limited(ref stream,n));seed=stream;return raw;}
    internal static double ExplosionRadius(SkillValues values)=>values.Ability switch
    {
        CombatAbility.Fireball=>SkillGeometry.Tiles(Math.Max(1,values.Eval("calc1"))),
        CombatAbility.GlacialSpike=>GlacialSpikes.Radius(values),
        _=>throw new NotSupportedException("Unmapped player explosion radius")
    };
    internal static void Explosion(PlayerMobile owner,Point3D center,SorceressSpellFlight flight,out ulong seed)
    {
        var profile=Profile(flight.Ability);if(profile.HitFunction is not(1 or 13)){throw new NotSupportedException("Unmapped player area callback");}
        // Both callbacks evaluate radius on the current owner at retained
        // rank; only hit13 evaluates freeze length. Damage stays captured.
        var values=SkillMechanics.Values(owner,flight.Ability,flight.Rank);double radius=ExplosionRadius(values);
        int frames=profile.HitFunction==13?GlacialSpikes.FreezeFrames(values):0;
        int raw=Roll(flight,out seed);var mods=flight.Modifiers with{Potion=CombatPotions.BonusAt(owner,"magical-penetration",flight.NextUtc)};
        var basis=profile.Element=="fire"?new GearDamage(0,raw/256.0,0,0,0):new(0,0,raw/256.0,0,0);
        var packet=DruidMoltenBoulder.Potion(mods,basis);
        SkillCombat.CopiedArea(owner,center,radius,target=>
        {
            owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,packet);
            if(frames>0){FreezeStates.Apply(owner,target,frames,TimedEffects.TickAt(flight.NextUtc));}
            int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
            if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,owner,loss);}CombatDamage.Finish(target,owner);
        });
        Effects.SendLocationEffect(center,owner.Map,profile.Element=="fire"?0x36BD:0x36E4,8,10,profile.Element=="fire"?0x489:0x480,0); // Identified shared explosion placeholders.
    }
    private static void Advance(SorceressSpellFlight flight,DateTime at,bool? actions)
    {
        var npc=flight.Npc==null?null:World.FindMobile((Serial)flight.Npc.Actor)as SkeletonGuardian;
        var owner=flight.Npc==null?Owner(flight.Owner):World.FindMobile((Serial)flight.Owner)as PlayerMobile;
        if(flight.Npc==null&&(owner==null||owner.Map!=Map.Internal&&owner.Map?.MapID!=flight.Map)){Remove(flight.Id);return;}
        Mobile caster=flight.Npc==null?owner:npc;
        bool attached=flight.Npc==null||NpcItemSpellFlights.AttachedSource(npc,owner,flight.Map,flight.Npc);
        bool loaded=Loaded.Remove(flight.Id);
        int elapsed=loaded?(int)Math.Min(flight.Frames-flight.Frame,(at-flight.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
        bool ring=Ring(flight.Ability),nonKilling=ring||Profile(flight.Ability).CollideKill==0;int frame=flight.Frame+elapsed;var next=flight with{Frame=frame,NextUtc=flight.NextUtc.AddMilliseconds(40L*elapsed)};
        // Existing persistent-spell host policy:offline/restart age harmlessly,
        // never replay past damage. This is not a modern owner-removal proof.
        if(loaded||flight.Fault!=null||!attached||owner?.Map==Map.Internal||!(actions??owner?.NetState is{Running:true}))
        {if(frame>=flight.Frames){Remove(flight.Id);}else{_pool=_pool with{Flights=_pool.Flights.SetItem(flight.Id,next)};}return;}
        var map=Map.Maps[flight.Map];bool barrier=false,removed=false;
        if(SpellHelper.IsTown(new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z),owner)){Remove(flight.Id);return;}
        int Move()
        {
            if(ring)
            {
                var d=FrozenOrbs.Direction(flight.RadialIndex);var direction=MissileVectors.Direction(d.X,d.Y);
                var motion=MissileVectors.Accelerate(flight.VelocityFp8,-1000,flight.AccelerationCounter,6144,0,direction.X,direction.Y);
                next=next with{VelocityFp8=motion.Velocity,AccelerationCounter=motion.Counter,Vx=motion.X/131072.0,Vy=motion.Y/131072.0};
            }
            if(flight.Charged is{} charged)
            {
                var moved=ChargedBolts.Advance(charged.Path,charged.OriginX,charged.OriginY,flight.X,flight.Y,charged.Next,SkillGeometry.Tiles(flight.VelocityFp8/4096.0));
                next=next with{X=moved.X,Y=moved.Y,Charged=charged with{Next=moved.Next},Steps=SourceMissileSteps.Advance(flight.X,flight.Y,moved.X-flight.X,moved.Y-flight.Y,flight.Steps)};
                if(moved.Next>=charged.Path.Length){return 2;}
            }
            else{next=next with{X=flight.X+next.Vx,Y=flight.Y+next.Vy,Steps=SourceMissileSteps.Advance(flight.X,flight.Y,next.Vx,next.Vy,flight.Steps)};}
            barrier=next.X<0||next.Y<0||next.X>=map.Width||next.Y>=map.Height;return barrier?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var candidate in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,flight.Z),3))
            {
                if(candidate is not{Alive:true,Deleted:false,Blessed:false}||Math.Abs(candidate.Z-flight.Z)>=16
                    ||nonKilling&&candidate.Serial.Value==flight.LastCollision
                    ||ring&&!Ready(flight.Cast,candidate.Serial.Value,flight.NextUtc)
                    ||!ClassCombat.IsHostile(owner,candidate)||!(flight.Npc==null?caster.CanBeHarmful(candidate,false):NpcItemSpellFlights.CanHarm(npc,owner,candidate,flight.Npc))||flight.Npc!=null&&ClassCombat.Friendly(caster,candidate)||SpellHelper.IsTown(candidate.Location,owner)){continue;}
                int size=candidate is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(Profile(flight.Ability).Size,size,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){return candidate;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)
        {bool blocked=DruidFirestorm.CellBlocked(map,flight.Z,flight.X,flight.Y,cell.X,cell.Y);if(blocked){barrier=true;}return blocked;}
        int Hit(uint serial,bool terminal)
        {
            if(flight.Ability is CombatAbility.GlacialSpike or CombatAbility.Fireball)
            {
                if(!terminal&&World.FindMobile((Serial)serial) is{} primary){SorceressEffects.MissileHit(owner,primary,Profile(flight.Ability).Key);}
                var center=next.X>=0&&next.Y>=0&&next.X<map.Width&&next.Y<map.Height
                    ?new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z):new((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z);
                Explosion(owner,center,flight,out var seed);next=next with{Seed=seed};
            }
            else if(!terminal&&World.FindMobile((Serial)serial) is{} target)
            {
                if(ring){Leases[(flight.Cast,serial)]=new(flight.Cast,serial,flight.NextUtc.AddMilliseconds(160));}
                Impact(owner,target,flight,out var seed);next=next with{Seed=seed,LastCollision=nonKilling?serial:0};
                if(nonKilling){return 1;}
            }
            removed=true;return 2; // CollideKill1; no lifetime hit-set or piercing.
        }
        var result=BasicMissileUpdate.Run(new(true,true,flight.VelocityFp8,flight.Frames-flight.Frame,2,flight.Frames,true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Steps.Cells,
                cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Wall(cell)?0:Find(cell)?.Serial.Value??0,Hit));
        if(removed||barrier||result.Result==2){Remove(flight.Id);return;}
        // A reactive consumer may have cleared the owner's flights.
        if(_pool.Flights.ContainsKey(flight.Id)){_pool=_pool with{Flights=_pool.Flights.SetItem(flight.Id,next)};}
        if(frame%2==0&&(!ring||flight.RadialIndex%8==0)){bool cold=Profile(flight.Ability).Element=="cold",lightning=Profile(flight.Ability).Element=="ltng";Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z),map,lightning?0x3818:cold?0x36E4:0x36D4,2,10,lightning?0x482:cold?0x480:0x489,0);} // Identified shared bolt placeholders.
    }
    internal static void Tick(DateTime at,bool? actions=null,long? onlyId=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Sorceress flight clock must be UTC");}
        while(true)
        {
            var flight=_pool.Flights.Values.Where(s=>s.NextUtc<=at&&(!onlyId.HasValue||s.Id==onlyId.Value)).OrderBy(s=>s.NextUtc).ThenByDescending(s=>s.Id).FirstOrDefault();if(flight==null){break;}
            try{Advance(flight,at,actions);}
            catch(NotSupportedException e)
            {
                Log.Error(e,"Sorceress flight suspended: {Id}",flight.Id);
                if(_pool.Flights.ContainsKey(flight.Id))
                {if(flight.Frame+1>=flight.Frames){Remove(flight.Id);}else{_pool=_pool with{Flights=_pool.Flights.SetItem(flight.Id,flight with{Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40),Fault=e.Message})};}}
            }
        }
        var active=_pool.Flights.Values.Where(x=>Ring(x.Ability)).Select(x=>x.Cast).ToHashSet();
        foreach(var key in Leases.Where(x=>x.Value.UntilUtc<=at||!active.Contains(x.Key.Item1)).Select(x=>x.Key).ToArray()){Leases.Remove(key);}
    }
    private static void Validate(SorceressSpellFlight s)
    {
        if(s==null||s.Ability is not(CombatAbility.FireBolt or CombatAbility.Inferno or CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.GlacialSpike or CombatAbility.Fireball or CombatAbility.Nova or CombatAbility.FrostNova or CombatAbility.Lightning or CombatAbility.ChargedBolt or CombatAbility.ChillingArmor))
        {throw new InvalidDataException("Invalid saved Sorceress spell kind");}
        if(s.Npc!=null){NpcItemSpellFlights.Validate(s);}
        var profile=Profile(s.Ability);bool ring=Ring(s.Ability),capturedCold=s.Ability is CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.FrostNova or CombatAbility.ChillingArmor;
        if(s.Ability==CombatAbility.ChargedBolt)
        {
            if(s.Charged is not{} c||c.OriginX<=0||c.OriginY<=0||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||c.OriginX>=Map.Maps[s.Map].Width||c.OriginY>=Map.Maps[s.Map].Height
                ||c.Path?.Length!=39||c.Next is <1 or >39||c.Ordinal is <0 or >23||c.Path[0]!=Point2D.Zero)
            {throw new InvalidDataException("Invalid saved player charged path");}
            for(int i=1;i<c.Path.Length;i++){long dx=Math.Abs((long)c.Path[i].X-c.Path[i-1].X),dy=Math.Abs((long)c.Path[i].Y-c.Path[i-1].Y);if(dx>1||dy>1||dx+dy==0){throw new InvalidDataException("Invalid player charged waypoint");}}
        }
        else if(s.Charged!=null){throw new InvalidDataException("Unexpected player charged path");}
        if(s.Id<=0||!((Serial)s.Owner).IsMobile||s.Rank<(s.Ability==CombatAbility.ChillingArmor?0:1)||s.Rank>255
            ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal||s.Z is <-128 or >127
            ||!double.IsFinite(s.X+s.Y+s.Vx+s.Vy)||s.X<=0||s.Y<=0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height
            ||Math.Abs(s.Vx)>(s.Ability==CombatAbility.Lightning?0.703125:ring?0.5625:0.5)||Math.Abs(s.Vy)>(s.Ability==CombatAbility.Lightning?0.703125:ring?0.5625:0.5)||s.VelocityFp8<0||s.VelocityFp8>profile.Velocity*192||s.Frame<0||s.Frames!=(s.Ability==CombatAbility.Inferno?(35+3*(s.Rank-1))/2:profile.Frames)||s.Frame>=s.Frames
            ||s.Ability==CombatAbility.Inferno&&(s.LastCollision==0||s.Slow.DamagePercent!=100)
            ||(capturedCold?s.ColdFrames<(s.Ability==CombatAbility.ChillingArmor&&s.Rank==0?0:1)||s.ColdFrames>(s.Ability==CombatAbility.ChillingArmor?12250:s.Ability==CombatAbility.IceBlast?4035:s.Ability==CombatAbility.FrostNova?6550:9040):s.ColdFrames!=0)
            ||ring&&(s.Cast<=0||s.Cast>s.Id||s.Id!=s.Cast+s.RadialIndex||s.RadialIndex is <0 or >63||s.AccelerationCounter is <0 or >4||s.Slow.VelocityPercent!=100)
            ||!ring&&(s.Cast!=0||s.RadialIndex!=-1||s.AccelerationCounter!=0||profile.CollideKill!=0&&s.LastCollision!=0)
            ||s.LastCollision!=0&&!((Serial)s.LastCollision).IsMobile
            ||s.Minimum256<0||s.Maximum256<s.Minimum256||s.Modifiers==null||s.Slow.VelocityPercent is <0 or >100||s.Slow.DamagePercent is <0 or >100
            ||s.NextUtc.Kind!=DateTimeKind.Utc||s.Steps?.Cells is not{Length:>=1 and <=10}
            ||s.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[s.Map].Width*2||c.Y>=Map.Maps[s.Map].Height*2))
        {throw new InvalidDataException("Invalid saved Sorceress spell flight");}
    }
    internal static void Restore(SorceressSpellSave saved)
    {
        if(saved?.Flights==null||saved.NextId<0){throw new InvalidDataException("Invalid Sorceress flight save");}
        var flights=ImmutableDictionary<long,SorceressSpellFlight>.Empty;
        foreach(var flight in saved.Flights){Validate(flight);if(flight.Id>saved.NextId||flights.ContainsKey(flight.Id)){throw new InvalidDataException("Duplicate Sorceress flight");}flights=flights.Add(flight.Id,Copy(flight));}
        if(saved.Leases==null&&flights.Values.Any(x=>Ring(x.Ability))){throw new InvalidDataException("Missing Sorceress ring leases");}
        var leases=new Dictionary<(long,uint),WeaponFlightLease>();
        foreach(var lease in saved.Leases??[])
        {
            if(lease.Cast<=0||lease.Cast>saved.NextId||!((Serial)lease.Target).IsMobile||lease.UntilUtc.Kind!=DateTimeKind.Utc||!leases.TryAdd((lease.Cast,lease.Target),lease))
            {throw new InvalidDataException("Invalid Sorceress ring lease");}
        }
        Leases=leases;_pool=new(flights,saved.NextId);Loaded.Clear();foreach(var id in flights.Keys){Loaded.Add(id);}
    }
}
