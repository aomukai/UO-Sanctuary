// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

[SerializationGenerator(0)]
public partial class ClayGolemWorldBody:ClayGolemBody,IUnarmedGroundPet
{
    internal const string ProfileId="prepared-necropet-ground-fit-clay-clock-v1";
    [SerializableField(0,setter:"private")] private string _worldSaved="{}";
    private UnarmedPetWorld _ground;
    internal UnarmedPetWorld Ground=>_ground??=new(this,ProfileId,()=>_worldSaved,s=>_worldSaved=s,
        target=>base.PrepareMeleeContact(target),target=>base.CanReleaseMeleeContact(target),target=>base.ResolveMeleeContact(target),()=>base.DiscardMeleeContact(),
        (target,result,_)=>ApplyOutgoingSlow(target,result));
    UnarmedPetWorld IUnarmedGroundPet.Ground=>Ground;
    (int Action,int End) IUnarmedGroundPet.GroundAttackClock=>SourceAttackClock;
    int IUnarmedGroundPet.GroundVelocityPercent=>VelocityPercent;
    internal SkeletonWorldState WorldState=>Ground.State;
    internal IronGolemContact.Prepared WorldContact=>Ground.Contact;
    public override bool UsesNativeSummonTimer=>false;
    public ClayGolemWorldBody(){CantWalk=true;AIObject?.AITimer.Stop();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromMilliseconds(SourceAttackClock.End*40);
    internal void BeginWorld(PlayerMobile owner,DateTime at,ulong seed){Name="Clay Golem [placeholder]";Ground.Initialize(owner,at,seed);}
    public override void SuspendActions(){base.SuspendActions();Ground.Suspend();}
    internal override void InterruptForRecovery(){base.InterruptForRecovery();Ground.Suspend();}
    public override void OnAfterDelete(){Ground.Cancel();IronGolems.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterClayWorldLoad(){Name="Clay Golem [placeholder]";Ground.Load();}
}
