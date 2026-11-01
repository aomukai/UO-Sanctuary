// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SkeletonWorldState(uint Owner,IronGolemWorldState Controller);

// Opt-in world actor; historical RaisedSkeleton layouts and native controllers
// remain intact. Pending contact is canceled by existing suspension/load rules.
[SerializationGenerator(0)]
public partial class SkeletonWorldBody:RaisedSkeleton,IUnarmedGroundPet
{
    internal const string ProfileId="prepared-necropet-ground-fit-unarmed-clock-v1";
    [SerializableField(0,setter:"private")] private string _worldSaved="{}";
    private UnarmedPetWorld _ground;
    internal UnarmedPetWorld Ground=>_ground??=new(this,ProfileId,()=>_worldSaved,s=>_worldSaved=s,
        target=>base.PrepareMeleeContact(target),target=>base.CanReleaseMeleeContact(target),target=>base.ResolveMeleeContact(target),()=>base.DiscardMeleeContact());
    UnarmedPetWorld IUnarmedGroundPet.Ground=>Ground;
    (int Action,int End) IUnarmedGroundPet.GroundAttackClock=>SourceAttackClock;
    int IUnarmedGroundPet.GroundVelocityPercent=>SkeletonPetUpgrades.MovementPercent(this);
    internal bool WorldLoaded{get=>Ground.Loaded;set=>Ground.Loaded=value;}
    internal SkeletonWorldState WorldState=>Ground.State;
    internal IronGolemContact.Prepared WorldContact=>Ground.Contact;
    public override bool UsesNativeSummonTimer=>false;
    public SkeletonWorldBody(){Name="Skeleton Warrior [placeholder]";CantWalk=true;AIObject?.AITimer.Stop();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromMilliseconds(SourceAttackClock.End*40);
    internal void StoreWorld(IronGolemWorldState state)=>Ground.Store(state);
    internal static SkeletonWorldBody CreatePrepared(PlayerMobile owner,int rank,int mastery,Point3D point,DateTime at,ulong seed)
    {
        if(owner?.Map==null||owner.Map==Map.Internal||!owner.Map.CanSpawnMobile(point)||at.Kind!=DateTimeKind.Utc)
        {throw new ArgumentException("Skeleton world actor needs legal placement and UTC time");}
        var body=new SkeletonWorldBody();
        try
        {
            body.InitializeExact(owner,rank,mastery);
            if(!body.SetControlMaster(owner)){body.Delete();return null;}
            body.MoveToWorld(point,owner.Map);body.Ground.Initialize(owner,at,seed);return body;
        }
        catch{body.Delete();throw;}
    }
    internal void PrepareWorldContact(PlayerMobile owner,IronGolemWorldState state,BaseCreature target)=>Ground.Prepare(owner,state,target);
    internal bool ReleaseWorldContact(PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)=>Ground.Release(owner,state,target,at);
    internal void CancelWorldContact()=>Ground.Cancel();
    public override void SuspendActions(){base.SuspendActions();Ground.Suspend();}
    internal override void InterruptForRecovery(){base.InterruptForRecovery();Ground.Suspend();}
    public override void OnAfterDelete(){Ground.Cancel();IronGolems.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterWorldLoad()=>Ground.Load();
}
