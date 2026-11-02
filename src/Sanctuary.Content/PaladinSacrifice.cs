// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinSacrifice
{
    internal sealed record Prepared(Serial Target,bool Hit,PreparedWeaponImpact Impact,double ElementRoll,long EffectAt);
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static int Cost256(int physical256,int targetLife256,int percent)
        =>(int)Math.Clamp((long)Math.Min(Math.Max(0,physical256),Math.Max(0,targetLife256))*Math.Max(0,percent)/100,0,int.MaxValue);
    public static bool ValidTarget(PlayerMobile owner,Serial selected)=>PaladinVengeance.ValidTarget(owner,selected);
    public static void Initialize()=>ClassMapHooks.LocationChanged+=mobile=>
    {
        if(mobile is PlayerMobile owner&&ClassCombat.Existing(owner) is{} session&&session.SacrificeAttack!=null
            &&(owner.Location!=session.ActionOrigin||owner.Map!=session.ActionMap)){Cancel(owner);}
    };
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} session){return;}
        session.SacrificeAttack=null;if(session.PendingAbility==CombatAbility.Sacrifice){session.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{} session&&session.SacrificeAttack!=null
            &&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}
    }
    private static Prepared Allocate(PlayerMobile owner,Mobile target,SkillValues values,long effectAt)
    {
        // St29 allocates a record even for a miss. AllocCombat prepares the
        // typed damage before Do64 reads its physical lane for the self-cost.
        bool hit=ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)==true;
        double roll=0;PreparedWeaponImpact impact=default;
        if(hit)
        {
            roll=ElementRoll();
            impact=GearEffects.PrepareWeaponImpact(owner,target,roll,Random.Shared,
                GearEffects.Damage(owner,roll,target,(int)values.Eval("calc1"),Roll,Utility.Random));
        }
        return new(target.Serial,hit,impact,roll,effectAt);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!SkillCombat.Begin(owner,CombatAbility.Sacrifice,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id))){return false;}
        var session=ClassCombat.For(owner);
        session.SacrificeAttack=Allocate(owner,World.FindMobile(selected),SkillMechanics.Values(owner,CombatAbility.Sacrifice),session.EffectAt);
        return true;
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.SacrificeAttack;
        if(prepared==null||now<prepared.EffectAt){return false;}
        session.SacrificeAttack=null;
        if(prepared.Target!=selected||!PaladinCombat.Available(owner,CombatAbility.Sacrifice)
            ||World.FindMobile(selected) is not{Alive:true,Deleted:false} target){return false;}
        return Deliver(owner,target,SkillMechanics.Values(owner,CombatAbility.Sacrifice),prepared);
    }
    public static bool Strike(PlayerMobile owner,Mobile target,SkillValues values)
        =>Deliver(owner,target,values,Allocate(owner,target,values,Core.TickCount),false);
    private static bool Deliver(PlayerMobile owner,Mobile target,SkillValues values,Prepared prepared,bool checkContact=true)
    {
        int physical256=prepared.Hit?NamedElements.To256(prepared.Impact.Parts.Physical):0;
        int cost=Cost256(physical256,SkillHealth.Balance256(target),(int)values.Eval("calc2"));
        // Do64 commits direct self damage before DrainItemDurability rechecks
        // contact. Never reapply self resistance or reroll the stored packet.
        SkillHealth.PayLife(owner,cost);
        if(owner is not{Alive:true,Deleted:false}){return true;}
        if(checkContact&&!ValidTarget(owner,target.Serial)){return true;}
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound placeholder.
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        if(!prepared.Hit){return true;}
        GearEffects.Strike(owner,target,prepared.ElementRoll,preparedImpact:prepared.Impact);
        if(owner is{Alive:true,Deleted:false}){owner.FixedEffect(0x37B9,5,5,0x21,0);} // Placeholder; source blood1 art is separately registered.
        return true;
    }
}
