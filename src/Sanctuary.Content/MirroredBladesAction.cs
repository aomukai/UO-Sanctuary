// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Complete prepared action candidate. Network availability remains gated;
// one SQ25 event releases primary then copies,not invented extra SQ markers.
internal static class MirroredBladesAction
{
    internal static bool Approved=>true;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    internal sealed class Pending(uint weapon,Map map,Point3D origin,Point3D aim,Serial selected,bool basic,long effect,long until,ActionTiming timing)
    {
        internal readonly uint Weapon=weapon;
        internal readonly Map Map=map;
        internal readonly Point3D Origin=origin,Aim=aim;
        internal readonly Serial Selected=selected;
        internal readonly bool BasicAtEntry=basic;
        internal readonly long EffectAt=effect,Until=until;
        internal readonly ActionTiming Timing=timing;
        internal bool Released,Cancelled,Finished,Delivering,UsedBasic;
        internal int Attempts,Successful,ManaPaid256;
        internal long[] Flights=[];
        internal string Fault;
    }
    internal static bool FlightOwner(PlayerMobile p,int map)=>p is{Alive:true,Deleted:false,NetState.Running:true,Map:not null}
        &&p.Map!=Map.Internal&&p.Map.MapID==map&&!MinionSafeAreas.Contains(p)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock;
    private static bool Actor(PlayerMobile p)=>p?.Map!=null&&FlightOwner(p,p.Map.MapID)&&SkillMechanics.Rank(p,CombatAbility.MirroredBlades)>0&&CombatTiming.Equipped(p)!=null&&ShapeRestrictions.Allowed(p,CombatAbility.MirroredBlades);
    private static bool Aim(PlayerMobile p,Point3D point)=>point.X>=0&&point.Y>=0&&point.X<p.Map.Width&&point.Y<p.Map.Height
        &&p.InRange(point,CombatProtocol.SightRange)&&!Server.Spells.SpellHelper.IsTown(point,p);
    private static bool Enemy(PlayerMobile p,BaseCreature t)=>t is{Alive:true,Deleted:false,Blessed:false}&&t.Map==p.Map&&Math.Abs(t.Z-p.Z)<16
        &&ClassCombat.IsHostile(p,t)&&!Server.Spells.SpellHelper.IsTown(t.Location,p);
    internal static ActionTiming Timing(PlayerMobile p,MirroredBladeValues values)
    {
        var weapon=CombatTiming.Equipped(p);var timing=CombatTiming.Weapon(weapon);
        int advance=CombatTiming.FrameAdvance(timing,CombatTiming.Ias(p),GearEffects.AttackSlowTotal(p),CombatTiming.SkillAttackRate(p)+values.SpeedPercent);
        var plan=MirroredBladesPrepared.Schedule(WeaponAnimationClasses.Equipped(p),advance,0,0);
        return new(plan.Events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame,plan.Events.Single(x=>x.Type==SourceAnimationEvents.ModeChange).Frame);
    }
    internal static bool BeginLearned(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Approved||!WarlockCombat.Available(p,CombatAbility.MirroredBlades)){return false;}
        try{return Begin(p,selected,point,now)!=null;}
        catch(NotSupportedException ex){p.SendMessage(0x22,"Mirrored Blades: "+ex.Message);return false;}
    }
    internal static Pending Begin(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Actor(p)){return null;}var input=MirroredBladesPackets.Capture(p);
        if(selected!=Serial.Zero)
        {
            if(World.FindMobile(selected)is not BaseCreature target||!Enemy(p,target)
                ||input.Kind==MirroredWeaponKind.Melee&&!PlayerMeleeGeometry.Within(p,target,0)){return null;}
            point=target.Location;
        }
        if(point.X==p.X&&point.Y==p.Y){int d=(int)p.Direction&7;int[]dx=[0,1,1,1,0,-1,-1,-1],dy=[-1,-1,0,1,1,1,0,-1];point=new(p.X+dx[d],p.Y+dy[d],p.Z);}
        if(!Aim(p,point)){return null;}
        bool basic=!NecromancerEffects.CanPay(p,input.Skill.Mana256);var timing=basic?CombatTiming.For(p,CombatAbility.BasicAttack):Timing(p,input.Skill);
        if(!SkillCombat.BeginPrepared(p,CombatAbility.MirroredBlades,selected,point,now,(who,_,_,at)=>Aim(who,at),(who,_)=>Actor(who),timing,requireMana:false)){return null;}
        var s=ClassCombat.For(p);var action=new Pending(input.Weapon,p.Map,p.Location,point,selected,basic,s.EffectAt,s.NextAction,timing);s.Mirrored=action;return action;
    }
    internal static void Cancel(PlayerMobile p)
    {
        if(ClassCombat.Existing(p)is not{} s||s.Mirrored is not{} a){return;}a.Cancelled=true;s.Mirrored=null;
        if(s.PendingAbility==CombatAbility.MirroredBlades){s.PendingAbility=CombatAbility.None;}
    }
    private static bool Eligible(PlayerMobile p,Pending a,long now)=>Actor(p)&&p is{Frozen:false,Paralyzed:false,Spell:null}&&AlliedOverlap.Allowed(p)
        &&!HitRecovery.Active(p,now)&&p.Map==a.Map&&p.Location==a.Origin&&CombatTiming.Equipped(p)?.Serial.Value==a.Weapon;
    internal static bool Resolve(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.Mirrored is not{} a||a.Cancelled||a.Finished||a.Delivering){return false;}
        if(s.PendingAbility!=CombatAbility.MirroredBlades||!Eligible(p,a,now)){Cancel(p);return false;}
        if(!a.Released&&now>=a.EffectAt)
        {
            a.Released=true;a.Delivering=true;
            try
            {
                var input=MirroredBladesPackets.Capture(p);a.UsedBasic=a.BasicAtEntry||!NecromancerEffects.CanPay(p,input.Skill.Mana256);
                var target=World.FindMobile(a.Selected)as BaseCreature;
                if(a.Selected==Serial.Zero&&input.Kind==MirroredWeaponKind.Melee)
                {target=SkillCombat.NearbySnapshot<BaseCreature>(p.Map,p.Location,4).Where(t=>Enemy(p,t)&&PlayerMeleeGeometry.Within(p,t,0)&&t.InRange(a.Aim,2))
                    .OrderBy(t=>(long)(t.X-a.Aim.X)*(t.X-a.Aim.X)+(long)(t.Y-a.Aim.Y)*(t.Y-a.Aim.Y)).ThenBy(t=>t.Serial.Value).FirstOrDefault();}
                bool hasMeleeTarget = input.Kind == MirroredWeaponKind.Melee && target != null
                    && Enemy(p, target) && PlayerMeleeGeometry.Within(p, target, 0);
                if (hasMeleeTarget)
                {PlayerWeaponContact.RequireOrdinary(p,target,true);}
                int count=a.UsedBasic?1:input.Skill.TotalAttacks;ulong seed=SourceRandom.InitLow(SeedLow());var packets=new MirroredWeaponPacket[count];
                for(int i=0;i<count;i++){packets[i]=MirroredBladesPackets.Prepare(input,i,MirroredBladesPackets.Critical(input,ref seed));}
                if(input.Kind==MirroredWeaponKind.Melee)
                {
                    if (!a.UsedBasic)
                    {
                        if (!NecromancerEffects.Pay(p, input.Skill.Mana256)) { return false; }
                        a.ManaPaid256 = input.Skill.Mana256;
                        WarlockRitualVisuals.Attached(p, SpellEffectGraphics.MirroredBladesCast, SpellEffectSounds.MirroredBladesRelease);
                        MirroredBladesVisuals.Sweep(p, hasMeleeTarget ? target.Location : a.Aim, 0);
                    }
                    bool contactCuePlayed = false;
                    foreach(var packet in packets)
                    {
                        if(!ReferenceEquals(s.Mirrored,a)||!Eligible(p,a,now)||target==null||!Enemy(p,target)||!PlayerMeleeGeometry.Within(p,target,0)){break;}
                        var contact=MirroredBladesContacts.Melee(p,target,packet,()=>Enemy(p,target)&&PlayerMeleeGeometry.Within(p,target,0),ref seed,a.UsedBasic);
                        a.Attempts++;if((contact.Flags&SourceMeleeResult.Successful)!=0){a.Successful++;}
                        int ordinal = packet.Attack.Ordinal;
                        var point = target.Location;
                        if (!a.UsedBasic && ordinal > 0) { MirroredBladesVisuals.Sweep(p, point, ordinal); }
                        int before = !a.UsedBasic && !target.Hidden && (contact.Flags & SourceMeleeResult.Successful) != 0
                            ? SkillHealth.Balance256(target) : 0;
                        PlayerWeaponContact.Release(p,target,contact,TimedEffects.Now,true);
                        if (before > 0 && (target.Deleted || SkillHealth.Balance256(target) < before))
                        {
                            MirroredBladesVisuals.Hit(a.Map, point, !contactCuePlayed);
                            contactCuePlayed = true;
                        }
                    }
                }
                else
                {
                    var aim=target!=null&&Enemy(p,target)?target.Location:a.Aim;if(!Aim(p,aim)){return false;}
                    long cast=WeaponMissiles.NewCast();var created=new List<long>();var at=TimedEffects.Now;
                    foreach(var packet in packets)
                    {
                        ulong child=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));
                        int pierces=NamedProjectiles.PierceBudget(NamedProjectiles.PierceChance(p),MirroredBladesContacts.Effects);
                        var damage=a.UsedBasic?packet.Damage with{SkillAttackPercent=0}:packet.Damage;
                        long id=WeaponMissiles.Launch(p,CombatAbility.MirroredBlades,new(aim.X-p.X,aim.Y-p.Y,32,0),damage,cast,pierces,24,50,0,
                            missile:input.Kind==MirroredWeaponKind.Arrow?"arrow":input.Kind==MirroredWeaponKind.Bolt?"bolt":"javelin",skillRank:input.Skill.Rank,
                            mirrored:new(input.Skill.Rank,packet.Attack.Ordinal,a.UsedBasic,(int)input.Kind,child),mirroredAt:at.AddMilliseconds(40));
                        if(id==0){foreach(long old in created){WeaponMissiles.RemovePrepared(old);}return false;}created.Add(id);
                    }
                    if(!a.UsedBasic&&!NecromancerEffects.Pay(p,input.Skill.Mana256)){foreach(long id in created){WeaponMissiles.RemovePrepared(id);}return false;}
                    a.ManaPaid256=a.UsedBasic?0:input.Skill.Mana256;
                    a.Flights=created.ToArray();a.Attempts=count;
                    if (!a.UsedBasic)
                    {
                        WarlockRitualVisuals.Attached(p, SpellEffectGraphics.MirroredBladesCast, SpellEffectSounds.MirroredBladesRelease);
                    }
                }
                if (a.UsedBasic) { p.PlaySound(0x238); }
            }
            catch(NotSupportedException ex){a.Fault=ex.Message;Cancel(p);return false;}
            finally{a.Delivering=false;}
        }
        if(now>=a.Until&&ReferenceEquals(s.Mirrored,a)){a.Finished=true;s.Mirrored=null;s.PendingAbility=CombatAbility.None;}
        return true;
    }
}
