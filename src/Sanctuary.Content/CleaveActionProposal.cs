// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Approved Sanctuary action;cleave-action-approval.json records the explicit
// start68/math/membership/payment adaptation and its tested candidate commit.
internal static class CleaveActionProposal
{
    internal static bool Approved=>true;
    internal static Func<double> HitRoll=Utility.RandomDouble,ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random,PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal sealed class Action(PlayerMobile owner,Serial weapon,Map map,Point3D origin,Point3D aim,Serial selected,
        CleaveValues values,bool basic,long effectAt,long until,ActionTiming timing)
    {
        internal readonly PlayerMobile Owner=owner;
        internal readonly Serial Weapon=weapon,Selected=selected;
        internal readonly Map Map=map;
        internal readonly Point3D Origin=origin,Aim=aim;
        internal readonly CleaveValues Values=values;
        internal readonly bool BasicAtEntry=basic;
        internal readonly long EffectAt=effectAt,Until=until;
        internal readonly ActionTiming Timing=timing;
        internal bool Released,Cancelled,Finished,UsedBasic,Delivering;
        internal int Contacts,Successful,ManaPaid256;
        internal string Fault;
    }
    public static void Configure()
    {void Offline(Mobile m){if(m is PlayerMobile p){Cancel(p);}}EventSink.Disconnected+=Offline;EventSink.Logout+=Offline;}
    public static void Initialize()=>ClassMapHooks.LocationChanged+=m=>
    {if(m is PlayerMobile p&&ClassCombat.Existing(p)?.Cleave is{} a&&(p.Map!=a.Map||p.Location!=a.Origin)){Cancel(p);}};
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner)?.Cleave is not{} action){return;}
        action.Cancelled=true;var session=ClassCombat.For(owner);session.Cleave=null;
        if(session.PendingAbility==CombatAbility.Cleave){session.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(ClassCombat.Existing(owner)?.Cleave is{} a&&(CombatTiming.Equipped(owner)?.Serial!=a.Weapon||!PlayerMeleeGeometry.WarlockMelee(owner))){Cancel(owner);}}
    private static bool Actor(PlayerMobile p)=>p is{Alive:true,Deleted:false}&&ClassCombat.Enabled(p)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(p,CombatAbility.Cleave)>0&&PlayerMeleeGeometry.WarlockMelee(p)&&!MinionSafeAreas.Contains(p);
    private static bool Eligible(Action a,long now)=>Actor(a.Owner)&&a.Owner is{Frozen:false,Paralyzed:false,Spell:null}
        &&AlliedOverlap.Allowed(a.Owner)&&!HitRecovery.Active(a.Owner,now)&&a.Owner.Map==a.Map&&a.Owner.Location==a.Origin
        &&CombatTiming.Equipped(a.Owner)?.Serial==a.Weapon;
    internal static readonly SourceDirectionServices MathRule=new((x,y)=>MathF.Atan2(x,y),(v,min,max)=>
    {float span=max-min,r=(v-min)%span;if(r<0){r+=span;}return r+min;},v=>MathF.Round(v,MidpointRounding.AwayFromZero));
    private static SourceGridPoint Fixed(int x,int y)=>new(unchecked((x<<16)+0x8000),unchecked((y<<16)+0x8000));
    internal static bool InArc(CleaveSweepRequest request,int x,int y)
    {
        if(x==request.X&&y==request.Y){return true;}
        if(request.Start==request.End){return true;}
        int h=SourceDirection64.Calculate(Fixed(request.X,request.Y),Fixed(x,y),MathRule).Heading;
        return request.Start<request.End?h>=request.Start&&h<=request.End:h>=request.Start||h<=request.End;
    }
    internal static ActionTiming Timing(PlayerMobile owner,CleaveValues values)
    {
        var weapon=CombatTiming.Equipped(owner);var w=CombatTiming.Weapon(weapon);
        int advance=CombatTiming.FrameAdvance(w,CombatTiming.Ias(owner),GearEffects.AttackSlowTotal(owner),CombatTiming.SkillAttackRate(owner)+values.SpeedPercent);
        var plan=CleavePrepared.Schedule(PaladinZeal.FamilyCode(w.Family,weapon?.Type==WeaponType.Staff),advance,0,0);
        return new(plan.Events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame,plan.Events.Single(x=>x.Type==SourceAnimationEvents.ModeChange).Frame);
    }
    private static Point3D Facing(PlayerMobile p)
    {int i=(int)p.Direction&7;int[]dx=[0,1,1,1,0,-1,-1,-1],dy=[-1,-1,0,1,1,1,0,-1];return new(p.X+dx[i],p.Y+dy[i],p.Z);}
    private static bool Aim(PlayerMobile owner,Point3D aim)=>owner.Map is not null&&owner.Map!=Map.Internal&&aim.X>=0&&aim.Y>=0
        &&aim.X<owner.Map.Width&&aim.Y<owner.Map.Height&&owner.InRange(aim,CombatProtocol.SightRange);
    internal static Action BeginPrepared(PlayerMobile owner,Serial selected,Point3D aim,long now)
    {
        if(!Actor(owner)){return null;}
        if(selected!=Serial.Zero)
        {
            if(World.FindMobile(selected)is not BaseCreature target||!ClassCombat.IsHostile(owner,target)||!PlayerMeleeGeometry.Within(owner,target,0)){return null;}
            aim=target.Location;
        }
        if(aim.X==owner.X&&aim.Y==owner.Y){aim=Facing(owner);}if(!Aim(owner,aim)){return null;}
        var values=CleavePrepared.Capture(SkillMechanics.Values(owner,CombatAbility.Cleave));bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        var timing=basic?CombatTiming.For(owner,CombatAbility.BasicAttack):Timing(owner,values);
        if(!SkillCombat.BeginPrepared(owner,CombatAbility.Cleave,selected,aim,now,(p,_,_,at)=>Aim(p,at),(p,_)=>Actor(p),timing,requireMana:false)){return null;}
        var session=ClassCombat.For(owner);var action=new Action(owner,CombatTiming.Equipped(owner).Serial,owner.Map,owner.Location,aim,selected,values,basic,session.EffectAt,session.NextAction,timing);
        session.Cleave=action;return action;
    }
    private static bool Recipient(Action a,BaseCreature target,CleaveSweepRequest request)=>target is{Alive:true,Deleted:false}
        &&ClassCombat.IsHostile(a.Owner,target)&&!SpellHelper.IsTown(target.Location,a.Owner)&&Math.Abs(target.Z-a.Origin.Z)<16
        &&PlayerMeleeGeometry.Within(a.Owner,target,0)&&InArc(request,target.X*2,target.Y*2);
    internal static bool Advance(Action action,long now)
    {
        var owner=action.Owner;var session=ClassCombat.For(owner);
        if(action.Cancelled||action.Finished||action.Delivering||!ReferenceEquals(session.Cleave,action)){return false;}
        if(session.PendingAbility!=CombatAbility.Cleave||!Eligible(action,now)){Cancel(owner);return false;}
        if(!action.Released&&now>=action.EffectAt)
        {
            action.Released=true; // Claim before payment or any nested combat callback.
            action.Delivering=true;
            try
            {
                var request=CleaveSweep.PrimaryPrepared(owner,action.Aim,MathRule,action.Values);
                var targets=SkillCombat.NearbySnapshot<BaseCreature>(action.Map,action.Origin,(int)Math.Ceiling(SkillGeometry.Tiles(request.Radius)))
                    .Where(t=>Recipient(action,t,request)).OrderBy(t=>(long)(t.X-owner.X)*(t.X-owner.X)+(long)(t.Y-owner.Y)*(t.Y-owner.Y)).ThenBy(t=>t.Serial.Value).ToArray();
                bool basic=action.BasicAtEntry||!NecromancerEffects.CanPay(owner,action.Values.Mana256);action.UsedBasic=basic;
                if(basic)
                {var preferred=targets.FirstOrDefault(t=>t.Serial==action.Selected)??targets.FirstOrDefault();targets=preferred==null?[]:[preferred];}
                foreach(var target in targets){PlayerWeaponContact.RequireOrdinary(owner,target,true);}
                if(!basic){if(!NecromancerEffects.Pay(owner,action.Values.Mana256)){Cancel(owner);return false;}action.ManaPaid256=action.Values.Mana256;}
                foreach(var target in targets)
                {
                    if(!ReferenceEquals(session.Cleave,action)){break;}
                    if(!Eligible(action,now)){Cancel(owner);break;}
                    if(!Recipient(action,target,request)){continue;}
                    var contact=PlayerWeaponContact.PrepareOrdinary(owner,target,basic?0:action.Values.AttackPercent,basic?0:action.Values.DamagePercent,
                        ()=>Recipient(action,target,request),HitRoll,ElementRoll,PhysicalRoll,PoisonRoll,Effects,allowNative:true);
                    action.Contacts++;if((contact.Flags&SourceMeleeResult.Successful)!=0){action.Successful++;}
                    PlayerWeaponContact.Release(owner,target,contact,TimedEffects.Now,true);
                }
            }
            catch(NotSupportedException e){action.Fault=e.Message;Cancel(owner);return false;}
            finally{action.Delivering=false;}
        }
        if(now>=action.Until&&ReferenceEquals(session.Cleave,action))
        {action.Finished=true;session.Cleave=null;session.PendingAbility=CombatAbility.None;}
        return true;
    }
}
