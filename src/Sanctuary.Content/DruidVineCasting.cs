// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Shared source SC/FCR windup. Recycler vines use approved always-vulnerable
// bodies; Poison Creeper retains its separate poison-transport gate.
internal static class DruidVineCasting
{
    internal sealed record Pending(CombatAbility Ability,RecyclerRecoveryModel? Recovery,DruidPlayerForm Form);
    internal static bool Learned(CombatAbility ability)=>ability is CombatAbility.CarrionVine or CombatAbility.SolarCreeper;
    internal static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
        =>Learned(ability)&&BeginPrepared(owner,ability,selected,point,now,
            ability==CombatAbility.SolarCreeper?RecyclerRecoveryModel.SanctuaryApprovedSolar:RecyclerRecoveryModel.LegacySource);
    internal static bool Release(PlayerMobile owner,CombatAbility ability,long now)
        =>Learned(ability)&&ClassCombat.Existing(owner)?.VineCast?.Ability==ability&&ResolvePrepared(owner,now);
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.PoisonCreeper or CombatAbility.CarrionVine or CombatAbility.SolarCreeper;
    internal static bool Eligible(PlayerMobile owner,CombatAbility ability)=>Handles(ability)&&owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}
        &&ClassCombat.Enabled(owner)&&AlliedOverlap.Allowed(owner)&&owner.Map!=null&&owner.Map!=Map.Internal
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Druid
        &&ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(ability).TalentId)>0
        &&SkillMechanics.Rank(owner,ability) is >0 and <=255&&ShapeRestrictions.Allowed(owner,ability);
    internal static ActionTiming Timing(PlayerMobile owner)=>DruidFormCastClock.Calculate(DruidPlayerForms.State(owner)?.Form.Ability??CombatAbility.None,CombatTiming.Fcr(owner));
    private static void Policy(CombatAbility ability,RecyclerRecoveryModel? model)
    {
        if(!Handles(ability)||ability==CombatAbility.PoisonCreeper&&model!=null
            ||ability!=CombatAbility.PoisonCreeper&&(model==null||!Enum.IsDefined(model.Value)))
        {throw new NotSupportedException("Prepared vine recovery selection must be explicit and appropriate to its species");}
        var row=SkillMechanics.For(ability);
        if(row.Number("srvdofunc")!=115||row.Text("anim")!="SC"||row.Number("InTown")!=1||row.Number("leftskill")!=0
            ||row.Number("rightskill")!=1||row.Text("pettype")!="vine")
        {throw new NotSupportedException("Unsupported shared vine cast binding");}
    }
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static void Moved(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile owner||ClassCombat.Existing(owner) is not{VineCast:not null} action){return;}
        if(owner.Map!=action.ActionMap||owner.Location!=action.ActionOrigin){ClassCombat.Stop(owner);}
    }
    internal static bool BeginPrepared(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now,RecyclerRecoveryModel? recovery)
    {
        Policy(ability,recovery);
        if(!Eligible(owner,ability)||ClassCombat.For(owner).VineCast!=null
            ||!DruidCreeperCasting.Destination(owner,selected,point,false,out _)){return false;}
        if(!SkillCombat.BeginPrepared(owner,ability,selected,point,now,
            (p,skill,id,where)=>DruidCreeperCasting.Destination(p,id,where,false,out _),Eligible,Timing(owner))){return false;}
        ClassCombat.For(owner).VineCast=new(ability,recovery,DruidPlayerForms.State(owner));return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{VineCast:{} pending} action){return;}
        action.VineCast=null;if(action.PendingAbility==pending.Ability){action.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile owner,long now)
    {
        if(ClassCombat.Existing(owner) is not{VineCast:{} pending} action){return false;}
        var ability=pending.Ability;
        if(action.PendingAbility!=ability||!ReferenceEquals(pending.Form,DruidPlayerForms.State(owner))||!Eligible(owner,ability)||owner.Spell!=null||HitRecovery.Active(owner,now)
            ||owner.Location!=action.ActionOrigin||owner.Map!=action.ActionMap
            ||(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=action.PendingWeapon)
        {Cancel(owner);return false;}
        if(now<action.EffectAt){return false;}
        // Consume before allocation/payment, including rejection, so a duplicate
        // action callback cannot create or charge a second summon.
        Cancel(owner);
        return ability==CombatAbility.PoisonCreeper
            ?DruidCreeperCasting.Commit(owner,action.PendingTarget,action.PendingPoint,TimedEffects.Now,out _)
            :DruidRecyclerCasting.Commit(owner,ability,action.PendingTarget,action.PendingPoint,TimedEffects.Now,pending.Recovery.Value,out _);
    }
}
