// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SkeletonWorldState(uint Owner,IronGolemWorldState Controller);

// Opt-in world actor; historical RaisedSkeleton layouts and native controllers
// remain intact. Pending contact is canceled by existing suspension/load rules.
[SerializationGenerator(0)]
public partial class SkeletonWorldBody:RaisedSkeleton
{
    internal const string ProfileId="prepared-necropet-ground-fit-unarmed-clock-v1";
    [SerializableField(0,setter:"private")] private string _worldSaved="{}";
    private SkeletonWorldState _world;
    private IronGolemContact.Prepared _worldContact;
    private uint _legacyTarget;
    internal bool WorldLoaded;
    internal SkeletonWorldState WorldState=>_world??(_worldSaved=="{}"?null:_world=JsonSerializer.Deserialize<SkeletonWorldState>(_worldSaved));
    internal IronGolemContact.Prepared WorldContact=>_worldContact;
    public override bool UsesNativeSummonTimer=>false;
    public SkeletonWorldBody(){Name="Skeleton Warrior [placeholder]";CantWalk=true;AIObject?.AITimer.Stop();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromMilliseconds(SourceAttackClock.End*40);
    internal void StoreWorld(IronGolemWorldState state)
    {
        if(state?.Profile!=ProfileId){throw new InvalidDataException("Invalid skeleton world profile");}
        IronGolemWorld.Validate(state with{Profile="prepared-necropet-ground-fit-equipment-clock-v1"});
        if(WorldState is not{} old||!((Serial)old.Owner).IsMobile){throw new InvalidDataException("Missing skeleton world owner");}
        var next=old with{Controller=state};string saved=JsonSerializer.Serialize(next);_world=next;_worldSaved=saved;
        if(state.Mode!=4||state.ActionAt==0){CancelWorldContact();}
    }
    internal static SkeletonWorldBody CreatePrepared(PlayerMobile owner,int rank,int mastery,Point3D point,DateTime at,ulong seed)
    {
        if(owner?.Map==null||owner.Map==Map.Internal||!owner.Map.CanSpawnMobile(point)||at.Kind!=DateTimeKind.Utc)
        {throw new ArgumentException("Skeleton world actor needs legal placement and UTC time");}
        var body=new SkeletonWorldBody();
        try
        {
            body.InitializeExact(owner,rank,mastery);
            if(!body.SetControlMaster(owner)){body.Delete();return null;}
            body.MoveToWorld(point,owner.Map);NpcEquipmentRandom.Set(body,seed);
            var controller=new IronGolemWorldState(owner.Map.MapID,DruidRavenMotion.Center(point.X*2,point.Y*2),at.AddMilliseconds(40),
                EndAt:DruidWolfGeometry.Clock("S1",source:"necroskeleton-animation").End){Profile=ProfileId};
            body._world=new(owner.Serial.Value,controller);body.StoreWorld(controller);
            PetOwnerRoutes.For(owner,at);BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);IronGolems.Track(body);return body;
        }
        catch{body.Delete();throw;}
    }
    internal void PrepareWorldContact(PlayerMobile owner,IronGolemWorldState state,BaseCreature target)
    {
        CancelWorldContact();IronGolemContact.Rules rules;
        try{rules=UnarmedSummonContact.Require(this,owner,target);}
        catch(NotSupportedException)
        {
            // Preserve the existing explicitly reported native recipient bridge.
            // This fallback is allowed only before packet RNG/allocation begins.
            if(!base.PrepareMeleeContact(target)){throw new NotSupportedException("Skeleton native recipient preparation refused");}
            _legacyTarget=target.Serial.Value;return;
        }
        _worldContact=UnarmedSummonContact.Prepare(this,owner,target,rules,bonus=>IronGolemWorld.Melee(this,state,target,bonus));
    }
    internal bool ReleaseWorldContact(PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)
    {
        var contact=_worldContact;_worldContact=null;
        if(contact==null)
        {
            bool legacy=_legacyTarget==target.Serial.Value;_legacyTarget=0;
            if(!legacy||!base.CanReleaseMeleeContact(target)){return false;}
            int before=SkillHealth.Balance256(target);base.ResolveMeleeContact(target);
            return SkillHealth.Balance256(target)<before; // Native legacy damaging-hit metric, not a source result flag.
        }
        var result=UnarmedSummonContact.Release(this,owner,target,contact,at,bonus=>IronGolemWorld.Melee(this,state,target,bonus));
        return(result.Packet.ResultFlags&SourceMeleeResult.Successful)!=0;
    }
    internal void CancelWorldContact(){_worldContact?.Cancel();_worldContact=null;_legacyTarget=0;base.DiscardMeleeContact();}
    public override void SuspendActions(){base.SuspendActions();CancelWorldContact();if(WorldState is{} state){StoreWorld(IronGolemWorld.Park(state.Controller));}}
    internal override void InterruptForRecovery(){base.InterruptForRecovery();CancelWorldContact();if(WorldState is{} state){StoreWorld(IronGolemWorld.Park(state.Controller));}}
    public override void OnAfterDelete(){CancelWorldContact();IronGolems.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterWorldLoad()
    {
        AIObject?.AITimer.Stop();if(WorldState is not{} state){return;}
        StoreWorld(state.Controller);WorldLoaded=true;IronGolems.Track(this);
    }
}
