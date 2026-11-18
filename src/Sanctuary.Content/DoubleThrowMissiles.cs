// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

public sealed record PlayerThrowSource(uint Weapon,int ArmorDelta);
internal sealed record ThrowingMissileCatalog(string ReferenceVersion,Dictionary<string,string> SourceHashes,
    Dictionary<string,string> Weapons,Dictionary<string,Dictionary<string,int>> Missiles);
internal static class DoubleThrowMissiles
{
    internal const string Profile="source-double-throw-ordinary-missile-v1";
    private static readonly Lazy<ThrowingMissileCatalog> Catalog=new(()=>
    {
        var rows=BalanceCatalog.Read<ThrowingMissileCatalog>("double-throw-missiles");
        if(rows.Length!=1||rows[0] is not{ReferenceVersion:"3.3.93847",Weapons.Count:30,Missiles.Count:5,SourceHashes.Count:2}){throw new InvalidDataException("Invalid ordinary throwing catalog");}
        foreach(var (key,r) in rows[0].Missiles)
        {
            if(key is not("javelin" or "throwaxe" or "throwknife" or "glaive" or "pilum")||r["Range"]!=(key is "throwaxe" or "throwknife"?20:40))
            {throw new NotSupportedException("Unqualified throwing missile lifetime");}
            foreach(var (field,value) in new[]{("Vel",24),("VelLev",0),("Size",1),("Activate",0),("ToHit",1),("SrcDamage",128),("pSrvDoFunc",1),("pSrvHitFunc",0),("pSrvDmgFunc",0),("LastCollide",1),("NextHit",0),("NextDelay",0),("Pierce",1),("CanSlow",1),("GetHit",1)})
            {if(!r.TryGetValue(field,out int actual)||actual!=value){throw new NotSupportedException("Unqualified throwing missile field "+field);}}
        }
        if(rows[0].Weapons.Values.Any(v=>!rows[0].Missiles.ContainsKey(v))){throw new InvalidDataException("Unknown weapon missile mapping");}return rows[0];
    });
    internal static int HitClass(string missile)=>Catalog.Value.Missiles.TryGetValue(missile,out var row)&&row.TryGetValue("HitClass",out int value)&&value>0?value:throw new NotSupportedException("Missing pinned throwing missile hit class");
    internal static PreparedMonsterDamageResult Contact(PlayerMobile owner,BaseCreature target,WeaponFlight flight,GearDamage damage,int rate256)
        =>PlayerWeaponMissileContact.Release(owner,target,flight,damage,rate256,HitClass(flight.Missile));
    internal static string Missile(BaseWeapon weapon)=>ClassGear.Base(weapon) is{} basis&&Catalog.Value.Weapons.TryGetValue(basis.Code,out string missile)
        ?missile:throw new NotSupportedException("This throwable has no pinned ordinary missile mapping");
    internal static void Require(PlayerMobile owner)
    {_=Missile(CombatTiming.Equipped(owner));WeaponStartEffects.Require(owner);}
    internal static bool Valid(WeaponFlight f)=>f is{Ability:CombatAbility.DoubleThrow,MechanicalProfile:Profile,Flags:33,Activation:0,NextDelay:0,HostileSpell:false,Npc:null,Mirrored:null,Guided:null,PathChanged:false}
        &&f.Slow is{VelocityPercent:>=0 and <=100,DamagePercent:>=0 and <=100}&&f.SourceSteps?.Cells is{Length:>=1 and <=10}
        &&f.Damage is{SourcePoisonFrames:true,Conversion:null,PhysicalToMagicPercent:0,LightningJavelinPercent:null,FreezeColdLength:false}&&f.Damage.SkillAttackPercent==10+f.SkillRank*10
        &&f.Throwing is{} t&&((Serial)t.Weapon).IsItem&&t.ArmorDelta<=0&&t.ArmorDelta!=int.MinValue&&f.SkillRank is >=1 and <=255
        &&f.Missile!=null&&Catalog.Value.Missiles.TryGetValue(f.Missile,out var row)&&f.Frames==row["Range"]
        &&f.Damage.Elements!=null;
    internal static long Launch(PlayerMobile owner,Point3D aim,ThrowRelease released,DateTime at)
    {
        Require(owner);var weapon=CombatTiming.Equipped(owner);string missile=Missile(weapon);var row=Catalog.Value.Missiles[missile];
        var values=SkillMechanics.Values(owner,CombatAbility.DoubleThrow);var packet=BowLaunch.Capture(owner,CombatAbility.DoubleThrow,released.Critical,preparedValues:values);
        var slow=AmazonSlowMissiles.Capture(owner,true);packet=BladeFuryMissiles.Scale(packet,slow);
        return WeaponMissiles.Launch(owner,CombatAbility.DoubleThrow,new(aim.X-owner.X,aim.Y-owner.Y,33,0),packet,WeaponMissiles.NewCast(),
            released.Pierces,row["Vel"],row["Range"],0,missile,values.Rank,slow:slow,mechanicalProfile:Profile,
            throwing:new(weapon.Serial.Value,ClassGear.Bonus(owner,"dmg-ac")),throwingAt:at);
    }
    internal static (double X,double Y) Velocity(FanRay ray,int rank,SlowMissilePayload slow)
    {
        int dx=ray.X,dy=ray.Y;if(dx==0&&dy==0){dx=dy=1;}
        return MissileVectors.VelocityFp8(dx,dy,SlowMissilePayload.VelocityFp8(24,0,rank,33,0,true,slow.VelocityPercent));
    }
    internal static bool Block(BaseCreature target,bool physical,out bool blocked)
    {
        // Same explicit native fallback as ordinary player contact:there is
        // no invented D2 shield on an unclassified UO actor.
        if(target is not EncounterCreature&&CreatureProfiles.Definition(target).Reference==null){blocked=false;return true;}
        return SourceBlocking.TryBlock(target,physical,out blocked);
    }
    internal static bool? Hit(PlayerMobile p,Mobile t,int percent,Func<double> roll)
        =>ClassStats.CheckHit(p,t,percent-WeaponMasteries.Bonus(p,CombatTiming.Equipped(p),1),roll,projectile:true);
}
