// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public static class DruidRavenCasting
{
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile mobile)
    {
        if(World.Loading || mobile is not PlayerMobile owner){return;}
        var action=ClassCombat.For(owner);
        if(action.PendingAbility==CombatAbility.Raven && (owner.Map!=action.ActionMap || owner.Location!=action.ActionOrigin))
        {ClassCombat.Stop(owner);}
    }
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    // The current row permits town casting. Reuse the native range/LOS
    // projection, but not the travelling-fire adapter's town exclusion or a
    // ground creature's spawn-fit rule: Raven has source footprint zero.
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D cached,bool release,out Point3D point,out Mobile focus)
    {
        point=cached;focus=null;
        if(owner?.Map==null || owner.Map==Map.Internal){return false;}
        if(selected!=Serial.Zero)
        {
            var target=World.FindMobile(selected);
            if(target is{Deleted:false})
            {
                if(target.Map!=owner.Map){return false;}
                point=target.Location;if(target.Alive){focus=target;}
            }
            else if(!release){return false;}
        }
        // A dead registered target retains its position; a deleted target
        // falls back to the original point. Exact current-client fallback and
        // source room/spawn-search behavior remain recorded acceptance gaps.
        return point.X>0 && point.Y>0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
            && Math.Abs(point.Z-owner.Z)<16 && owner.InRange(point,CombatProtocol.TeethRange) && owner.InLOS(point);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!Destination(owner,selected,point,false,out var aim,out _)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.Raven,selected,aim,now,(p,ability,id,where)=>Destination(p,id,where,false,out _,out _));
    }
    public static bool Release(PlayerMobile owner)
    {
        var action=ClassCombat.For(owner);
        if(!DruidCombat.Available(owner,CombatAbility.Raven)
            || !Destination(owner,action.PendingTarget,action.PendingPoint,true,out var point,out var focus)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Raven);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        var current=DruidRavens.For(owner).LastOrDefault();
        var at=TimedEffects.Now;
        if(current?.CanRefresh(owner)==true)
        {
            var target=AimFocus(owner,current,point,focus)??current.CurrentFocus(owner);
            RavenBodyState renewed;
            try{renewed=current.PrepareRefresh(owner,target,at,SourceRandom.InitLow(SeedLow()));}
            catch(NotSupportedException){owner.SendMessage(0x22,"Raven cannot be used in this context yet.");return false;}
            if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
            current.Refresh(owner,renewed);
            DruidRavens.KeepLatest(owner);
            return true;
        }
        var spawn=PersistentMinions.FindPosition(owner,out var nearby)?nearby:owner.Location;
        DruidRavenBody body;
        try{body=DruidRavenBody.CreatePrepared(owner,spawn,at,SourceRandom.InitLow(SeedLow()),enforce:false);}
        catch(NotSupportedException){owner.SendMessage(0x22,"Raven cannot be used in this context yet.");return false;}
        // Allocation must succeed before payment; replacing the flock and
        // commanding it wait until payment succeeds, preserving old birds on
        // any failed cast. All of this runs on the server loop.
        if(!NecromancerEffects.Pay(owner,values.Mana256)){body.Delete();return false;}
        DruidRavens.KeepLatest(owner);
        focus=AimFocus(owner,body,point,focus);
        if(focus!=null){DruidRavens.Focus(owner,focus);}
        DruidSummonVisuals.Arrive(body);
        return true;
    }

    private static Mobile AimFocus(PlayerMobile owner,DruidRavenBody body,Point3D point,Mobile focus)
        =>body.Eligible(owner,focus)?focus:NearClickTargets.Acquire(owner,point,CombatProtocol.TeethRange,
            target=>body.Eligible(owner,target)&&owner.InLOS(target),stableTies:true);
}
