// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DemonNativeAction(int Mode,int Contacts);

// One action owner inside the shared source-frame host. Existing melee and
// A2 implementations keep their packets/ranks/events;no second spell is added.
internal sealed class DemonNativeCombat:IDisposable
{
    private readonly GoatmanNativeCombat Melee;
    private readonly WarlockTaintedBody Caster;
    private readonly PlayerMobile Owner;
    private Map Map;
    private Point3D Origin;
    private (uint X,uint Y) SourceOrigin;
    internal DemonNativeCombat(SkeletonGuardian body,PlayerMobile owner)
    {
        Owner=owner;
        if(body is WarlockGoatmanBody goat){Melee=new(goat,owner);}
        else if(body is WarlockTaintedBody tainted){Caster=tainted;TaintedFireBallCasting.ClaimClock(tainted);}
        else{throw new NotSupportedException("No source action binding for this ground demon");}
    }
    internal DemonNativeAction Current=>Melee?.Current is{} action?new(action.Mode,action.Contacts)
        :Caster!=null&&TaintedFireBallCasting.State(Caster)is{} cast?new(5,cast.Released?1:0):null;
    internal GenericPetActionServices Bind(GenericPetActionServices outer,Func<int> frame,Func<DateTime> now)
    {
        if(Melee!=null){return Melee.Bind(outer,frame,now);}
        return TaintedFireBallCasting.BindRegistered(Caster,outer,(target,rank)=>
        {
            if(target is not BaseCreature){throw new NotSupportedException("Tainted automatic recipient is not a qualified NPC");}
            if(Caster is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||rank<=0||Owner.NetState is not{Running:true}
                ||DeathMarkWorld.State(Caster)?.Mode!=1||GenericPetNativeControlStates.Read(Caster)!=0||CreatureHitRecovery.Remaining(Caster)>0){return false;}
            Map=Caster.Map;Origin=Caster.Location;var p=DeathMarkWorld.Path(Caster);SourceOrigin=(p.X,p.Y);return true;
        },now);
    }
    internal void Cancel(){Melee?.Cancel();if(Caster!=null){TaintedFireBallCasting.Cancel(Caster);}}
    internal void Advance(int frame,DateTime at)
    {
        if(Melee!=null){Melee.Advance(frame,at);return;}
        if(TaintedFireBallCasting.State(Caster)==null){return;}
        var p=DeathMarkWorld.Path(Caster);
        if(Caster is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||Caster.Map!=Map||Caster.Location!=Origin
            ||(p.X,p.Y)!=SourceOrigin||DeathMarkWorld.State(Caster)?.Mode!=5||GenericPetNativeControlStates.Read(Caster)!=0
            ||CreatureHitRecovery.Remaining(Caster)>0){Cancel();return;}
        TaintedFireBallCasting.Tick(at,true,Caster.Serial.Value);
        if(TaintedFireBallCasting.State(Caster)is{Fault:not null} failed){throw new NotSupportedException(failed.Fault);}
    }
    public void Dispose(){Cancel();if(Caster!=null){TaintedFireBallCasting.ReleaseClock(Caster);}}
}
