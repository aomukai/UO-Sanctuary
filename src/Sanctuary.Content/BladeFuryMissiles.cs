// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Prepared player-flight adapter. Learned input stays gated. The AR profile
// retains the older missile mastery exclusion plus the documented current Fury
// bonus; current mastery/slow rounding and full executable acceptance stay scoped.
internal static class BladeFuryMissiles
{
    internal const string Profile="source-player-missile-plus-current-fury-v1";
    internal static void Cancel(Mobile owner)=>WeaponMissiles.CancelBladeFury(owner);
    internal static long Launch(PlayerMobile owner,Point3D aim,int rank,Random random)
    {
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||!ClassCombat.Enabled(owner)
            ||SpellHelper.IsTown(owner.Location,owner)||aim.X<=0||aim.Y<=0||aim.X>=owner.Map.Width||aim.Y>=owner.Map.Height
            ||rank is <1 or >255){return 0;}
        var damage=BladeFuryLaunch.Capture(owner,rank,random);
        var slow=AmazonSlowMissiles.Capture(owner,NecromancerSummons.Source("bladefragment1","CanSlow")!=0);
        damage=Scale(damage,slow);
        return WeaponMissiles.Launch(owner,CombatAbility.BladeFury,new(aim.X-owner.X,aim.Y-owner.Y,32,1),damage,
            WeaponMissiles.NewCast(),0,22,40,0,"bladefragment1",rank,slow:slow,mechanicalProfile:Profile);
    }
    internal static WeaponMissileDamage Scale(WeaponMissileDamage damage,SlowMissilePayload slow)
        =>damage with{Min256=slow.Damage256(damage.Min256),Max256=slow.Damage256(damage.Max256),
            Elements=slow.Scale(damage.Elements),PhysicalSupplement=damage.PhysicalSupplement is{} extra
                ?new(slow.Damage256(extra.Min256),slow.Damage256(extra.Max256)):null};
    internal static (double X,double Y) Velocity(FanRay ray,int rank,SlowMissilePayload slow)
    {
        int x=ray.X,y=ray.Y;if(x==0&&y==0){x=y=1;}
        return MissileVectors.VelocityFp8(x,y,SlowMissilePayload.VelocityFp8(22,0,rank,32,0,true,slow.VelocityPercent));
    }
    internal static int AttackRating(PlayerMobile owner,Mobile target,int skillPercent)
        =>ClassStats.AttackRatingAgainst(owner,target,skillPercent-WeaponMasteries.Bonus(owner,CombatTiming.Equipped(owner),1));
    internal static bool Valid(int rank,int frames,int activation,int nextDelay,int pierces,uint flags,string missile,
        WeaponMissileDamage damage,SlowMissilePayload? slow,string profile)
        =>rank is >=1 and <=255&&frames==40&&activation==1&&nextDelay==0&&pierces==0&&flags==32
            &&missile=="bladefragment1"&&profile==Profile&&slow is{VelocityPercent:>=0 and <=100,DamagePercent:>=0 and <=100}
            &&damage is{Min256:>=0,SourcePoisonFrames:true,PhysicalToMagicPercent:0,Conversion:null,FreezeColdLength:false,LightningJavelinPercent:null}
            &&damage.Max256>=damage.Min256&&damage.Elements!=null&&damage.SkillAttackPercent==rank*10;
    internal static bool Valid(WeaponFlight flight)=>!flight.HostileSpell&&flight.Guided==null&&!flight.PathChanged
        &&flight.SourceSteps?.Cells is{Length:>=1 and <=10}
        &&Valid(flight.SkillRank,flight.Frames,flight.Activation,flight.NextDelay,flight.Pierces,flight.Flags,
            flight.Missile,flight.Damage,flight.Slow,flight.MechanicalProfile);
    internal static bool? Hit(PlayerMobile owner,Mobile target,int skillPercent,Func<double> roll)
        =>ClassStats.CheckHit(owner,target,skillPercent-WeaponMasteries.Bonus(owner,CombatTiming.Equipped(owner),1),roll,projectile:true);
}
