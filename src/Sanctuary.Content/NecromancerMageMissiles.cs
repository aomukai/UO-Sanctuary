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

internal sealed record MageMissilePacket(string Missile,string Element,int Rank,int Minimum256,int Maximum256,int ElementFrames);
internal sealed record MageMissileState(long Id,uint Actor,uint Owner,int Map,int Z,double X,double Y,double Vx,double Vy,
    int Frame,int Frames,int VelocityFp8,MageMissilePacket Packet,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,DateTime NextUtc,string Fault=null);
internal sealed record MageCriticalSeed(uint Actor,uint Owner,ulong Seed);
internal sealed record MageMissileSave(MageMissileState[] Bolts,MageCriticalSeed[] Seeds,long NextId);
public sealed class MageMissileProfiles:GenericPersistence
{
    private MageMissileSave _snapshot;
    public MageMissileProfiles():base("SanctuaryMageMissiles",224){}
    public override void Serialize(){_snapshot=NecromancerMageMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown skeletal mage missile version");}NecromancerMageMissiles.Restore(JsonSerializer.Deserialize<MageMissileSave>(reader.ReadString()));}
}

// Straight NPC spell flights reuse the factory velocity, Q12 direction,
// retained source cells, do1 update and shared state/mitigation services.
// Native room enumeration/terrain and actor seed allocation remain adapters.
internal static class NecromancerMageMissiles
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerMageMissiles));
    private static readonly Dictionary<long,MageMissileState> Bolts=new();
    private static readonly Dictionary<uint,MageCriticalSeed> Seeds=new();
    private static long _nextId;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()=>_ = new MageMissileProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static MageMissilePacket Packet(int element,int rank)
    {
        if(element is <0 or >3 || rank is <1 or >381){throw new NotSupportedException("Unsupported skeletal mage element/derived missile rank");}
        string key="necromage"+(element+1);var skill=new SkillValues(SkillMechanics.For(CombatAbility.RaiseSkeletalMage),_=>rank,_=>0,99);
        var missile=new MissileValues(key,skill);
        if(missile.Number("pSrvDoFunc")!=1 || missile.Number("pSrvHitFunc")!=0 || missile.Number("pSrvDmgFunc")!=0
            ||missile.Number("Size")!=1||missile.Number("CollideType")!=3||missile.Number("CollideKill")!=1||missile.Number("LastCollide")!=1
            ||missile.Number("ToHit")!=0||missile.Number("SrcDamage")!=0||missile.Number("Pierce")!=0||missile.Number("NextHit")!=0
            ||missile.Number("Range")!=30||missile.Number("Vel")!=14||missile.Number("MaxVel")!=14||missile.Number("Accel")!=0||missile.Number("Activate")!=0
            ||missile.Number("MinDamage")!=0||missile.Number("MaxDamage")!=(element==0?1280:0))
        {throw new NotSupportedException("Unsupported current skeletal mage missile route");}
        // Poison's absent HitShift means zero. Its physical min0 makes the
        // unusual physical max1280 lane a no-roll zero in RollDamageValue.
        return new(key,missile.Text("EType"),rank,missile.ElementDamage256(false,0,381),missile.ElementDamage256(true,0,381),missile.ElementFrames());
    }
    internal static long Launch(NecromancerMinion actor,Mobile target,DateTime at)
    {
        if(actor is not{Alive:true,Deleted:false,IsMage:true,ControlMaster:PlayerMobile owner}||target is not{Alive:true,Deleted:false}
            ||actor.Map==null||actor.Map==Map.Internal||target.Map!=actor.Map||!ClassCombat.IsHostile(owner,target)) {return 0;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Mage missile needs UTC launch time");}
        var packet=Packet(actor.MageElement,actor.MageMissileRank);var slow=AmazonSlowMissiles.Capture(actor,true);
        packet=packet with{Minimum256=slow.Damage256(packet.Minimum256),Maximum256=slow.Damage256(packet.Maximum256)};
        int velocity=SlowMissilePayload.VelocityFp8(14,0,packet.Rank,0x21,0,true,slow.VelocityPercent);
        int dx=target.X-actor.X,dy=target.Y-actor.Y;if(dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);long id=checked(++_nextId);
        if(!Seeds.TryGetValue(actor.Serial.Value,out var seed)||seed.Owner!=owner.Serial.Value)
        {Seeds[actor.Serial.Value]=new(actor.Serial.Value,owner.Serial.Value,SourceRandom.InitLow(SeedLow()));}
        var state=new MageMissileState(id,actor.Serial.Value,owner.Serial.Value,actor.Map.MapID,actor.Z,actor.X,actor.Y,vector.X,vector.Y,
            0,30,velocity,packet,slow,SourceRandom.InitLow(SeedLow()),new([new(actor.X*2,actor.Y*2)]),at.AddMilliseconds(40));
        Validate(state);Bolts.Add(id,state);return id;
    }
    private static bool Fighting(MageMissileState s,NecromancerMinion actor,PlayerMobile owner)=>actor is{Alive:true,Deleted:false,IsMage:true}
        &&actor.ControlMaster==owner&&actor.Map?.MapID==s.Map&&owner is{Alive:true,Deleted:false,NetState:not null}
        &&owner.Map==actor.Map&&PersistentMinions.Active(owner,actor)&&actor.Permitted(owner)&&!SpellHelper.IsTown(owner.Location,owner);
    internal static void Tick(DateTime at,bool? allowActions=null)
    {
        foreach(uint actor in Seeds.Keys.Where(id=>World.FindMobile((Serial)id) is not NecromancerMinion{Deleted:false,IsMage:true}).ToArray()){Seeds.Remove(actor);}
        while(true)
        {
            var s=Bolts.Values.Where(x=>x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenByDescending(x=>x.Id).FirstOrDefault();if(s==null){break;}
            try{Advance(s,allowActions);}
            catch(Exception e)
            {
                if(s.Frame+1>=s.Frames){Bolts.Remove(s.Id);}else{Bolts[s.Id]=s with{Frame=s.Frame+1,NextUtc=s.NextUtc.AddMilliseconds(40),Fault=e.Message};}
                Log.Error(e,"Skeletal mage projectile suspended: {Id}",s.Id);
            }
        }
    }
    private static void Advance(MageMissileState s,bool? allowActions)
    {
        var actor=World.FindMobile((Serial)s.Actor) as NecromancerMinion;var owner=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        // Offline/facet/removed-source actors do not fight. The original
        // projectile lifetime still runs; no replay on reconnect/reappearance.
        if(s.Fault!=null||actor is not{Alive:true,Deleted:false,IsMage:true}||actor.ControlMaster!=owner||actor.Map?.MapID!=s.Map
            ||owner is not{Alive:true,Deleted:false}||owner.Map!=actor.Map||!(allowActions??Fighting(s,actor,owner)))
        {if(frame>=s.Frames){Bolts.Remove(s.Id);}else{Bolts[s.Id]=next;}return;}
        var map=actor.Map;bool removed=false;
        int Move()
        {
            next=next with{X=s.X+s.Vx,Y=s.Y+s.Vy,Steps=SourceMissileSteps.Advance(s.X,s.Y,s.Vx,s.Vy,s.Steps)};
            return next.X<0||next.Y<0||next.X>=map.Width||next.Y>=map.Height?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var candidate in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),3))
            {
                if(candidate==actor||candidate is not{Alive:true,Deleted:false,Blessed:false}||Math.Abs(candidate.Z-s.Z)>=16
                    ||!ClassCombat.IsHostile(owner,candidate)||ClassCombat.Friendly(actor,candidate)||!actor.CanBeHarmful(candidate,false)
                    ||SpellHelper.IsTown(candidate.Location,owner)){continue;}
                int size=candidate is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2; // Existing provisional native footprint.
                if(CombatHitboxes.SourceTouches(1,size,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){return candidate;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)=>!map.LineOfSight(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z+12),new((cell.X+1)/2,(cell.Y+1)/2,s.Z+12));
        int Hit(uint serial,bool terminal)
        {
            if(!terminal&&World.FindMobile((Serial)serial) is Mobile target){Impact(actor,target,s);}
            removed=true;return 2;
        }
        BasicMissileUpdate.Run(new(true,true,s.VelocityFp8,s.Frames-s.Frame,2,s.Frames,true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Steps.Cells,
                cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Wall(cell)?0:Find(cell)?.Serial.Value??0,Hit));
        if(removed){Bolts.Remove(s.Id);return;}Bolts[s.Id]=next;
        if(frame%2==0){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),s.Z),map,
            s.Packet.Element=="cold"?0x36E4:s.Packet.Element=="fire"?0x36D4:s.Packet.Element=="pois"?0x36BD:0x3818,2,10,0,0);} // Identified placeholders.
    }
    internal static int Impact(NecromancerMinion actor,Mobile target,MageMissileState s)
    {
        SorceressEffects.MissileHit(actor,target,s.Packet.Missile);
        ulong stream=s.Seed;int raw=WeaponMissileDamage.RollValue(s.Packet.Minimum256,s.Packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref stream,n));
        bool blocked=ClassAvoidance.Missile(target,s.Packet.Missile);
        if(!Seeds.TryGetValue(s.Actor,out var state)||state.Owner!=s.Owner){throw new NotSupportedException("Missing mage intrinsic critical stream");}
        ulong criticalSeed=state.Seed;int crit=NecromancerSummons.Source("necromage","Crit");
        bool critical=NpcEquipmentCritical.Intrinsic(new(0,0,0,crit),()=>SourceRandom.Roll(ref criticalSeed));
        Seeds[s.Actor]=state with{Seed=criticalSeed};if(blocked){return 0;}if(critical){raw=checked(raw*2);}
        actor.DoHarmful(target,true);int before=SkillHealth.Balance256(target);
        if(s.Packet.Element=="pois")
        {
            int resist=NamedElements.Resistance(actor,target,"pois",target.PoisonResistance);
            var poison=PoisonPackets.Mitigate(raw,s.Packet.ElementFrames,target is PlayerMobile,resist,ItemDurationDefenses.PoisonLength(target),0,target is PlayerMobile p?Math.Max(0,ClassGear.Bonus(p,"res-pois-max")):0,0);
            poison.Commit(actor,target,s.NextUtc);SkillHealth.Damage(actor,target,poison.Rate256); // Existing older-source initial-rate bridge.
        }
        else
        {
            var parts=s.Packet.Element switch{"fire"=>new GearDamage(0,raw/256.0,0,0,0),"cold"=>new(0,0,raw/256.0,0,0),"ltng"=>new(0,0,0,raw/256.0,0),_=>throw new NotSupportedException("Unsupported mage element")};
            var damage=SkillCombat.MitigatePrepared(actor,target,parts);
            if(s.Packet.Element=="cold"){ColdStates.Apply(actor,target,s.Packet.ElementFrames,TimedEffects.TickAt(s.NextUtc));}
            int loss=SkillHealth.Damage(actor,target,NamedElements.To256(damage.Total));if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,actor,loss);}CombatDamage.Finish(target,actor);
        }
        return Math.Max(0,before-SkillHealth.Balance256(target));
    }
    private static void Validate(MageMissileState s)
    {
        if(s==null||s.Id<=0||!((Serial)s.Actor).IsMobile||!((Serial)s.Owner).IsMobile||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
            ||s.Z is <-128 or >127||s.Frames!=30||s.Frame is <0 or >=30||s.VelocityFp8 is <0 or >2688||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc>DateTime.MaxValue.AddMilliseconds(-1200)
            ||!double.IsFinite(s.X)||!double.IsFinite(s.Y)||!double.IsFinite(s.Vx)||!double.IsFinite(s.Vy)||s.X<0||s.Y<0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height
            ||s.Slow.VelocityPercent is <0 or >100||s.Slow.DamagePercent is <0 or >100||s.Packet is not{Rank:>=1 and <=381,Minimum256:>=0,Maximum256:>=0,ElementFrames:>=0}
            ||s.Packet.Maximum256<s.Packet.Minimum256||s.Packet.Missile is not("necromage1" or "necromage2" or "necromage3" or "necromage4")
            ||s.Packet.Element!=new[]{"pois","cold","fire","ltng"}[s.Packet.Missile[^1]-'1']||s.Steps?.Cells is not{Length:>=1 and <=10})
        {throw new InvalidDataException("Invalid saved skeletal mage missile");}
    }
    internal static MageMissileSave Snapshot()=>new(Bolts.Values.OrderBy(s=>s.Id).ToArray(),Seeds.Values.Where(s=>World.FindMobile((Serial)s.Actor) is NecromancerMinion{Deleted:false,IsMage:true} a&&a.ControlMaster?.Serial.Value==s.Owner).OrderBy(s=>s.Actor).ToArray(),_nextId);
    internal static void Restore(MageMissileSave saved)
    {
        if(saved?.Bolts==null||saved.Seeds==null||saved.NextId<0){throw new InvalidDataException("Missing mage projectile save");}
        Bolts.Clear();Seeds.Clear();_nextId=saved.NextId;
        foreach(var s in saved.Bolts){Validate(s);if(s.Id>_nextId||!Bolts.TryAdd(s.Id,s)){throw new InvalidDataException("Duplicate mage projectile");}}
        foreach(var s in saved.Seeds)
        {if(!((Serial)s.Actor).IsMobile||!((Serial)s.Owner).IsMobile||!Seeds.TryAdd(s.Actor,s)){throw new InvalidDataException("Invalid mage critical seed");}}
    }
}
