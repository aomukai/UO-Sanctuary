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

internal sealed record NecromancerSpellFlight(long Id,uint Owner,int Map,int Z,CombatAbility Ability,int Rank,
    double X,double Y,double Vx,double Vy,int VelocityFp8,int Frame,int Frames,int Minimum256,int Maximum256,
    FirestormModifiers Modifiers,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,DateTime NextUtc,uint LastCollider=0,string Fault=null,long Cast=0,uint Flags=33,int Activation=0);
internal sealed record NecromancerSpellLease(long Cast,uint Target,DateTime UntilUtc);
internal sealed record NecromancerSpellSave(NecromancerSpellFlight[] Flights,long NextId,NecromancerSpellLease[] Leases=null);
public sealed class NecromancerSpellProfiles:GenericPersistence
{
    private NecromancerSpellSave _snapshot;
    public NecromancerSpellProfiles():base("SanctuaryNecromancerSpellMissiles",225){}
    public override void Serialize(){_snapshot=NecromancerSpellMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Necromancer spell flight version");}NecromancerSpellMissiles.Restore(JsonSerializer.Deserialize<NecromancerSpellSave>(reader.ReadString()));}
}

// Player spell flights share factory velocity,Q12 vectors,retained source
// cells,do1 update,FP rolls and spell defenses. No weapon AR,critical,leech,
// durability or on-strike proc path is substituted for SrcDam0 spells.
internal static class NecromancerSpellMissiles
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerSpellMissiles));
    private static Dictionary<long,NecromancerSpellFlight> Flights=new();
    private static long _nextId;
    private static Dictionary<(long,uint),NecromancerSpellLease> Leases=new();
    private static bool Teeth(CombatAbility ability)=>ability==CombatAbility.Teeth;
    private static string Missile(CombatAbility ability)=>Teeth(ability)?"teeth":"bonespear";
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()=>_ = new NecromancerSpellProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static void Route(SkillValues values)
    {
        bool teeth=Teeth(values.Ability);string missile=Missile(values.Ability);
        var row=SkillMechanics.For(values.Ability);int M(string field)=>NecromancerSummons.Source(missile,field);
        if(values.Ability is not(CombatAbility.BoneSpear or CombatAbility.Teeth)||values.Rank is <1 or >255||row.SourceId!=(teeth?67:84)
            ||row.Text(teeth?"srvmissilea":"srvmissile")!=missile||row.Text("EType")!="mag"||row.Number("SrcDam")!=0
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=0||M("pSrvDmgFunc")!=0
            ||M("Vel")!=(teeth?16:24)||M("MaxVel")!=(teeth?16:24)||M("Accel")!=0||M("Range")!=(teeth?50:40)||M("Size")!=1||M("CollideType")!=3
            ||M("CollideKill")!=(teeth?1:0)||M("LastCollide")!=1||M("ToHit")!=0||M("NextHit")!=(teeth?1:0)||M("NextDelay")!=(teeth?4:0)
            ||M("Pierce")!=0||M("SrcDamage")!=0||M("CanSlow")!=1||M("InitSteps")!=(teeth?1:0)||M("Activate")!=0
            ||M("ReturnFire")!=1||M("GetHit")!=1||M("ResultFlags")!=4)
        {throw new NotSupportedException("Unmapped Necromancer spell missile route");}
        if(teeth&&(values.Eval("calc1") is <2 or >24||values.Eval("calc2")!=0||values.Eval("calc3")!=0))
        {throw new NotSupportedException("Unmapped Teeth fan");}
    }
    internal static (int Min,int Max) Endpoints(SkillValues values,int mastery,SlowMissilePayload slow)
    {Route(values);return DruidFirestorm.Endpoints(values,mastery,slow);}
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id) is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Necromancer?p:null;
    private static NecromancerSpellFlight Prepare(PlayerMobile owner,Point3D point,SkillValues values,DateTime at,FanRay? ray=null,long? id=null,long cast=0)
    {
        Route(values);
        if(Owner(owner.Serial.Value)!=owner||owner.Map==null||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc
            ||point.X<=0||point.Y<=0||point.X>=owner.Map.Width||point.Y>=owner.Map.Height)
        {throw new NotSupportedException("Unsupported spell flight owner/aim");}
        var slow=AmazonSlowMissiles.Capture(owner,true);var ends=Endpoints(values,NamedElements.MasteryPercent(owner,"mag"),slow);
        bool teeth=Teeth(values.Ability);int flags=(int)(ray?.Flags??33);
        int velocity=SlowMissilePayload.VelocityFp8(teeth?16:24,0,values.Rank,flags,0,true,slow.VelocityPercent);
        int dx=ray?.X??point.X-owner.X,dy=ray?.Y??point.Y-owner.Y;if(dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        var s=new NecromancerSpellFlight(id??checked(_nextId+1),owner.Serial.Value,owner.Map.MapID,owner.Z,values.Ability,values.Rank,
            owner.X,owner.Y,vector.X,vector.Y,velocity,0,teeth?50:40,ends.Min,ends.Max,DruidFirestorm.Modifiers(owner),slow,
            SourceRandom.InitLow(SeedLow()),new([new(owner.X*2,owner.Y*2)]),at.AddMilliseconds(40),teeth?owner.Serial.Value:0,Cast:cast,Flags:(uint)flags,Activation:ray?.ActivationFrame??0);Validate(s);return s;
    }
    private static long Commit(NecromancerSpellFlight s){Flights.Add(s.Id,s);_nextId=s.Id;return s.Id;}
    internal static long Create(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)=>Commit(Prepare(owner,point,values,at));
    private static NecromancerSpellFlight[] PrepareFan(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {
        Route(values);if(!Teeth(values.Ability)){throw new NotSupportedException("Expected Teeth fan");}
        long cast=checked(_nextId+1);
        return FanGeometry.Create((point.X-owner.X)*2,(point.Y-owner.Y)*2,(int)values.Eval("calc1"),(int)values.Eval("calc3"),(int)values.Eval("calc2"))
            .Select((ray,index)=>Prepare(owner,point,values,at,ray,checked(cast+index),cast)).ToArray();
    }
    internal static long[] CreateFan(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {var fan=PrepareFan(owner,point,values,at);return fan.Select(Commit).ToArray();}
    internal static bool Release(PlayerMobile owner,Point3D point,SkillValues values)
    {
        NecromancerSpellFlight[] prepared;
        try{prepared=Teeth(values.Ability)?PrepareFan(owner,point,values,TimedEffects.Now):[Prepare(owner,point,values,TimedEffects.Now)];}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}foreach(var s in prepared){Commit(s);}owner.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
    internal static void Cancel(Mobile owner)
    {if(owner!=null){foreach(long id in Flights.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key).ToArray()){Flights.Remove(id);}PruneLeases();}}
    private static void PruneLeases()
    {var casts=Flights.Values.Where(x=>x.Cast>0).Select(x=>x.Cast).ToHashSet();foreach(var key in Leases.Keys.Where(x=>!casts.Contains(x.Item1)).ToArray()){Leases.Remove(key);}}
    // Existing cast-local adapter retained explicitly. The released2.7 rule
    // establishes same-skill isolation,not exact modern supplier/cast lifetime.
    private static bool Delayed(NecromancerSpellFlight s,uint target)=>s.Cast>0&&Leases.TryGetValue((s.Cast,target),out var lease)&&lease.UntilUtc>s.NextUtc;
    internal static NecromancerSpellFlight State(long id)=>Flights.GetValueOrDefault(id);
    internal static void Tick(DateTime at,bool? actions=null)
    {
        while(true)
        {
            var s=Flights.Values.Where(x=>x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenByDescending(x=>x.Id).FirstOrDefault();if(s==null){PruneLeases();return;}
            try{Advance(s,actions);}
            catch(Exception e)
            {
                if(s.Frame+1>=s.Frames){Flights.Remove(s.Id);}else{Flights[s.Id]=s with{Frame=s.Frame+1,NextUtc=s.NextUtc.AddMilliseconds(40),Fault=e.Message};}
                Log.Error(e,"Necromancer spell flight suspended: {Id}",s.Id);
            }
        }
    }
    private static void Advance(NecromancerSpellFlight s,bool? actions)
    {
        var owner=Owner(s.Owner);if(owner==null||owner.Map!=Map.Internal&&owner.Map?.MapID!=s.Map){Flights.Remove(s.Id);return;}
        int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        if(s.Fault!=null||owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {if(frame>=s.Frames){Flights.Remove(s.Id);}else{Flights[s.Id]=next;}return;}
        var map=owner.Map;
        if(SpellHelper.IsTown(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z),owner)){Flights.Remove(s.Id);return;}
        int Move()
        {
            next=next with{X=s.X+s.Vx,Y=s.Y+s.Vy,Steps=SourceMissileSteps.Advance(s.X,s.Y,s.Vx,s.Vy,s.Steps)};
            return next.X<0||next.Y<0||next.X>=map.Width||next.Y>=map.Height?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var candidate in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),3))
            {
                if(candidate.Serial.Value==s.LastCollider||Delayed(s,candidate.Serial.Value)||candidate is not{Alive:true,Deleted:false,Blessed:false}
                    ||Math.Abs(candidate.Z-s.Z)>=16||!ClassCombat.IsHostile(owner,candidate)||!owner.CanBeHarmful(candidate,false)
                    ||SpellHelper.IsTown(candidate.Location,owner)){continue;}
                int size=candidate is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){return candidate;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)=>DruidFirestorm.CellBlocked(map,s.Z,s.X,s.Y,cell.X,cell.Y);
        bool removed=false;
        int Hit(uint serial,bool terminal)
        {
            if(terminal){removed=true;return 2;}
            // Bone Spear's null hit callback leaves hit result2: direct
            // damage with no removal. LastCollide is only the latest identity.
            next=next with{LastCollider=serial};
            if(Teeth(s.Ability)){Leases[(s.Cast,serial)]=new(s.Cast,serial,s.NextUtc.AddMilliseconds(160));}
            if(World.FindMobile((Serial)serial) is{} target){next=next with{Seed=Impact(owner,target,s)};}
            if(Teeth(s.Ability)){removed=true;return 2;}return 1;
        }
        var result=BasicMissileUpdate.Run(new(true,true,s.VelocityFp8,s.Frames-s.Frame,2,s.Frames-s.Activation,true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Steps.Cells,
                cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Wall(cell)?0:Find(cell)?.Serial.Value??0,Hit));
        if(removed||result.Result==2){Flights.Remove(s.Id);return;}Flights[s.Id]=next;
        if(frame%2==0){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),s.Z),map,0x36E4,2);} // Identified Bone Spear placeholder.
    }
    internal static GearDamage Packet(NecromancerSpellFlight s,int magic256)
    {
        // Retain the approved shared spell-potion additions; these are
        // separate from the D2 SrcDam0 packet and introduce no weapon stats.
        var m=s.Modifiers;double magic=magic256/256.0,added=magic*m.Potion/100;
        return new(0,added*Math.Max(0,100+m.FireMastery)/100,added*Math.Max(0,100+m.ColdMastery)/100,
            added*Math.Max(0,100+m.LightningMastery)/100,added*Math.Max(0,100+m.PoisonMastery)/100,Magic:magic);
    }
    internal static ulong Impact(PlayerMobile owner,Mobile target,NecromancerSpellFlight s)
    {
        SorceressEffects.MissileHit(owner,target,Missile(s.Ability));ulong seed=s.Seed;
        int raw=WeaponMissileDamage.RollValue(s.Minimum256,s.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        if(ClassAvoidance.Missile(target,Missile(s.Ability))){return seed;}
        owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,Packet(s,raw));
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,owner,loss);}CombatDamage.Finish(target,owner);return seed;
    }
    private static void Validate(NecromancerSpellFlight s)
    {
        bool teeth=s!=null&&Teeth(s.Ability);int frames=teeth?50:40;
        if(s==null||s.Id<=0||!((Serial)s.Owner).IsMobile||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
            ||s.Z is <-128 or >127||s.Ability is not(CombatAbility.BoneSpear or CombatAbility.Teeth)||s.Rank is <1 or >255||s.Frame<0||s.Frame>=frames||s.Frames!=frames
            ||s.Cast<0||teeth&&(s.Cast<=0||s.Cast>s.Id||s.Flags!=2080||s.Activation!=0)||!teeth&&(s.Cast!=0||s.Flags!=33||s.Activation!=0)
            ||s.Minimum256<0||s.Maximum256<s.Minimum256||s.VelocityFp8<0||s.VelocityFp8>(teeth?3072:4608)||s.Slow.VelocityPercent is <0 or >100||s.Slow.DamagePercent is <0 or >100
            ||s.Modifiers==null||s.Modifiers.Potion<0||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc>DateTime.MaxValue.AddMilliseconds(-2000)
            ||!double.IsFinite(s.X)||!double.IsFinite(s.Y)||!double.IsFinite(s.Vx)||!double.IsFinite(s.Vy)||Math.Abs(s.Vx)>.5625||Math.Abs(s.Vy)>.5625
            ||s.X<0||s.Y<0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height||s.LastCollider!=0&&!((Serial)s.LastCollider).IsMobile
            ||s.Steps?.Cells is not{Length:>=1 and <=10}||s.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[s.Map].Width*2||c.Y>=Map.Maps[s.Map].Height*2))
        {throw new InvalidDataException("Invalid saved Necromancer spell flight");}
    }
    internal static NecromancerSpellSave Snapshot()=>new(Flights.Values.OrderBy(x=>x.Id).ToArray(),_nextId,Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray());
    internal static void Restore(NecromancerSpellSave saved)
    {
        if(saved?.Flights==null||saved.NextId<0){throw new InvalidDataException("Missing Necromancer spell save");}
        var prepared=new Dictionary<long,NecromancerSpellFlight>();
        foreach(var s in saved.Flights){Validate(s);if(s.Id>saved.NextId||!prepared.TryAdd(s.Id,s)){throw new InvalidDataException("Duplicate spell flight");}}
        var leases=new Dictionary<(long,uint),NecromancerSpellLease>();var casts=prepared.Values.Where(x=>x.Cast>0).Select(x=>x.Cast).ToHashSet();
        foreach(var lease in saved.Leases??[])
        {
            if(lease==null||!casts.Contains(lease.Cast)||!((Serial)lease.Target).IsMobile||lease.UntilUtc.Kind!=DateTimeKind.Utc
                ||!leases.TryAdd((lease.Cast,lease.Target),lease)){throw new InvalidDataException("Invalid saved Necromancer hit-delay lease");}
        }
        Flights=prepared;Leases=leases;_nextId=saved.NextId;
    }
}
