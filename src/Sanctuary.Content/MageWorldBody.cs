// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

[SerializationGenerator(0)]
public partial class MageWorldBody:NecromancerMinion,INecromancerGroundActor
{
    internal const string ProfileId="prepared-necropet-ground-fit-mage-clock-v1";
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageWorldBody));
    [SerializableField(0,setter:"private")] private string _worldSaved="{}";
    private NecromancerGroundWorld _ground;
    private string _lastNativeSize;
    internal NecromancerGroundWorld Ground=>_ground??=new(this,ProfileId,()=>_worldSaved,s=>_worldSaved=s,()=>{},true);
    NecromancerGroundWorld INecromancerGroundActor.WorldGround=>Ground;
    int INecromancerGroundActor.MovementPercent=>0;
    internal SkeletonWorldState WorldState=>Ground.State;
    internal override string SourceMonsterId=>IsMage?"necromage":base.SourceMonsterId;
    public override bool UsesNativeSummonTimer=>false;
    public MageWorldBody(){CantWalk=true;AIObject?.AITimer.Stop();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromMilliseconds(MageAttackClock.End*40);
    internal void BeginWorld(PlayerMobile owner,DateTime at,ulong seed)
    {
        if(!IsMage||GenericPetMetadata.Table.Skill(338)?.Name!="NecromageMissile")
        {throw new NotSupportedException("Mage world needs the pinned NPC338 skill");}
        Name="Skeletal Mage [placeholder]";Ground.Initialize(owner,at,()=>NecromancerMageMissiles.SetActorSeed(this,seed));
    }
    private int Footprint(BaseCreature target)
    {
        if(CreatureProfiles.For(target).Traits.Size is >=0 and <=3 and var size){return size;}
        string kind=target.GetType().FullName;
        if(_lastNativeSize!=kind)
        {
            _lastNativeSize=kind;Log.Warning("Mage target {Target} uses the existing provisional native size2; source footprint is not mapped",kind);
            ControlMaster?.SendMessage(0x22,"Some Mage targeting on this creature still uses Sanctuary's existing native footprint.");
        }
        return 2; // Same explicitly provisional projection used by existing Mage missile collisions.
    }
    internal NecromancerPetAiTarget Primary(PlayerMobile owner,IronGolemWorldState state)
    {
        var choice=NpcRangedTargets.Select(this,IronGolemWorld.Cell(state),target=>IronGolemWorld.Eligible(this,owner,target),
            target=>PrimaryPath(state,target),Footprint);
        if(choice==null){return new(0,0,false);}
        var target=World.FindMobile((Serial)choice.Candidate.Serial);
        return new((int)choice.Candidate.Serial,choice.Distance,IronGolemWorld.Melee(this,state,target,0));
    }
    private bool PrimaryPath(IronGolemWorldState state,Mobile target)
    {
        // Think runs after emergence/idle/attack/path completion, with stopped
        // motion. Reuse the verified stationary zero-lead unit-target probe;
        // this is not the moving Mage's actual path initializer.
        if((state.Motion.Flags&0x20)!=0||state.Motion.Vector!=new Point2D(0,0))
        {throw new NotSupportedException("Mage primary path probe needs stationary think context");}
        return AssassinSentryUnitPath.Points(state.Motion.X,state.Motion.Y,PetOwnerRoutes.Cell(target),14,
            new(0,0,Map.Width*2,Map.Height*2),(x,y)=>BladeWorldSpace.Mask(Map,Z,x,y,this)).Length!=0;
    }
    internal bool Launch(PlayerMobile owner,Mobile target,DateTime at)=>IronGolemWorld.Eligible(this,owner,target)
        &&NecromancerMageMissiles.Launch(this,target,at)>0;
    public override void SuspendActions(){base.SuspendActions();Ground.Suspend();}
    internal override void InterruptForRecovery(){base.InterruptForRecovery();Ground.Suspend();}
    public override void OnAfterDelete(){IronGolems.Forget(this);NecromancerMageMissiles.ForgetActor(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterMageWorldLoad(){Name="Skeletal Mage [placeholder]";Ground.Load();}
}
