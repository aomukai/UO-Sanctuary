// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal interface IUnarmedGroundPet:INecromancerGroundActor
{
    UnarmedPetWorld Ground{get;}
    (int Action,int End) GroundAttackClock{get;}
    int GroundVelocityPercent{get;}
    NecromancerGroundWorld INecromancerGroundActor.WorldGround=>Ground.World;
    int INecromancerGroundActor.MovementPercent=>GroundVelocityPercent;
}

// Periodic skills have their own cadence, including while ordinary AI is
// frozen. Skipped offline frames advance phase without releasing catch-up hits.
internal interface INecromancerGroundPeriodic
{
    void GroundFrame(int frame,DateTime at);
    void GroundSkipped(int frame);
}

// Shared owner/state/contact lifecycle. Actor classes retain their original
// serialized field/layout and supply historical native fallback hooks.
internal sealed class UnarmedPetWorld(SkeletonGuardian body,string profile,Func<string> read,Action<string> write,
    Func<Mobile,bool> prepareLegacy,Func<Mobile,bool> releaseAllowed,Action<Mobile> releaseLegacy,Action discardLegacy,
    Action<BaseCreature,MonsterContactResult,DateTime> afterContact=null)
{
    private NecromancerGroundWorld _world;
    private IronGolemContact.Prepared _contact;
    private uint _legacyTarget;
    internal NecromancerGroundWorld World=>_world??=new(body,profile,read,write,Cancel);
    internal bool Loaded{get=>World.Loaded;set=>World.Loaded=value;}
    internal SkeletonWorldState State=>World.State;
    internal IronGolemContact.Prepared Contact=>_contact;
    internal void Store(IronGolemWorldState state)=>World.Store(state);
    internal void Initialize(PlayerMobile owner,DateTime at,ulong seed)=>World.Initialize(owner,at,()=>NpcEquipmentRandom.Set(body,seed));
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
    internal void Suspend()=>World.Suspend();
    internal void Load()=>World.Load();
}
