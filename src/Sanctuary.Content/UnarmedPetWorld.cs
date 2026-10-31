// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal interface IUnarmedGroundPet
{
    UnarmedPetWorld Ground{get;}
    (int Action,int End) GroundAttackClock{get;}
    int GroundVelocityPercent{get;}
}

// Shared owner/state/contact lifecycle. Actor classes retain their original
// serialized field/layout and supply historical native fallback hooks.
internal sealed class UnarmedPetWorld(SkeletonGuardian body,string profile,Func<string> read,Action<string> write,
    Func<Mobile,bool> prepareLegacy,Func<Mobile,bool> releaseAllowed,Action<Mobile> releaseLegacy,Action discardLegacy,
    Action<BaseCreature,MonsterContactResult,DateTime> afterContact=null)
{
    private SkeletonWorldState _state;
    private IronGolemContact.Prepared _contact;
    private uint _legacyTarget;
    internal bool Loaded;
    internal SkeletonWorldState State=>_state??(read()=="{}"?null:_state=JsonSerializer.Deserialize<SkeletonWorldState>(read()));
    internal IronGolemContact.Prepared Contact=>_contact;
    internal void Store(IronGolemWorldState state)
    {
        if(state?.Profile!=profile){throw new InvalidDataException("Invalid unarmed world profile");}
        IronGolemWorld.Validate(state with{Profile="prepared-necropet-ground-fit-equipment-clock-v1"});
        if(State is not{} old||!((Serial)old.Owner).IsMobile){throw new InvalidDataException("Missing unarmed world owner");}
        var next=old with{Controller=state};string saved=JsonSerializer.Serialize(next);_state=next;write(saved);
        if(state.Mode!=4||state.ActionAt==0){Cancel();}
    }
    internal void Initialize(PlayerMobile owner,DateTime at,ulong seed)
    {
        if(at.Kind!=DateTimeKind.Utc||body.ControlMaster!=owner||body.Map!=owner.Map||body.Map==Map.Internal)
        {throw new ArgumentException("Unarmed world needs an owned actor,world placement and UTC time");}
        NpcEquipmentRandom.Set(body,seed);
        var controller=new IronGolemWorldState(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),at.AddMilliseconds(40),
            EndAt:DruidWolfGeometry.Clock("S1",source:body.SourceMonsterId+"-animation").End){Profile=profile};
        _state=new(owner.Serial.Value,controller);Store(controller);PetOwnerRoutes.For(owner,at);
        BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);IronGolems.Track(body);
    }
    internal void Prepare(PlayerMobile owner,IronGolemWorldState state,BaseCreature target)
    {
        Cancel();IronGolemContact.Rules rules;
        try{rules=UnarmedSummonContact.Require(body,owner,target);}
        catch(NotSupportedException)
        {
            if(!prepareLegacy(target)){throw new NotSupportedException("Native unarmed recipient preparation refused");}
            _legacyTarget=target.Serial.Value;return;
        }
        _contact=UnarmedSummonContact.Prepare(body,owner,target,rules,bonus=>IronGolemWorld.Melee(body,state,target,bonus));
    }
    internal bool Release(PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)
    {
        var contact=_contact;_contact=null;
        if(contact==null)
        {
            bool legacy=_legacyTarget==target.Serial.Value;_legacyTarget=0;
            if(!legacy||!releaseAllowed(target)){return false;}
            int before=SkillHealth.Balance256(target);releaseLegacy(target);
            return SkillHealth.Balance256(target)<before;
        }
        var result=UnarmedSummonContact.Release(body,owner,target,contact,at,bonus=>IronGolemWorld.Melee(body,state,target,bonus));
        afterContact?.Invoke(target,result,at);return(result.Packet.ResultFlags&SourceMeleeResult.Successful)!=0;
    }
    internal void Cancel(){_contact?.Cancel();_contact=null;_legacyTarget=0;discardLegacy();}
    internal void Suspend(){Cancel();if(State is{} state){Store(IronGolemWorld.Park(state.Controller));}}
    internal void Load(){body.AIObject?.AITimer.Stop();if(State is not{} state){return;}Store(state.Controller);Loaded=true;IronGolems.Track(body);}
}
