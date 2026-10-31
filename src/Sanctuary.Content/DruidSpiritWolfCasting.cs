// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public static class DruidSpiritWolfCasting
{
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile mobile)
    {
        if(World.Loading || mobile is not PlayerMobile owner){return;}
        var action=ClassCombat.For(owner);
        if(action.PendingAbility==CombatAbility.SummonSpiritWolf && (owner.Map!=action.ActionMap || owner.Location!=action.ActionOrigin))
        {ClassCombat.Stop(owner);}
    }
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    // Shared native target/range/LOS projection. Wolf needs ground fit unlike
    // Raven. The source retries failed placement with spawn parameter4; the
    // deterministic two-native-tile perimeter below is an explicit adapter,
    // not a claim about source room/collision search order.
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D cached,bool release,out Point3D point)
    {
        if(!DruidRavenCasting.Destination(owner,selected,cached,release,out var aim,out _)){point=default;return false;}
        return Placement(owner,aim,out point);
    }
    // Shared native ground-pet search adapter; item-targeted summons retain
    // their actual item aim separately from this available spawn position.
    internal static bool Placement(PlayerMobile owner,Point3D aim,out Point3D point)
    {
        bool Legal(Point3D p)=>p.X>0 && p.Y>0 && p.X<owner.Map.Width && p.Y<owner.Map.Height
            && Math.Abs(p.Z-owner.Z)<16 && owner.InRange(p,CombatProtocol.TeethRange) && owner.InLOS(p) && owner.Map.CanSpawnMobile(p);
        if(Legal(aim)){point=aim;return true;}
        for(int radius=1;radius<=2;radius++)
        for(int x=-radius;x<=radius;x++)
        for(int y=-radius;y<=radius;y++)
        {
            if(Math.Abs(x)!=radius && Math.Abs(y)!=radius){continue;}
            var trial=new Point3D(aim.X+x,aim.Y+y,aim.Z);
            if(trial.X<=0 || trial.Y<=0 || trial.X>=owner.Map.Width || trial.Y>=owner.Map.Height){continue;}
            if(Legal(trial)){point=trial;return true;}
            int z=owner.Map.GetAverageZ(trial.X,trial.Y);
            if(Math.Abs(z-aim.Z)<16 && Legal(new(trial.X,trial.Y,z))){point=new(trial.X,trial.Y,z);return true;}
        }
        point=default;return false;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>Destination(owner,selected,point,false,out _)
            && SkillCombat.Begin(owner,CombatAbility.SummonSpiritWolf,selected,point,now,(p,ability,id,where)=>Destination(p,id,where,false,out _));
    public static bool Release(PlayerMobile owner)
    {
        var action=ClassCombat.For(owner);
        if(!DruidCombat.Available(owner,CombatAbility.SummonSpiritWolf)
            || !Destination(owner,action.PendingTarget,action.PendingPoint,true,out var point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.SummonSpiritWolf);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        DruidSpiritWolfBody body;
        try
        {
            body=DruidSpiritWolfWorld.Create(owner,point,TimedEffects.Now,SourceRandom.InitLow(SeedLow()));
        }
        catch(NotSupportedException){owner.SendMessage(0x22,"Spirit Wolf cannot be used in this context yet.");return false;}
        // The server loop cannot interleave a world tick before payment.
        // Never replace an existing wolf on failed allocation or payment.
        if(!NecromancerEffects.Pay(owner,values.Mana256)){body.Delete();return false;}
        body.RestorePrepared(body.Prepared with{LearnedCast=true});DruidSpiritWolves.Enforce(owner);
        owner.PlaySound(0x1E5); // Identified druid_summon audio placeholder.
        return true;
    }
}
