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

internal sealed record IcePacket(int Count,int InitialRank,int Rank,int Inline256,int Minimum256,int Maximum256,int Frames,int Radius,int ScatterRadius);
internal sealed record IceCube(long Id,uint Owner,int Map,int Rank,double X,double Y,int Z,int Frame,int Minimum256,int Maximum256,
    int Frames,int ColdFrames,int Pierce,SlowMissilePayload Slow,uint LastCollision,DateTime NextUtc,string Fault=null);
internal sealed record IceCubeSave(IceCube[] Cubes,long NextId);
public sealed class IceCubeProfiles:GenericPersistence
{
    private IceCubeSave _snapshot;
    public IceCubeProfiles():base("SanctuaryIceCubes",161){}
    public override void Serialize(){_snapshot=MartialCold.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown ice-cube version");}MartialCold.Restore(JsonSerializer.Deserialize<IceCubeSave>(reader.ReadString()));}
}

// The shared charge action and source cold/freeze state kernels remain separate
// from these Ice-only inline, copied area and stationary missile consumers.
public static class MartialCold
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MartialCold));
    private static readonly Dictionary<long,IceCube> Cubes=new();
    private static long _nextId;
    internal static Func<int,int> DamageRoll=Utility.Random;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()=>_ = new IceCubeProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static IcePacket Capture(PlayerMobile owner,MartialCharge charge)
    {
        if(charge is not{Ability:CombatAbility.BladesOfIce,Count:>=1 and <=3}){return null;}
        int rank=Math.Max(charge.InitialRank,SkillMechanics.Rank(owner,CombatAbility.BladesOfIce));
        return Capture(SkillMechanics.Values(owner,CombatAbility.BladesOfIce,rank),charge.Count,NamedElements.MasteryPercent(owner,"cold"),DamageRoll,charge.InitialRank);
    }
    internal static IcePacket Capture(SkillValues values,int count,int mastery,Func<int,int> roll,int initialRank=0)
    {
        if(values.Ability!=CombatAbility.BladesOfIce || values.Rank is <1 or >255 || count is <1 or >3 || values.Eval("calc1")!=0)
        {throw new NotSupportedException("Unmapped Ice packet context");}
        int Enhance(int value)=>checked((int)((long)value*Math.Max(0,100+mastery)/100));
        int lo=Enhance(values.Damage256(false)),hi=Enhance(values.Damage256(true));
        return new(count,initialRank==0?values.Rank:initialRank,values.Rank,WeaponMissileDamage.RollValue(lo,hi,0,roll),lo,hi,values.DurationFrames,(int)values.Eval("prgcalc2"),(int)values.Eval("prgcalc3"));
    }
    private static SkillValues Current(PlayerMobile owner,IcePacket packet)=>SkillMechanics.Values(owner,CombatAbility.BladesOfIce,
        Math.Max(packet.InitialRank,SkillMechanics.Rank(owner,CombatAbility.BladesOfIce)));
    private static Point3D Center(Mobile target,Point3D fallback,Map map)=>target is{Deleted:false} && target.Map==map?target.Location:fallback;
    internal static bool AreaAllowed(PlayerMobile owner,Mobile target,Point3D center,Map map,int radius)
        =>target.Map==map && FistsFireRelease.InRadius(center,target,radius) && ClassCombat.IsHostile(owner,target)
            && !SpellHelper.IsTown(target.Location,target) && owner.InLOS(target);
    internal static int Impact(PlayerMobile owner,BaseCreature target,int damage256,int coldFrames,int freezeFrames,int? pierce,long tick)
    {
        if(ClassAvoidance.Missile(target)){return 0;}
        owner.DoHarmful(target,true);
        var reduced=NamedElements.Resolve256(owner,target,damage256,"cold",enhance:false,pierceOverride:pierce);
        if(reduced.Absorbed256>0){SkillHealth.Heal(target,reduced.Absorbed256);}
        // Separate source duration lanes survive flat magic damage reduction.
        ColdStates.Apply(owner,target,coldFrames,tick,pierce);
        FreezeStates.Apply(owner,target,freezeFrames,tick,pierce);
        int loss=SkillHealth.Damage(owner,target,reduced.Damage256);CreatureHitRecovery.Apply(target,owner,loss);CombatDamage.Finish(target,owner);return loss;
    }
    internal static void AfterParent(PlayerMobile owner,Mobile target,Point3D fallback,Map map,IcePacket packet,DateTime at)
    {
        if(packet==null || packet.Count<2 || owner is not{Alive:true,Deleted:false} || owner.Map!=map || map==null || map==Map.Internal){return;}
        // Each source callback reads max(stored,current) rank and current stats.
        // Do not retain an item bonus removed by the committed parent attack.
        var values=Current(owner,packet);int mastery=NamedElements.MasteryPercent(owner,"cold");
        int Enhance(int value)=>checked((int)((long)value*Math.Max(0,100+mastery)/100));
        int area=WeaponMissileDamage.RollValue(Enhance(values.Damage256(false)),Enhance(values.Damage256(true)),0,DamageRoll);
        var center=Center(target,fallback,map);int radius=(int)values.Eval("prgcalc2");
        Effects.SendLocationEffect(center,map,0x374A,3,10,0x480,0); // Identified cold-area placeholder.
        foreach(var recipient in map.GetMobilesInRange<BaseCreature>(center,(radius+1)/2))
        {
            // Current filter0x870B adds caster-to-recipient LOS. The shared
            // native allegiance/town/height projection remains explicit.
            if(AreaAllowed(owner,recipient,center,map,radius))
            {Impact(owner,recipient,area,values.DurationFrames,0,null,Core.TickCount);}
        }
        if(packet.Count<3 || !owner.Alive || owner.Deleted || owner.Map!=map){return;}
        values=Current(owner,packet);center=Center(target,fallback,map);mastery=NamedElements.MasteryPercent(owner,"cold");Tables();
        int low=SeedLow(); // One owner draw; the scatter uses its own source LCG.
        foreach(var offset in FistsFireRelease.Scatter((int)values.Eval("prgcalc3"),low))
        {
            double x=center.X+offset.X/2.0,y=center.Y+offset.Y/2.0;
            if(x<0 || y<0 || x>=map.Width || y>=map.Height || SpellHelper.IsTown(new Point3D((int)Math.Round(x),(int)Math.Round(y),center.Z),owner)){continue;}
            var slow=AmazonSlowMissiles.Capture(owner,false);int minimum=Enhance(FireGround.MissileDamage256("bladesoficecubes",values.Rank,0,false));
            int maximum=Enhance(FireGround.MissileDamage256("bladesoficecubes",values.Rank,0,true));
            var cube=new IceCube(checked(++_nextId),owner.Serial.Value,map.MapID,values.Rank,x,y,center.Z,0,slow.Damage256(minimum),slow.Damage256(maximum),
                30,100,ClassGear.Bonus(owner,"pierce-cold"),slow,owner.Serial.Value,at.AddMilliseconds(40));Validate(cube);Cubes.Add(cube.Id,cube);
        }
    }
    private static void Tables()
    {
        int F(string field)=>NecromancerSummons.Source("bladesoficecubes",field);
        if(F("pSrvDoFunc")!=1 || F("pSrvHitFunc")!=0 || F("pSrvDmgFunc")!=10 || F("Range")!=30 || F("Size")!=1 || F("Vel")!=0
            || F("Accel")!=0 || F("CollideType")!=3 || F("CollideKill")!=0 || F("LastCollide")!=1 || F("NextHit")!=0 || F("ToHit")!=0
            || F("ApplyMastery")!=1 || F("SrcDamage")!=0 || F("EMin")!=1 || F("EMax")!=1 || F("ELen")!=100)
        {throw new NotSupportedException("Unmapped Ice cube table route");}
    }
    internal static BaseCreature Recipient(PlayerMobile owner,IceCube cube)
    {
        var map=Map.Maps[cube.Map];var point=new Point3D((int)Math.Round(cube.X),(int)Math.Round(cube.Y),cube.Z);
        foreach(var target in map.GetMobilesInRange<BaseCreature>(point,2))
        {
            if(target.Serial.Value==cube.LastCollision || Math.Abs(target.Z-cube.Z)>=16 || !ClassCombat.IsHostile(owner,target) || SpellHelper.IsTown(target.Location,target)){continue;}
            int size=CreatureProfiles.For(target).Traits.Size??throw new NotSupportedException("Ice cube collider lacks mapped source size");
            if(CombatHitboxes.SourceTouches(1,size,(int)(cube.X*2)-target.X*2,(int)(cube.Y*2)-target.Y*2)){return target;}
        }
        return null;
    }
    internal static void Tick(DateTime at,bool? allowActions=null)
    {
        var queue=new PriorityQueue<IceCube,(DateTime,long)>();foreach(var cube in Cubes.Values){if(cube.NextUtc<=at){queue.Enqueue(cube,(cube.NextUtc,-cube.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!Cubes.TryGetValue(due.Id,out var cube) || cube.NextUtc!=due.NextUtc){continue;}
            try{Advance(cube,allowActions);}
            catch(Exception e){if(cube.Frame+1>=cube.Frames){Cubes.Remove(cube.Id);}else{Cubes[cube.Id]=cube with{Fault=e.Message,Frame=cube.Frame+1,NextUtc=cube.NextUtc.AddMilliseconds(40)};}Log.Error(e,"Ice cube suspended: {Id}",cube.Id);}
            if(Cubes.TryGetValue(cube.Id,out var next) && next.NextUtc<=at){queue.Enqueue(next,(next.NextUtc,-next.Id));}
        }
    }
    private static void Advance(IceCube cube,bool? allowActions)
    {
        int frame=cube.Frame+1;var owner=World.FindMobile((Serial)cube.Owner) as PlayerMobile;
        if(frame>=cube.Frames || owner is not{Alive:true,Deleted:false} || ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Assassin
            || owner.Map==null || owner.Map!=Map.Internal && owner.Map.MapID!=cube.Map){Cubes.Remove(cube.Id);return;}
        var next=cube with{Frame=frame,NextUtc=cube.NextUtc.AddMilliseconds(40)};
        bool active=owner.Map!=Map.Internal && (allowActions??owner.NetState is{Running:true});
        if(active && cube.Fault==null)
        {
            var target=Recipient(owner,cube);
            if(target!=null)
            {
                int damage=WeaponMissileDamage.RollValue(cube.Minimum256,cube.Maximum256,0,DamageRoll);
                // Settle controls at the source event time when catching up.
                long tick=Core.TickCount+(long)(cube.NextUtc-TimedEffects.Now).TotalMilliseconds;
                Impact(owner,target,damage,cube.ColdFrames,cube.ColdFrames,cube.Pierce,tick);next=next with{LastCollision=target.Serial.Value};
            }
            else if(LobProjectiles.Barrier(Map.Maps[cube.Map],new Point3D((int)Math.Round(cube.X),(int)Math.Round(cube.Y),cube.Z))){Cubes.Remove(cube.Id);return;}
            if(frame%5==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(cube.X),(int)Math.Round(cube.Y),cube.Z),Map.Maps[cube.Map],0x374A,2,10,0x480,0);} // Identified stationary cube placeholder.
        }
        Cubes[cube.Id]=next;
    }
    private static void Validate(IceCube cube)
    {
        if(cube==null || cube.Id<=0 || !((Serial)cube.Owner).IsMobile || cube.Map<0 || cube.Map>=Map.Maps.Length || Map.Maps[cube.Map]==null || Map.Maps[cube.Map]==Map.Internal
            || cube.Rank is <1 or >255 || !double.IsFinite(cube.X+cube.Y) || cube.X<0 || cube.Y<0 || cube.X>=Map.Maps[cube.Map].Width || cube.Y>=Map.Maps[cube.Map].Height
            || cube.Z is <-128 or >127 || cube.Frame<0 || cube.Frame>=30 || cube.Frames!=30 || cube.ColdFrames!=100 || cube.Minimum256<0 || cube.Maximum256<cube.Minimum256
            || cube.Slow.VelocityPercent is <0 or >100 || cube.Slow.DamagePercent is <0 or >100 || cube.NextUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Invalid saved Ice cube");}
    }
    internal static IceCubeSave Snapshot()=>new(Cubes.Values.OrderBy(x=>x.Id).ToArray(),_nextId);
    internal static void Restore(IceCubeSave save)
    {
        if(save?.Cubes==null || save.NextId<0){throw new InvalidDataException("Missing Ice cubes");}var cubes=new Dictionary<long,IceCube>();
        foreach(var cube in save.Cubes){Validate(cube);if(cube.Id>save.NextId || !cubes.TryAdd(cube.Id,cube)){throw new InvalidDataException("Duplicate Ice cube");}}
        Cubes.Clear();foreach(var cube in cubes){Cubes.Add(cube.Key,cube.Value);}_nextId=save.NextId;
    }
}
