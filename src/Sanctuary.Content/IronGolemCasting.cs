// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Learned item-target route. Unsupported originals fail before windup and
// again at release; a failed candidate returns its exact original. The paid
// old golem is replaced only after allocation and payment both succeed.
internal static class IronGolemCasting
{
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<PlayerMobile,Item,Point3D,DateTime,ulong,IronGolemBody> Spawn=IronGolemWorld.Create;
    internal static bool Plan(PlayerMobile owner,Item original,out Point3D point,out string issue)
    {
        point=default;issue="Select an identified metal item on the ground.";
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            ||profile.Class!=DiabloClass.Necromancer||SkillMechanics.Rank(owner,CombatAbility.IronGolem)<1
            ||owner.Map==null||owner.Map==Map.Internal||original is not{Deleted:false}||original.Map!=owner.Map
            ||!IronGolemItems.Eligible(original,out issue)){return false;}
        point=original.Location;
        if(!original.Visible||!original.Movable||!original.IsAccessibleTo(owner)||!owner.CanSee(original)
            ||!owner.InRange(point,CombatProtocol.TeethRange)||Math.Abs(point.Z-owner.Z)>=16||!owner.InLOS(original))
        {issue="That original item is not accessible within casting range.";return false;}
        if(!DruidSpiritWolfCasting.Placement(owner,original.Location,out point)){issue="There is no safe space for the golem near that item.";return false;}
        try
        {
            var row=SkillMechanics.For(CombatAbility.IronGolem);var values=SkillMechanics.Values(owner,CombatAbility.IronGolem);
            if(row.Number("srvstfunc")!=20||row.Number("srvdofunc")!=57||!string.Equals(row.Text("summon"),"irongolem",StringComparison.OrdinalIgnoreCase)||row.Text("pettype")!="golem"
                ||row.Text("anim")!="SC"||row.Number("InTown")!=1||values.Eval("petmax")!=1)
            {throw new NotSupportedException("Unmapped learned Iron Golem casting row");}
            var recipe=IronGolemBodyRecipes.Capture(owner,original);
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>()){IronGolemBodyRecipes.Build(recipe,difficulty);}
            _=new NpcCombatEvents(new(original,ClassStats.Level(owner)));NecromancerGolems.Limit(owner);
            issue=null;return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {issue=e.Message;return false;}
    }
    internal static bool Release(PlayerMobile owner,Item original,DateTime at,out IronGolemBody created)
    {
        created=null;
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Iron Golem release requires UTC");}
        if(!Plan(owner,original,out var point,out var issue)){owner.SendMessage(0x22,issue??"That item is unavailable.");return false;}
        int cost=SkillMechanics.Values(owner,CombatAbility.IronGolem).Mana256;
        if(!NecromancerEffects.CanPay(owner,cost)){return false;}
        var map=original.Map;var location=original.Location;bool visible=original.Visible,movable=original.Movable;
        IronGolemBody candidate=null;bool committed=false,paid=false;
        try
        {
            candidate=Spawn(owner,original,point,at,SourceRandom.InitLow(SeedLow()));
            if(candidate is not{Alive:true,Deleted:false}||candidate.ControlMaster!=owner||candidate.Prepared?.Controller==null
                ||IronGolemItems.State(candidate)?.Item!=original.Serial.Value||original.Parent!=candidate
                ||owner is not{Alive:true,Deleted:false}){return false;}
            // Native item/ownership callbacks can run during allocation.
            // Recheck current item capabilities and resources before paying.
            _=new NpcCombatEvents(NpcEquipmentCombat.Context(candidate)??throw new NotSupportedException("New Iron Golem lost its original context"));
            if(!NecromancerEffects.Pay(owner,cost)){return false;}paid=true;
            NecromancerGolems.Commit(owner,candidate);committed=true;created=candidate;
            candidate.FixedParticles(0x376A,9,20,5024,EffectLayer.Waist);
            owner.PlaySound(0x1E5); // Identified native summon/audio placeholders.
            return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {owner.SendMessage(0x22,e.Message);return false;}
        finally
        {
            if(!committed)
            {
                if(candidate is{Deleted:false})
                {
                    if(IronGolemItems.State(candidate)!=null){IronGolemItems.ReturnPreparedOriginal(candidate,map,location,visible,movable);}
                    candidate.Delete();
                }
                if(paid){NecromancerEffects.AddMana(owner,cost);}
            }
        }
    }
}
