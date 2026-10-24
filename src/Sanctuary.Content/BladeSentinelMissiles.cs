// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record BladeMissileState(long Id,uint Owner,uint OriginalOwner,int Map,int Z,int Rank,
    BladeFollowPosition Position,WeaponMissileDamage Damage,int TargetDefenseDelta,ulong Seed,DateTime NextUtc,string Fault=null);
internal sealed record BladeMissileSave(BladeMissileState[] Missiles,WeaponFlightLease[] Leases,long NextId);

public sealed class BladeMissileProfiles:GenericPersistence
{
    private BladeMissileSave _snapshot;
    public BladeMissileProfiles():base("SanctuaryBladeMissiles",155){}
    public override void Serialize(){_snapshot=BladeSentinelMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown blade missile version");}BladeSentinelMissiles.Restore(JsonSerializer.Deserialize<BladeMissileSave>(reader.ReadString()));}
}

// One following collision missile per successfully armed blade. The shared
// conductor runs this phase before monster movement at each due source frame.
public static class BladeSentinelMissiles
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeSentinelMissiles));
    private static readonly Dictionary<long,BladeMissileState> Missiles=new();
    private static readonly Dictionary<(long,uint),WeaponFlightLease> Leases=new();
    private static long _nextId;
    internal static Random LaunchRandom=new();
    internal static Func<int> SeedLow=()=>Utility.Random(int.MaxValue);
    public static BladeMissileProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal static DateTime NextFrame=>Missiles.Values.Where(x=>x.Fault==null).Select(x=>x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();
    internal static AssassinTrapCreationStats Create(BladeSentinelBody body)
    {
        var prepared=body.Prepared;
        if(Missiles.Values.Any(x=>x.Owner==body.Serial.Value)){throw new InvalidOperationException("Repeated blade following missile");}
        if(World.FindMobile((Serial)prepared.Owner) is not PlayerMobile owner){throw new NotSupportedException("Missing blade player snapshot supplier");}
        var capture=BladeSentinelLaunch.Capture(owner,prepared.Creation,LaunchRandom);
        long id=checked(++_nextId);var pose=new BladeFollowPosition((int)(prepared.Motion.X>>16),(int)(prepared.Motion.Y>>16),10+5*prepared.Creation.Rank);
        Missiles.Add(id,new(id,body.Serial.Value,owner.Serial.Value,prepared.Map,prepared.Z,prepared.Creation.Rank,pose,capture.Damage,
            capture.TargetDefenseDelta,SourceRandom.InitLow(SeedLow()),prepared.NextFrameUtc.AddMilliseconds(40)));
        return capture.Creation;
    }
    internal static bool Ready(long missile,uint target,DateTime at)=>!Leases.TryGetValue((missile,target),out var lease) || lease.UntilUtc<=at;
    internal static BaseCreature Recipient(BladeSentinelBody body,BladeMissileState state,int x,int y)
    {
        if(World.FindMobile((Serial)state.OriginalOwner) is not PlayerMobile owner){throw new NotSupportedException("Missing blade allegiance supplier");}
        foreach(var target in body.Map.GetMobilesInRange<BaseCreature>(new Point3D((x+1)/2,(y+1)/2,state.Z),3))
        {
            int size=CreatureProfiles.For(target).Traits.Size??2;
            if(Math.Abs(target.Z-state.Z)<16 && CombatHitboxes.SourceTouches(3,size,x-target.X*2,y-target.Y*2)
                && body.TrapTarget(owner,target) && Ready(state.Id,target.Serial.Value,state.NextUtc)
                && !SpellHelper.IsTown(target.Location,target)){return target;}
        }
        return null;
    }
    internal static void PruneFaults()
    {
        foreach(var id in Missiles.Values.Where(x=>x.Fault!=null && World.FindMobile((Serial)x.Owner) is not BladeSentinelBody{Deleted:false,Alive:true}).Select(x=>x.Id).ToArray())
        {Missiles.Remove(id);}
    }
    internal static void TickFrame(DateTime at)
    {
        PruneFaults();
        foreach(var state in Missiles.Values.Where(x=>x.Fault==null && x.NextUtc==at).OrderByDescending(x=>x.Id).ToArray())
        {
            try{Advance(state);}
            catch(Exception e){Missiles[state.Id]=state with{Fault=e.Message};Log.Error(e,"Blade following missile suspended: {Id}",state.Id);}
        }
        foreach(var key in Leases.Where(x=>!Missiles.ContainsKey(x.Key.Item1) && x.Value.UntilUtc<=at).Select(x=>x.Key).ToArray()){Leases.Remove(key);}
    }
    private static void Advance(BladeMissileState state)
    {
        var body=World.FindMobile((Serial)state.Owner) as BladeSentinelBody;
        bool live=body is{Alive:true,Deleted:false} && body.Prepared.Control?.Mode!=0;
        if(live && (body.Map?.MapID!=state.Map || body.Prepared.ControllerFault!=null || body.Prepared.Owner!=state.OriginalOwner
            || !body.Prepared.Creation.MissileArmed || body.Prepared.Creation.Rank!=state.Rank))
        {throw new NotSupportedException("Blade missile owner map/controller lifecycle is not connected");}
        if(live && (World.FindMobile((Serial)state.OriginalOwner) is not PlayerMobile{Alive:true,Deleted:false} original
            || !ClassCreation.Profiles.TryGet(original.Serial,out var profile) || profile.Class!=DiabloClass.Assassin))
        {throw new NotSupportedException("Blade missile original owner death/map/class lifecycle is not connected");}
        if(live && !AssassinTrapRoster.Fighting(body))
        {Missiles[state.Id]=state with{NextUtc=state.NextUtc.AddMilliseconds(40)};return;}
        var next=state;BaseCreature target=null;int mask=0;
        var position=BladeSentinelContact.Advance(state.Position,live?new((int)(body.Prepared.Motion.X>>16),(int)(body.Prepared.Motion.Y>>16)):null,!live,live,true,
            10+5*state.Rank,point=>
            {
                // The native room adapter is the same flat fixed-height map as
                // the prepared actor. No swept native tile/rounded-body hitbox.
                if(point.X<=0 || point.Y<=0 || point.X>=body.Map.Width*2 || point.Y>=body.Map.Height*2)
                {throw new NotSupportedException("Blade following missile room relocation is not mapped");}
                target=Recipient(body,state,point.X,point.Y);mask=target!=null?0x100:0;
                // Native terrain fit is an explicit missile-barrier projection.
                if(!body.Map.CanFit((point.X+1)/2,(point.Y+1)/2,state.Z,16,true,false,true)){mask|=4;}
                return mask;
            },()=>mask,()=>target?.Serial.Value,id=>
            {
                var stats=body.Prepared.Creation;
                int percent=stats.AttackPercent+(SorceressEffects.Enchantment(body)?.AttackPercent??0)+PaladinAuras.AttackPercent(body)+NecromancerEffects.AttackStatPercent(body);
                int chance=AssassinTrapAttack.Chance(stats.Level,stats.Attack,body.Dex,percent,ClassStats.Level(target),ClassStats.Defense(target));
                return AssassinTrapAttack.Hit(chance,body.RollSourceRandom());
            },(id,delay)=>Leases[(state.Id,id)]=new(state.Id,id,state.NextUtc.AddMilliseconds(delay*40L)),
            id=>{if(id!=null){SorceressEffects.MissileHit(body,target,"blade creeper");}},id=>
            {
                ulong seed=next.Seed;int Roll(int bound)=>(int)SourceRandom.Limited(ref seed,bound);
                Impact(body,target,state,Roll);next=next with{Seed=seed};
            });
        if(!position.Alive){Missiles.Remove(state.Id);return;}
        Missiles[state.Id]=next with{Position=position,NextUtc=state.NextUtc.AddMilliseconds(40)};
    }
    private static void Impact(BladeSentinelBody body,BaseCreature target,BladeMissileState state,Func<int,int> roll)
    {
        var raw=state.Damage.Roll(target,roll,out int poisonRate);
        if(!SourceBlocking.TryBlock(target,raw.Physical>0,out bool blocked)){throw new NotSupportedException("Blade recipient block supplier is not mapped");}
        if(!blocked)
        {
            body.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(body,target,raw with{Poison=0});
            NecromancerEffects.LifeTap(body,target,NamedElements.To256(parts.Physical));
            WeaponLeech.Apply(body,target,NamedElements.To256(parts.Physical),state.Damage.Elements);
            if(raw.Cold>0){ColdStates.Apply(body,target,state.Damage.Elements.ColdFrames,Core.TickCount);}
            // CalculateTotalDamage adds the non-hireling NPC life-drain lane
            // to direct damage separately from physical resistance/reduction.
            // Its modern acceptance remains an explicit legacy-source gap.
            int direct=checked(NamedElements.To256(parts.Total)+Math.Max(0,state.Damage.Elements.LifeDrain));
            int loss=SkillHealth.Damage(body,target,direct);CreatureHitRecovery.Apply(target,body,loss);
            if(target is{Alive:true,Deleted:false} && poisonRate>0)
            {
                int resistance=NamedElements.Resistance(body,target,"pois",target.PoisonResistance,pierceOverride:0);
                int pierce=body.Passive("passive_pois_pierce");if(pierce<0 && resistance<100){resistance-=pierce;}
                PoisonPackets.Mitigate(poisonRate,state.Damage.Elements.PoisonImpactFrames,false,resistance,0,Math.Max(0,pierce),0,0).Commit(body,target,state.NextUtc);
            }
            CombatDamage.Finish(target,body);
        }
        if(state.TargetDefenseDelta<0 && target is{Alive:true,Deleted:false}){TimedEffects.ReduceDefense(target,-state.TargetDefenseDelta,state.NextUtc);}
    }
    internal static BladeMissileSave Snapshot()=>new(Missiles.Values.OrderBy(x=>x.Id).ToArray(),Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray(),_nextId);
    internal static void Restore(BladeMissileSave saved)
    {
        if(saved?.Missiles==null || saved.Leases==null || saved.NextId<0){throw new InvalidDataException("Missing blade missile state");}
        Missiles.Clear();Leases.Clear();_nextId=saved.NextId;
        foreach(var row in saved.Missiles)
        {
            var d=row.Damage;
            if(row.Id<=0 || row.Id>_nextId || !((Serial)row.Owner).IsMobile || !((Serial)row.OriginalOwner).IsMobile || row.Rank is <1 or >255
                || row.Map<0 || row.Map>=Map.Maps.Length || Map.Maps[row.Map]==null || Map.Maps[row.Map]==Map.Internal || row.Z is <-128 or >127
                || row.Position is not{Alive:true,X:>0,Y:>0} || row.Position.X>=Map.Maps[row.Map].Width*2 || row.Position.Y>=Map.Maps[row.Map].Height*2
                || row.Position.Frame!=9 && row.Position.Frame!=10+5*row.Rank || row.NextUtc.Kind!=DateTimeKind.Utc || row.TargetDefenseDelta>0
                || d?.Elements==null || !d.SourcePoisonFrames || d.Min256<0 || d.Max256<d.Min256 || d.Percent< -90 || !d.ValidConversion
                || d.Conversion!=null || d.PhysicalToMagicPercent!=0 || d.LightningJavelinPercent!=null || d.FreezeColdLength || d.SkillAttackPercent!=0
                || d.FireMastery!=0 || d.ColdMastery!=0 || d.LightningMastery!=0 || d.PoisonMastery!=0 || d.MagicMastery!=0
                || !Missiles.TryAdd(row.Id,row)){throw new InvalidDataException("Invalid saved blade missile");}
        }
        foreach(var lease in saved.Leases)
        {
            if(lease.Cast<=0 || lease.Cast>_nextId || !((Serial)lease.Target).IsMobile || lease.UntilUtc.Kind!=DateTimeKind.Utc
                || lease.RetainWithoutFlight || !Leases.TryAdd((lease.Cast,lease.Target),lease)){throw new InvalidDataException("Invalid saved blade contact lease");}
        }
    }
}
