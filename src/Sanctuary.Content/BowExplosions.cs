// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record BowExplosion(long Id,uint Owner,int Map,double X,double Y,int Z,int Rank,int Min256,int Max256,DateTime NextUtc);

// The source parent hit callback creates a separate child. It retains the
// parent's rank, reads live owner stats at creation, and rolls one fire-only
// packet when its one-frame child expires on the following missile update.
public static class BowExplosions
{
    private static readonly Dictionary<long,BowExplosion> _pending=new();
    private static long _next;
    internal static void Create(PlayerMobile owner,WeaponFlight parent,DateTime at)
    {
        if(parent.Ability!=CombatAbility.ExplodingArrow || parent.SkillRank<1 || owner.Map==null || owner.Map==Map.Internal
            || parent.X<0 || parent.Y<0 || parent.X>=owner.Map.Width || parent.Y>=owner.Map.Height){return;}
        var values=SkillMechanics.Values(owner,CombatAbility.ExplodingArrow,parent.SkillRank);
        // The original creator also selects critical; fire-only delivery ignores
        // its physical critical flag. Exact source seed inheritance is separate.
        NamedCombat.ProjectileCritical(owner,AmazonBow.LaunchRandom);
        var fire=BowLaunch.ExplosionFire(owner,values);long id=++_next;
        _pending.Add(id,new(id,owner.Serial.Value,owner.Map.MapID,parent.X,parent.Y,parent.Z,parent.SkillRank,fire.FireMin,fire.FireMax,
            at.AddMilliseconds(NecromancerSummons.Source("explodingarrowexp2","Range")*40L)));
    }
    internal static void Cancel(Mobile owner)
    {foreach(long id in _pending.Values.Where(x=>x.Owner==owner.Serial.Value).Select(x=>x.Id).ToArray()){_pending.Remove(id);}}
    internal static void Tick(DateTime at)
    {
        foreach(var blast in _pending.Values.OrderBy(x=>x.NextUtc).ThenBy(x=>x.Id).ToArray())
        {
            if(blast.NextUtc>at){break;}
            _pending.Remove(blast.Id);
            if(World.FindMobile((Serial)blast.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner || !ClassCombat.Enabled(owner)
                || owner.Map==null || owner.Map==Map.Internal || owner.Map.MapID!=blast.Map || SpellHelper.IsTown(owner.Location,owner)){continue;}
            double radius=SkillGeometry.Tiles(NecromancerSummons.Source("explodingarrowexp2","sHitPar1"));
            int fire=WeaponMissileDamage.RollValue(blast.Min256,blast.Max256,0,WeaponMissiles.DamageRoll);
            var center=new Point3D((int)Math.Round(blast.X),(int)Math.Round(blast.Y),blast.Z);
            var targets=new List<Mobile>();
            foreach(var target in owner.Map.GetMobilesInRange<Mobile>(center,(int)Math.Ceiling(radius+1)))
            {
                double dx=target.X-blast.X,dy=target.Y-blast.Y;
                // Source default area filter0x8583 does not request LOS. The
                // existing co-op hostile/Z/town projection remains explicit.
                if(dx*dx+dy*dy<=radius*radius && Math.Abs(target.Z-blast.Z)<16 && ClassCombat.IsHostile(owner,target)
                    && !SpellHelper.IsTown(target.Location,owner)){targets.Add(target);}
            }
            targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
            Effects.SendLocationEffect(center,owner.Map,0x36BD,3); // Identified explosion placeholder.
            foreach(var target in targets){SkillCombat.FireImpact(owner,target,fire);}
        }
    }
    internal static BowExplosion[] Snapshot()=>_pending.Values.OrderBy(x=>x.Id).ToArray();
    internal static void Restore(BowExplosion[] saved)
    {
        _pending.Clear();_next=0;
        foreach(var blast in saved??throw new InvalidDataException("Missing bow explosions"))
        {
            if(blast.Id<=0 || !((Serial)blast.Owner).IsMobile || blast.Map<0 || blast.Rank<1 || blast.Min256<0 || blast.Max256<blast.Min256
                || !double.IsFinite(blast.X) || !double.IsFinite(blast.Y) || blast.NextUtc.Kind!=DateTimeKind.Utc || !_pending.TryAdd(blast.Id,blast))
            {throw new InvalidDataException("Invalid saved bow explosion");}
            _next=Math.Max(_next,blast.Id);
        }
    }
}
