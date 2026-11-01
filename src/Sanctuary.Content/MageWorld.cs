// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

// One ranged behavior for the learned subtype and original saved Minions.
// Each actor supplies its existing persistence storage; the shared ground
// conductor owns frames, movement, load cancellation and regeneration.
internal sealed class MageWorld(NecromancerMinion body,Func<string> read,Action<string> write)
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageWorld));
    private NecromancerGroundWorld _world;
    private string _lastNativeSize;
    private bool _inCombat;
    internal NecromancerGroundWorld World=>_world??=new(body,MageWorldBody.ProfileId,read,write,()=>{},true);
    internal static MageWorld For(SkeletonGuardian actor)=>actor is MageWorldBody current?current.Ranged:actor is NecromancerMinion{IsMage:true} old?old.LegacyMageWorld:null;
    private static void Require(NecromancerMinion actor)
    {if(!actor.IsMage||GenericPetMetadata.Table.Skill(338)?.Name!="NecromageMissile"){throw new NotSupportedException("Mage world needs pinned NPC338");}}
    internal void Begin(PlayerMobile owner,DateTime at,ulong seed)
    {Require(body);body.Name="Skeletal Mage [placeholder]";World.Initialize(owner,at,()=>NecromancerMageMissiles.SetActorSeed(body,seed));}
    internal static bool TryAdopt(NecromancerMinion actor,DateTime at)
    {
        if(actor is MageWorldBody||actor is not{Alive:true,Deleted:false,IsMage:true,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}
            ||actor.Map==null||actor.Map==Map.Internal||actor.Map!=owner.Map||!PersistentMinions.Active(owner,actor)||!actor.Permitted(owner)){return false;}
        var ranged=actor.LegacyMageWorld;if(ranged.World.State!=null){return true;}Require(actor);
        // Legacy native timers have no saved source-frame phase. Cancel them
        // once and enter neutral idle,without a new summon/emergence or HP write.
        actor.RetireNativeMageActions();ranged.World.Initialize(owner,at,()=>
        {if(NecromancerMageMissiles.ActorSeed(actor)==null){NecromancerMageMissiles.SetActorSeed(actor,SourceRandom.InitLow(NecromancerMageMissiles.SeedLow()));}},adopt:true);
        return true;
    }
    internal void ObserveCombatTarget(bool present){if(!present){_inCombat=false;}}
    internal void CombatStart(PlayerMobile owner){if(_inCombat){return;}_inCombat=true;MagePetBuffs.CombatStart(body,owner);}
    private int Footprint(BaseCreature target)
    {
        if(CreatureProfiles.For(target).Traits.Size is >=0 and <=3 and var size){return size;}string kind=target.GetType().FullName;
        if(_lastNativeSize!=kind){_lastNativeSize=kind;Log.Warning("Mage target {Target} uses provisional native size2; source footprint is unmapped",kind);body.ControlMaster?.SendMessage(0x22,"Some Mage targeting on this creature still uses Sanctuary's existing native footprint.");}
        return 2;
    }
    internal NecromancerPetAiTarget Primary(PlayerMobile owner,IronGolemWorldState state)
    {
        var choice=NpcRangedTargets.Select(body,IronGolemWorld.Cell(state),target=>IronGolemWorld.Eligible(body,owner,target),target=>PrimaryPath(state,target),Footprint);
        if(choice==null){return new(0,0,false);}var target=WorldFind((uint)choice.Candidate.Serial);
        return new((int)choice.Candidate.Serial,choice.Distance,IronGolemWorld.Melee(body,state,target,0));
    }
    private static Mobile WorldFind(uint serial)=>Server.World.FindMobile((Serial)serial);
    private bool PrimaryPath(IronGolemWorldState state,Mobile target)
    {
        if((state.Motion.Flags&0x20)!=0||state.Motion.Vector!=new Point2D(0,0)){throw new NotSupportedException("Mage primary needs stationary think context");}
        return AssassinSentryUnitPath.Points(state.Motion.X,state.Motion.Y,PetOwnerRoutes.Cell(target),14,
            new(0,0,body.Map.Width*2,body.Map.Height*2),(x,y)=>BladeWorldSpace.Mask(body.Map,body.Z,x,y,body)).Length!=0;
    }
    internal bool Launch(PlayerMobile owner,Mobile target,DateTime at)=>MageOffensiveSkills.Release(body,owner,target,at);
}
