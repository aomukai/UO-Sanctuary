// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Same candidate/payment/roster transaction as the learned Valkyrie route.
// Original Shadow creation,skills,AI,death and parked lifecycle stay shared.
internal static class AssassinShadowCasting
{
    internal static Func<PlayerMobile,Point3D,DateTime,CombatAbility,AssassinShadowBody> Spawn=AssassinShadowBody.CreateInitializedController;
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.ShadowWarrior or CombatAbility.ShadowMaster;
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile actor)
    {
        if(World.Loading||actor is not PlayerMobile p){return;}var s=ClassCombat.For(p);
        if(Handles(s.PendingAbility)&&(p.Map!=s.ActionMap||p.Location!=s.ActionOrigin)){ClassCombat.Stop(p);}
    }
    internal static AssassinShadowBody[] For(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<AssassinShadowBody>()
        .Where(b=>b is{Alive:true,Deleted:false,IsDeadBondedPet:false}&&b.ControlMaster==owner&&b.Prepared?.LearnedCast==true).ToArray();
    internal static bool Plan(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D cached,bool release,out Point3D point,out string issue)
    {
        point=default;issue="There is no available space for your Shadow there.";
        if(!Handles(ability)||owner is not{Alive:true,Deleted:false}||!AssassinCombat.Available(owner,ability)
            ||!DruidSpiritWolfCasting.Destination(owner,selected,cached,release,out point)){return false;}
        try
        {
            var row=SkillMechanics.For(ability);var values=SkillMechanics.Values(owner,ability);string monster=ability==CombatAbility.ShadowWarrior?"shadowwarrior":"shadowmaster";
            if(row.Number("srvdofunc")!=49||row.Number("srvstfunc")!=0||row.Text("summon")!=monster||row.Text("pettype")!="shadowwarrior"
                ||row.Text("anim")!="SC"||row.Text("seqtrans")!="SC"||row.Number("InTown")!=1||row.Number("localdelay")!=15||values.Eval("petmax")!=1)
            {throw new NotSupportedException("Unmapped learned Shadow row");}
            if(World.Loading||NpcOriginalItemProcs.Allocator==null||ChainLightning.OriginalNpcAllocator==null)
            {throw new NotSupportedException("Shadow needs its original world allocation services");}
            var capture=ShadowOwnerSnapshot.Capture(owner,ability);capture.Validate();_=ShadowInitializer.Roll(ability,SourceRandom.InitLow(1),SummonDifficulties.For(owner.Map));
            issue=null;return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {issue=e.Message;return false;}
    }
    internal static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
        =>Handles(ability)&&SkillCombat.Begin(owner,ability,selected,point,now,(p,a,id,where)=>Plan(p,a,id,where,false,out _,out _),timing:AssassinCastClock.Calculate(CombatTiming.Fcr(owner)));
    internal static bool Release(PlayerMobile owner,CombatAbility ability)=>Release(owner,ability,TimedEffects.Now,out _);
    internal static bool Release(PlayerMobile owner,CombatAbility ability,DateTime at,out AssassinShadowBody created)
    {
        created=null;if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow release requires UTC");}var action=ClassCombat.For(owner);
        if(!FireGround.Ready(owner,ability,at)||!Plan(owner,ability,action.PendingTarget,action.PendingPoint,true,out var point,out var issue))
        {return false;}
        int cost=SkillMechanics.Values(owner,ability).Mana256;if(!NecromancerEffects.CanPay(owner,cost)){return false;}
        var map=owner.Map;var origin=owner.Location;AssassinShadowBody candidate=null;bool paid=false,committed=false;
        try
        {
            candidate=Spawn(owner,point,at,ability);
            if(candidate is not{Alive:true,Deleted:false}||candidate.ControlMaster!=owner||owner is not{Alive:true,Deleted:false}
                ||owner.Map!=map||owner.Location!=origin||!AssassinCombat.Available(owner,ability)||candidate.Prepared is not{LearnedCast:false,GroundMotion:not null}
                ||candidate.Prepared.Capture.Ability!=ability||candidate.Map!=map&&!MinionSafeAreas.Parked(candidate)){return false;}
            _=ShadowEquipmentDefense.Require(candidate,false);_=ShadowEquipmentDefense.Registry(candidate);
            if(!NecromancerEffects.Pay(owner,cost)){return false;}paid=true;
            candidate.RestorePrepared(candidate.Prepared with{LearnedCast=true});committed=true;created=candidate;
            foreach(var previous in For(owner).Where(b=>b!=candidate).OrderBy(BattleCommand.PetOrder).ThenBy(b=>b.Serial.Value).ToArray()){previous.Delete();}
            FireGround.Delay(owner,ability,at);owner.PlaySound(0x1E5);candidate.FixedEffect(0x376A,10,16,0x455,0); // Identified shared summon placeholders.
            return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {if(committed){throw;}owner.SendMessage(0x22,e.Message);return false;}
        finally
        {
            if(!committed){if(candidate is{Deleted:false}){PersistentMinions.Park(candidate);candidate.Delete();}if(paid){NecromancerEffects.AddMana(owner,cost);}}
        }
    }
}
