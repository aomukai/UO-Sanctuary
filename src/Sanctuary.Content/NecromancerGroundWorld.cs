// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal interface INecromancerGroundActor
{
    NecromancerGroundWorld WorldGround{get;}
    int MovementPercent{get;}
}

// Original equipped actors supply their own transformed animation/stat graph.
// This motion capability grants no unarmed or legacy single-item attack path.
internal interface INecromancerGroundMotion:INecromancerGroundActor
{
    (int Velocity,int Animation) GroundMovement(int bonus,bool run);
    int GroundRegenerationBase{get;}
}

// Original equipment owns its animation plan and ordered contact at the
// action marker. This capability never falls through to unarmed/Iron damage.
internal interface INecromancerGroundEquipmentContact:INecromancerGroundMotion
{
    (int Speed,int Action,int End) GroundAttack();
    bool GroundRelease(PlayerMobile owner,IronGolemWorldState state,Server.Mobiles.BaseCreature target,DateTime at);
}

// Owner/frame/load transport shared by melee and ranged pets. Keep actor-owned
// serialized fields and the historical owner/controller JSON shape unchanged.
internal sealed class NecromancerGroundWorld(SkeletonGuardian body,string profile,Func<string> read,Action<string> write,Action cancel,bool ranged=false,bool run=false)
{
    private SkeletonWorldState _state;
    internal bool Loaded;
    internal SkeletonWorldState State=>_state??(read()=="{}"?null:_state=JsonSerializer.Deserialize<SkeletonWorldState>(read()));
    internal void Store(IronGolemWorldState state)
    {
        if(state?.Profile!=profile){throw new InvalidDataException("Invalid Necromancer ground profile");}
        IronGolemWorld.Validate(state with{Profile="prepared-necropet-ground-fit-equipment-clock-v1"},ranged,run);
        if(State is not{} old||!((Serial)old.Owner).IsMobile){throw new InvalidDataException("Missing ground actor owner");}
        var next=old with{Controller=state};string saved=JsonSerializer.Serialize(next);write(saved);_state=next;
        if(state.Mode!=4||state.ActionAt==0){cancel();}
    }
    internal void Initialize(PlayerMobile owner,DateTime at,Action initializeRandom,bool adopt=false,int? initialThink=null)
    {
        if(at.Kind!=DateTimeKind.Utc||body.ControlMaster!=owner||body.Map!=owner.Map||body.Map==Map.Internal)
        {throw new ArgumentException("Ground actor needs owner,world placement and UTC time");}
        initializeRandom();
        var controller=new IronGolemWorldState(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),at.AddMilliseconds(40),
            Mode:adopt?1:8,NextThink:initialThink??(adopt?1:25),EndAt:adopt?0:DruidWolfGeometry.Clock("S1",source:body.SourceMonsterId+"-animation").End,Ai:new(ranged?1:0,0,0)){Profile=profile};
        _state=new(owner.Serial.Value,controller);Store(controller);PetOwnerRoutes.For(owner,at);
        if(!adopt){BattleCommand.Track(body);}PersistentMinions.Profiles.Track(body);IronGolems.Track(body);
    }
    internal void Invalidate()=>_state=null;
    internal void Suspend(){cancel();if(State is{} state){Store(IronGolemWorld.Park(state.Controller));}}
    internal void Load(){body.AIObject?.AITimer.Stop();if(State is not{} state){return;}Store(state.Controller);Loaded=true;IronGolems.Track(body);}
}
