// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record AbyssState(long Id,uint Owner,int Map,Point3D Point,AbyssBirth Birth,ulong Seed,bool Terminal,DateTime CreatedUtc,DateTime NextUtc,DateTime UntilUtc);
internal readonly record struct AbyssAdvance(int Pulses,int Pulled,int Contacts,int Damage256,int Terminals,string Fault=null);
// Approved Sanctuary mapping for unavailable do39. Shared ground launch, entropy-root
// identity/persistence, area recipients, forced movement and magic receiver are
// reused;modern callback parity remains explicitly adapted.
internal static class AbyssWorld
{
    internal static bool Approved=>true;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(AbyssWorld));
    private static Dictionary<long,AbyssState> Fields=new();
    private static HashSet<long> Loaded=new();
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    internal static AbyssState[] Snapshot()=>Fields.Values.OrderBy(n=>n.Id).ToArray();
    internal static AbyssState[] For(Mobile p)=>Snapshot().Where(n=>n.Owner==p.Serial.Value).ToArray();
    internal static void Clear(Mobile p){if(p==null){return;}foreach(var n in For(p)){Fields.Remove(n.Id);Loaded.Remove(n.Id);}}
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id)is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock?p:null;
    internal static bool Aim(PlayerMobile p,Serial selected,ref Point3D point)
    {
        if(p is not{Map:not null}||Owner(p.Serial.Value)!=p||!MiasmaWorld.Fighting(p,p.Map.MapID)||!ShapeRestrictions.Allowed(p,CombatAbility.Abyss)){return false;}
        if(selected!=Serial.Zero)
        {if(World.FindMobile(selected)is not BaseCreature t||!MiasmaWorld.Enemy(p,t,p.Z)){return false;}point=t.Location;}
        return SigilActors.TargetPoint(p,point,out point)&&p.Map.LineOfSight(new Point3D(p.X,p.Y,p.Z+12),new Point3D(point.X,point.Y,point.Z+12));
    }
    private sealed class Launch(PlayerMobile p,Point3D point,AbyssBirth birth,DateTime at):GroundLaunchServices
    {
        internal long Created;
        internal override bool HasSkill(int skill)=>skill==402;
        internal override int SkillMissile()=>709;
        internal override int MissileCount=>742;
        internal override int ProgressiveMissile(int skill)=>throw new InvalidOperationException("Abyss uses do28");
        internal override bool HasRoom()=>p.Map!=null&&p.Map!=Map.Internal;
        internal override Point2D Coordinates()=>new(point.X*2,point.Y*2);
        internal override bool DestinationRoom(Point2D q)=>HasRoom()&&point.X>=0&&point.Y>=0&&point.X<p.Map.Width&&point.Y<p.Map.Height;
        internal override bool Town()=>Server.Spells.SpellHelper.IsTown(point,p);
        internal override int MissileSize(int missile)=>missile==709?3:throw new NotSupportedException("Unexpected Abyss body");
        internal override bool Blocked(Point2D q,int size,int mask)
        {
            if(size!=3||mask!=5){throw new NotSupportedException("Abyss placement context changed");}
            // Explicit native size3 terrain projection: center and surrounding
            // source subtiles, without rejecting an occupying monster.
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
            {
                var tile=new Point3D((int)Math.Round((q.X+dx)/2.0),(int)Math.Round((q.Y+dy)/2.0),point.Z);
                if(tile.X<0||tile.Y<0||tile.X>=p.Map.Width||tile.Y>=p.Map.Height||LobProjectiles.Barrier(p.Map,tile)
                    ||!p.Map.CanFit(tile,1,false,false,false)){return true;}
            }
            return false;
        }
        internal override int Distance(Point2D q)=>SkillGeometry.SourceDistance(q.X-p.X*2,q.Y-p.Y*2);
        internal override void SetDoFlag(){} // The shared pending action owns its do/released flag.
        internal override uint Random()=>throw new InvalidOperationException("Do28 does not consume the caster launch RNG");
        internal override void SetTargetMemory(uint seed)=>throw new InvalidOperationException("Do28 has no target-memory write");
        internal override void Notify(GroundMissileRequest request,uint seed)=>throw new InvalidOperationException("Do28 uses no do123 notification");
        internal override bool Create(GroundMissileRequest request)
        {
            if(request.Skill!=402||request.Rank!=birth.Numbers.Rank||request.Missile!=709||request.Flags!=1||request.Point!=Coordinates())
            {throw new NotSupportedException("Abyss source factory request changed");}
            long id=MiasmaWorld.NextIdentity;var n=new AbyssState(id,p.Serial.Value,p.Map.MapID,point,birth,SourceRandom.InitLow(SeedLow()),false,
                at,at.AddMilliseconds(480),at.AddMilliseconds(2000));Validate(n);
            if(!NecromancerEffects.Pay(p,birth.Numbers.Mana256)){return false;}
            MiasmaWorld.CommitIdentity(id);Fields.Add(id,n);Created=id;FireGround.Delay(p,CombatAbility.Abyss,at);return true;
        }
    }
    internal static long ReleasePrepared(PlayerMobile p,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){throw new ArgumentException("Abyss clock must be UTC");}
        if(!Aim(p,Serial.Zero,ref point)||!FireGround.Ready(p,CombatAbility.Abyss,at)){return 0;}var b=AbyssBirths.Capture(p);
        if(!NecromancerEffects.CanPay(p,b.Numbers.Mana256)){return 0;}var launch=new Launch(p,point,b,at);
        if(!GroundMissileLaunch.Start14(launch)||!GroundMissileLaunch.Do28(402,b.Numbers.Rank,launch)){return 0;}
        p.PlaySound(SpellEffectSounds.AbyssOpen);
        Effects.SendLocationEffect(point, p.Map, SpellEffectGraphics.AbyssOpen, SpellEffectGraphics.AbyssRefreshDuration);
        return launch.Created;
    }
    internal static void Tick(DateTime at)
    {
        if(World.Loading||!Approved){return;}
        foreach(uint id in Fields.Values.Select(n=>n.Owner).Distinct().ToArray())
        {var r=TickPrepared(at,onlyOwner:id);if(r.Fault!=null){Log.Warning("Abyss refused unsupported context: {Reason}",r.Fault);}}
    }
    private static DateTime NextPulse(AbyssState n,DateTime at)
    {
        long steps=(at-n.CreatedUtc).Ticks/TimeSpan.FromMilliseconds(480).Ticks+1;var next=n.CreatedUtc.AddMilliseconds(steps*480);
        return next<n.UntilUtc?next:n.UntilUtc;
    }
    internal static AbyssAdvance TickPrepared(DateTime at,bool? actions=null,uint? onlyOwner=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Abyss clock must be UTC");}int pulses=0,pulled=0,contacts=0,damage=0,terminals=0;
        foreach(var original in Snapshot().Where(n=>(!onlyOwner.HasValue||n.Owner==onlyOwner)&&n.NextUtc<=at))
        {
            var n=original;
            var p=Owner(n.Owner);if(p==null||at>=n.UntilUtc.AddMilliseconds(80)){Fields.Remove(n.Id);Loaded.Remove(n.Id);continue;}
            bool overdue=Loaded.Remove(n.Id)&&n.NextUtc<at;
            if(overdue||!MiasmaWorld.Fighting(p,n.Map,actions)||Math.Abs(p.Z-n.Point.Z)>=16)
            {
                if(at>=n.UntilUtc||n.Terminal){Fields.Remove(n.Id);}else{Fields[n.Id]=n with{NextUtc=NextPulse(n,at)};}continue;
            }
            if(!n.Terminal&&at>=n.UntilUtc)
            {
                // Source hit11 creates HitSubMissile1 at the parent's origin,
                // carrying skill/rank. With HitPar1=0 it makes no immediate hit.
                n=n with{Terminal=true,NextUtc=n.UntilUtc.AddMilliseconds(40)};Fields[n.Id]=n;if(n.NextUtc>at){continue;}
            }
            try
            {
                if(n.Terminal){Fields.Remove(n.Id);terminals++;}else{Fields[n.Id]=n with{NextUtc=NextPulse(n,at)};pulses++;}
                var targets=SkillCombat.NearbySnapshot<BaseCreature>(p.Map,n.Point,(int)Math.Ceiling(SkillGeometry.Tiles(n.Birth.Numbers.Radius)))
                    .Where(t=>SourceDamageAreas.Recipient(p,t,n.Map,n.Point.X*2,n.Point.Y*2,n.Point.Z,n.Birth.Numbers.Radius,true,n.Point)).OrderBy(t=>t.Serial.Value).ToArray();
                foreach(var t in targets){TaintedFireBallMissiles.RequireOrdinaryRecipient(p,t);}ulong seed=n.Seed;
                foreach(var t in targets)
                {
                    if(Owner(n.Owner)!=p||!MiasmaWorld.Fighting(p,n.Map,actions)||!n.Terminal&&!Fields.ContainsKey(n.Id)){break;}
                    if(!SourceDamageAreas.Recipient(p,t,n.Map,n.Point.X*2,n.Point.Y*2,n.Point.Z,n.Birth.Numbers.Radius,true,n.Point)){continue;}
                    // No pull dice or extra hit/stun packet. Existing forced
                    // movement retains walls,doors,occupancy and frozen checks.
                    if(!n.Terminal&&!NamedCombat.Elite(t)&&(t.X!=n.Point.X||t.Y!=n.Point.Y)&&CombatDisplacement.TryStep(t,t.GetDirectionTo(n.Point))){pulled++;}
                    var raw=AbyssBirths.Roll(n.Birth,n.Terminal,ref seed);contacts++;
                    if(!ClassAvoidance.Missile(t,reaction:true)){damage=(int)Math.Min(int.MaxValue,(long)damage+MiasmaWorld.Deliver(p,t,raw,true,at));}
                }
                if(Fields.ContainsKey(n.Id)){Fields[n.Id]=Fields[n.Id]with{Seed=seed};}
                if (n.Terminal)
                {
                    Effects.SendLocationEffect(n.Point, p.Map, SpellEffectGraphics.AbyssCollapse, SpellEffectGraphics.AbyssFrames);
                    Effects.PlaySound(n.Point, p.Map, SpellEffectSounds.AbyssCollapse);
                }
                else if (Fields.TryGetValue(n.Id, out var refreshed))
                {
                    // Refresh the field at its existing pulse boundary. Shorten the final
                    // sprite's lifetime so it stops with the active field.
                    int duration = Math.Clamp((int)Math.Ceiling((refreshed.NextUtc - at).TotalMilliseconds / 50), 1,
                        SpellEffectGraphics.AbyssRefreshDuration);
                    Effects.SendLocationEffect(n.Point, p.Map, SpellEffectGraphics.AbyssField, duration);
                    Effects.SendLocationEffect(n.Point, p.Map, SpellEffectGraphics.AbyssPull, Math.Min(duration, SpellEffectGraphics.AbyssFrames));
                    SpellAreaSounds.Pulse(p.Serial.Value, SpellEffectSounds.AbyssPull, n.Point, p.Map, at, 420);
                }
            }
            catch(NotSupportedException e){Fields.Remove(n.Id);return new(pulses,pulled,contacts,damage,terminals,e.Message);}
        }
        return new(pulses,pulled,contacts,damage,terminals);
    }
    internal static void Validate(AbyssState n)
    {
        if(n==null||n.Id<=0||!((Serial)n.Owner).IsMobile||n.Map<0||n.Map>=Map.Maps.Length||Map.Maps[n.Map]==null||Map.Maps[n.Map]==Map.Internal
            ||n.Point.X<0||n.Point.Y<0||n.Point.X>=Map.Maps[n.Map].Width||n.Point.Y>=Map.Maps[n.Map].Height||n.Point.Z is <-128 or >127||n.Seed==0
            ||n.CreatedUtc.Kind!=DateTimeKind.Utc||n.CreatedUtc<=DateTime.UnixEpoch||n.NextUtc.Kind!=DateTimeKind.Utc||n.UntilUtc.Kind!=DateTimeKind.Utc
            ||n.UntilUtc!=n.CreatedUtc.AddMilliseconds(2000)||n.NextUtc<=n.CreatedUtc||n.NextUtc>n.UntilUtc.AddMilliseconds(40)
            ||n.Terminal&&n.NextUtc!=n.UntilUtc.AddMilliseconds(40)||!n.Terminal&&n.NextUtc!=n.UntilUtc&&(n.NextUtc-n.CreatedUtc).Ticks%TimeSpan.FromMilliseconds(480).Ticks!=0)
        {throw new InvalidDataException("Invalid Abyss center/terminal stage");}AbyssBirths.Validate(n.Birth);
    }
    internal static void ValidateSnapshot(AbyssState[] fields,long next,IEnumerable<long> used)
    {var ids=used.ToHashSet();foreach(var n in fields){Validate(n);if(n.Id>next||!ids.Add(n.Id)){throw new InvalidDataException("Duplicate/future Abyss ID");}}}
    internal static void RestoreValidated(AbyssState[] fields,bool loaded)
    {Fields=fields.ToDictionary(n=>n.Id);Loaded=loaded?Fields.Keys.ToHashSet():new();}
}
