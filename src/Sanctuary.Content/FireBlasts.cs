// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record FireBomb(long Id,uint Owner,int Map,int Rank,double X,double Y,int Z,double Vx,double Vy,
    int Remaining,bool Ground,DateTime NextUtc,int Min256=0,int Max256=0,int Potion=0,
    int FireMastery=0,int ColdMastery=0,int LightningMastery=0,int PoisonMastery=0);

public sealed class FireBlastProfiles:GenericPersistence
{
    private FireBomb[] _snapshot;
    public FireBlastProfiles():base("SanctuaryFireBlast",153){}
    public override void Serialize(){_snapshot=FireBlasts.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Fire Blast version");}FireBlasts.Restore(JsonSerializer.Deserialize<FireBomb[]>(reader.ReadString()));}
}

public static class FireBlasts
{
    private static readonly Dictionary<long,FireBomb> _bombs=new();
    private static long _next;
    internal static Func<int,int> Roll=Utility.Random;
    public static FireBlastProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static ActionTiming Timing(int advance)=>LobProjectiles.Timing(advance);
    internal static ActionTiming Timing(PlayerMobile owner)=>LobProjectiles.Timing(owner);
    internal static bool Valid(PlayerMobile owner,Point3D point)=>LobProjectiles.Valid(owner,point);
    private static bool Aim(PlayerMobile owner,Serial selected,ref Point3D point)=>LobProjectiles.Aim(owner,selected,ref point);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!Aim(owner,selected,ref point)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.FireBlast,selected,point,now,(p,_,_,at)=>Valid(p,at),timing:Timing(owner));
    }
    internal static int Lifetime(int dx,int dy)=>LobProjectiles.Lifetime(dx,dy);
    // The legacy getters overflow signed32 in extreme synergy combinations.
    // Modern arithmetic is unknown there; retain an explicit unsupported boundary.
    internal static bool Supported(SkillValues values)
    {
        long basis=(long)SkillValues.Tier(SkillMechanics.For(CombatAbility.FireBlast).Number("EMax"),values.Rank,i=>SkillMechanics.For(CombatAbility.FireBlast).Number("EMaxLev"+i))<<7;
        return basis*values.Eval("EDmgSymPerCalc")<=int.MaxValue;
    }
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        var point=ClassCombat.For(owner).PendingPoint;
        if(!AssassinCombat.Available(owner,CombatAbility.FireBlast) || !Aim(owner,selected,ref point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.FireBlast);
        if(!Supported(values)){owner.SendMessage(0x22,"Fire Blast's extreme rank/synergy combination is not supported yet.");return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        int dx=(point.X-owner.X)*2,dy=(point.Y-owner.Y)*2;if(dx==0 && dy==0){dx=dy=1;}
        var v=MissileVectors.VelocityFp8(dx,dy,LobProjectiles.Velocity);long id=++_next;
        _bombs.Add(id,new(id,owner.Serial.Value,owner.Map.MapID,values.Rank,owner.X,owner.Y,owner.Z,v.X,v.Y,Lifetime(dx,dy),false,TimedEffects.Now.AddMilliseconds(40)));
        owner.PlaySound(0x23B); // Identified throw-audio placeholder; no final FireTrauma art.
        return true;
    }
    private static Point3D Point(FireBomb b)=>new((int)Math.Round(b.X),(int)Math.Round(b.Y),b.Z);
    internal static bool Recipient(PlayerMobile owner,BaseCreature target,FireBomb b)
    {
        bool attackable=target is EncounterCreature?target.CanBeDamaged():SourceAuraTargets.Rule(CreatureProfiles.Definition(target).Reference)?.Attackable==true;
        double dx=target.X-b.X,dy=target.Y-b.Y;
        // Filter8583 has no caster-LOS bit. NOAURA is not checked by this caller.
        return attackable && ClassCombat.IsHostile(owner,target) && 4*(dx*dx+dy*dy)<=25
            && Math.Abs(target.Z-b.Z)<16 && !SpellHelper.IsTown(target.Location,target);
    }
    internal static GearDamage Packet(FireBomb b,int damage256)
    {
        double fire=damage256/256.0,added=fire*b.Potion/100;
        return new(0,fire+added*(100+b.FireMastery)/100,added*(100+b.ColdMastery)/100,
            added*(100+b.LightningMastery)/100,added*(100+b.PoisonMastery)/100);
    }
    internal static void Impact(PlayerMobile owner,FireBomb b)
    {
        var raw=Packet(b,WeaponMissileDamage.RollValue(b.Min256,b.Max256,0,Roll));
        var targets=new List<BaseCreature>();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(Point(b),4)){if(Recipient(owner,target,b)){targets.Add(target);}}
        targets.Sort((a,c)=>a.Serial.CompareTo(c.Serial));
        foreach(var target in targets)
        {
            if(!Recipient(owner,target,b) || ClassAvoidance.Missile(target)){continue;}
            owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,raw);
            int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));CreatureHitRecovery.Apply(target,owner,loss);
        }
        Effects.SendLocationEffect(Point(b),owner.Map,0x36BD,15,10,0x489,0); // Identified explosion placeholder.
    }
    private static FireBomb Land(PlayerMobile owner,FireBomb b,DateTime at)
    {
        if(!LobProjectiles.CanLand(owner,b.X,b.Y)){return null;}
        var values=SkillMechanics.Values(owner,CombatAbility.FireBlast,b.Rank);if(!Supported(values)){return null;}
        int Enhance(int damage)=>(int)Math.Clamp((long)damage+(long)damage*NamedElements.MasteryPercent(owner,"fire")/100,0,int.MaxValue);
        return b with{X=Math.Floor(b.X*2+.5)/2,Y=Math.Floor(b.Y*2+.5)/2,Vx=0,Vy=0,Remaining=5,Ground=true,NextUtc=at.AddMilliseconds(40),
            Min256=Enhance(values.Damage256(false)),Max256=Enhance(values.Damage256(true)),Potion=CombatPotions.Bonus(owner,"magical-penetration"),
            FireMastery=NamedElements.MasteryPercent(owner,"fire"),ColdMastery=NamedElements.MasteryPercent(owner,"cold"),
            LightningMastery=NamedElements.MasteryPercent(owner,"ltng"),PoisonMastery=NamedElements.MasteryPercent(owner,"pois")};
    }
    internal static void Tick(DateTime now)
    {
        var queue=new PriorityQueue<FireBomb,(DateTime,long)>();
        foreach(var b in _bombs.Values){if(b.NextUtc<=now){queue.Enqueue(b,(b.NextUtc,b.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!_bombs.TryGetValue(due.Id,out var b) || b.NextUtc!=due.NextUtc){continue;}
            var owner=World.FindMobile((Serial)b.Owner) as PlayerMobile;
            if(owner is not{Deleted:false} || owner.Map==null || ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Assassin
                || owner.Map!=Map.Internal && owner.Map.MapID!=b.Map)
            {_bombs.Remove(b.Id);continue;}
            if(owner.Map==Map.Internal)
            {if(b.NextUtc.AddMilliseconds((b.Remaining+(b.Ground?0:5))*40L)<=now){_bombs.Remove(b.Id);}continue;}
            FireBomb next;
            if(b.Ground)
            {
                if(b.Remaining<=1){_bombs.Remove(b.Id);Impact(owner,b);continue;}
                next=b with{Remaining=b.Remaining-1,NextUtc=b.NextUtc.AddMilliseconds(40)};
            }
            else
            {
                var advance=LobProjectiles.Advance(owner.Map,b.X,b.Y,b.Z,b.Vx,b.Vy,b.Remaining);
                var moved=b with{X=advance.X,Y=advance.Y};
                next=advance.Landed?Land(owner,moved,b.NextUtc):moved with{Remaining=b.Remaining-1,NextUtc=b.NextUtc.AddMilliseconds(40)};
                if(next!=null){Effects.SendLocationEffect(Point(next),owner.Map,0x36E4,2,10,0x489,0);} // Identified bomb/landing placeholder.
            }
            if(next==null){_bombs.Remove(b.Id);continue;}_bombs[b.Id]=next;
            if(next.NextUtc<=now){queue.Enqueue(next,(next.NextUtc,next.Id));}
        }
    }
    internal static FireBomb[] Snapshot()=>_bombs.Values.OrderBy(b=>b.Id).ToArray();
    internal static void Restore(FireBomb[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Fire Blast state");}_bombs.Clear();_next=0;
        foreach(var b in saved)
        {
            if(b.Id<=0 || !((Serial)b.Owner).IsMobile || b.Rank is <1 or >255 || b.Remaining<1 || b.Remaining>(b.Ground?5:264)
                || b.Map<0 || b.Map>=Map.Maps.Length || Map.Maps[b.Map]==null || !double.IsFinite(b.X+b.Y+b.Vx+b.Vy)
                || b.X<0 || b.Y<0 || b.X>=Map.Maps[b.Map].Width || b.Y>=Map.Maps[b.Map].Height
                || b.Min256<0 || b.Max256<b.Min256 || b.Potion<0 || Math.Abs(b.Vx)>1 || Math.Abs(b.Vy)>1
                || b.Ground && (b.Vx!=0 || b.Vy!=0) || !b.Ground && (b.Min256!=0 || b.Max256!=0 || b.Potion!=0)
                || !_bombs.TryAdd(b.Id,b)){throw new InvalidDataException("Invalid saved Fire Blast");}_next=Math.Max(_next,b.Id);
        }
    }
}
