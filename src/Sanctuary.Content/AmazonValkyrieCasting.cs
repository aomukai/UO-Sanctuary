// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class AmazonValkyrieCasting
{
    internal static Func<PlayerMobile,Point3D,DateTime,AmazonValkyrieBody> Spawn=AmazonValkyrieBody.CreateInitializedController;
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile actor)
    {
        if(World.Loading||actor is not PlayerMobile owner){return;}var action=ClassCombat.For(owner);
        if(action.PendingAbility==CombatAbility.Valkyrie&&(owner.Map!=action.ActionMap||owner.Location!=action.ActionOrigin)){ClassCombat.Stop(owner);}
    }
    internal static AmazonValkyrieBody[] For(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<AmazonValkyrieBody>()
        .Where(b=>b is{Alive:true,Deleted:false,IsDeadBondedPet:false}&&b.ControlMaster==owner&&b.Prepared?.LearnedCast==true).ToArray();
    internal static bool Plan(PlayerMobile owner,Serial selected,Point3D cached,bool release,out Point3D point,out string issue)
    {
        point=default;issue="There is no available space for Valkyrie there.";
        if(owner is not{Alive:true,Deleted:false}||!AmazonCombat.Available(owner,CombatAbility.Valkyrie)
            ||!DruidSpiritWolfCasting.Destination(owner,selected,cached,release,out point)){return false;}
        try
        {
            var row=SkillMechanics.For(CombatAbility.Valkyrie);var values=SkillMechanics.Values(owner,CombatAbility.Valkyrie);
            if(row.Number("srvdofunc")!=16||row.Number("srvstfunc")!=0||row.Text("summon")!="valkyrie"||row.Text("pettype")!="valkyrie"
                ||row.Text("anim")!="SC"||row.Text("seqtrans")!="SC"||row.Number("InTown")!=1||values.Eval("petmax")!=1)
            {throw new NotSupportedException("Unmapped learned Valkyrie row");}
            if(NpcOriginalItemProcs.Allocator==null||ChainLightning.OriginalNpcAllocator==null||World.Loading)
            {throw new NotSupportedException("Valkyrie needs its original world allocation services");}
            var difficulty=SummonDifficulties.For(owner.Map);var birth=ValkyrieBirthNumbers.Capture(owner,NecromancerSummons.Source("valkyrie","minHP",difficulty)*256);
            AmazonValkyrieBody.Validate(new(owner.Serial.Value,birth,difficulty,TimedEffects.Now.AddMilliseconds(40)));_=ValkyrieStartup.Rates;
            issue=null;return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {issue=e.Message;return false;}
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>AmazonCastClock.Begin(owner,CombatAbility.Valkyrie,selected,point,now,(p,ability,id,where)=>Plan(p,id,where,false,out _,out _));
    internal static bool Release(PlayerMobile owner,DateTime at,out AmazonValkyrieBody created)
    {
        created=null;var action=ClassCombat.For(owner);
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Valkyrie release requires UTC");}
        if(!Plan(owner,action.PendingTarget,action.PendingPoint,true,out var point,out var issue))
        {if(issue!=null){owner.SendMessage(0x22,issue);}return false;}
        int cost=SkillMechanics.Values(owner,CombatAbility.Valkyrie).Mana256;if(!NecromancerEffects.CanPay(owner,cost)){return false;}
        var map=owner.Map;var origin=owner.Location;AmazonValkyrieBody candidate=null;bool paid=false,committed=false;
        try
        {
            candidate=Spawn(owner,point,at);
            if(candidate is not{Alive:true,Deleted:false}||candidate.ControlMaster!=owner||owner is not{Alive:true,Deleted:false}
                ||owner.Map!=map||owner.Location!=origin||!AmazonCombat.Available(owner,CombatAbility.Valkyrie)||candidate.Prepared?.Initialization==null||candidate.Prepared.LearnedCast
                ||candidate.Prepared.EquipmentContactProfile!=ValkyrieEquipmentContact.OriginalProfile||candidate.Prepared.GroundMotion?.StartupProfile!=ValkyrieStartup.Profile
                ||candidate.Map!=map&&!MinionSafeAreas.Parked(candidate)){return false;}
            ValkyrieEquipmentContact.RequireRegistration(candidate);
            if(!NecromancerEffects.Pay(owner,cost)){return false;}paid=true;
            candidate.RestorePrepared(candidate.Prepared with{LearnedCast=true});
            committed=true;created=candidate;
            foreach(var previous in For(owner).Where(b=>b!=candidate).OrderBy(BattleCommand.PetOrder).ThenBy(b=>b.Serial.Value).ToArray()){previous.Delete();}
            owner.PlaySound(0x1E5);candidate.FixedEffect(0x376A,10,16,0x481,0); // Identified summon/audio placeholders.
            return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {if(committed){throw;}owner.SendMessage(0x22,e.Message);return false;}
        finally
        {
            if(!committed){if(candidate is{Deleted:false}){candidate.Delete();}if(paid){NecromancerEffects.AddMana(owner,cost);}}
        }
    }
    public static bool Release(PlayerMobile owner)=>Release(owner,TimedEffects.Now,out _);
}
