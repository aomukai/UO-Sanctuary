// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared do115 release. The recovery profile is always explicit; learned
// routing awaits the surface-window and Solar current-executable checks.
internal static class DruidRecyclerCasting
{
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<PlayerMobile,CombatAbility,Point3D,DateTime,ulong,RecyclerRecoveryModel,DruidRecyclerBody> Spawn=
        (owner,ability,point,at,seed,model)=>DruidRecyclerWorld.Create(owner,ability,point,at,seed,model);
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.CarrionVine or CombatAbility.SolarCreeper;
    internal static bool Eligible(PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        &&owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}&&AlliedOverlap.Allowed(owner)
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Druid
        &&ClassCreation.Profiles.GetRank(owner.Serial,DruidRecyclerStats.Talent(ability))>0
        &&SkillMechanics.Rank(owner,ability) is >0 and <=255&&ShapeRestrictions.Allowed(owner,ability);
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D cached,bool release,out Point3D point)
        =>DruidCreeperCasting.Destination(owner,selected,cached,release,out point);
    internal static bool Commit(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D cached,DateTime at,
        RecyclerRecoveryModel model,out DruidRecyclerBody body)
    {
        body=null;
        if(!Enum.IsDefined(model)){throw new NotSupportedException("Recycler recovery model must be explicit");}
        if(at.Kind!=DateTimeKind.Utc||!Eligible(owner,ability)||!Destination(owner,selected,cached,true,out var point)){return false;}
        var values=SkillMechanics.Values(owner,ability);var row=SkillMechanics.For(ability);
        int expectedMana=ability==CombatAbility.CarrionVine?10:13+values.Rank;
        if(row.Number("srvdofunc")!=115||row.Number("InTown")!=1||row.Text("anim")!="SC"||row.Text("summode")!="S1"
            ||row.Text("pettype")!="vine"||row.Text("summon")!=DruidRecyclerStats.Source(ability)
            ||values.Eval("petmax")!=1||values.Mana256!=expectedMana*256)
        {throw new NotSupportedException("Unsupported recycler casting route");}
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        DruidRecyclerBody created;
        try{created=Spawn(owner,ability,point,at,SourceRandom.InitLow(SeedLow()),model);}
        catch(Exception e)when(e is ArgumentException or NotSupportedException){return false;}
        if(created==null){return false;}
        if(created.ControlMaster!=owner||created.VineAbility!=ability||created.Prepared?.Controller is not{} controller||controller.RecoveryModel!=model)
        {created.Delete();return false;}
        // Same-loop transaction: a failed allocation/payment never replaces
        // the existing paid vine. Shared category replacement follows payment.
        if(!Eligible(owner,ability)||!NecromancerEffects.Pay(owner,values.Mana256)){created.Delete();return false;}
        DruidVines.Replace(owner,created);body=created;
        owner.PlaySound(0x1E5); // Identified druid_summon audio placeholder.
        return true;
    }
}
