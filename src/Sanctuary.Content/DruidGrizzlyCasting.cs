// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public static class DruidGrizzlyCasting
{
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<PlayerMobile,Point3D,DateTime,ulong,DruidGrizzlyBody> Spawn=(owner,point,at,seed)=>DruidGrizzlyWorld.Create(owner,point,at,seed);
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static void Moved(Mobile mobile)
    {
        if(World.Loading || mobile is not PlayerMobile owner){return;}var action=ClassCombat.For(owner);
        if(action.PendingAbility==CombatAbility.SummonGrizzly && (owner.Map!=action.ActionMap || owner.Location!=action.ActionOrigin)){ClassCombat.Stop(owner);}
    }
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D cached,bool release,out Point3D point)
        =>DruidSpiritWolfCasting.Destination(owner,selected,cached,release,out point);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>Destination(owner,selected,point,false,out _)
            && SkillCombat.Begin(owner,CombatAbility.SummonGrizzly,selected,point,now,(p,ability,id,where)=>Destination(p,id,where,false,out _));
    internal static bool Commit(PlayerMobile owner,Serial selected,Point3D cached,DateTime at,out DruidGrizzlyBody body)
    {
        body=null;
        if(owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false} || at.Kind!=DateTimeKind.Utc || !AlliedOverlap.Allowed(owner)
            || !DruidCombat.Available(owner,CombatAbility.SummonGrizzly) || !Destination(owner,selected,cached,true,out var point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.SummonGrizzly);var row=SkillMechanics.For(CombatAbility.SummonGrizzly);
        if(row.Number("srvdofunc")!=119 || row.Number("InTown")!=1 || row.Text("anim")!="SC" || row.Text("pettype")!="grizzly"
            || row.Text("summon")!="druidbear" || values.Mana256!=40*256 || DruidGrizzlyStats.Limit(values)!=1)
        {throw new NotSupportedException("Unsupported learned Grizzly casting route");}
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        DruidGrizzlyBody created;
        try{created=Spawn(owner,point,at,SourceRandom.InitLow(SeedLow()));}
        catch(Exception e) when(e is ArgumentException or NotSupportedException){return false;}
        if(created==null){return false;}
        if(created.ControlMaster!=owner || created.Prepared?.Controller==null){created.Delete();return false;}
        // Allocation/payment finish on the same server-loop turn. A failed
        // cast must not remove an existing paid bear or change its FIFO order.
        if(!NecromancerEffects.Pay(owner,values.Mana256)){created.Delete();return false;}
        created.RestorePrepared(created.Prepared with{LearnedCast=true});DruidGrizzlies.Enforce(owner);body=created;
        owner.PlaySound(0x1E5); // Identified druid_summon audio placeholder.
        return true;
    }
    public static bool Release(PlayerMobile owner)
    {var action=ClassCombat.For(owner);return Commit(owner,action.PendingTarget,action.PendingPoint,TimedEffects.Now,out _);}
}
