// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DemonNativeAction(int Mode,int Contacts);

// One action owner inside the shared source-frame host. Each species keeps
// its own packets/ranks/events;DefilerS1 never becomes an ordinary attack.
internal sealed class DemonNativeCombat:IDisposable
{
    private readonly GoatmanNativeCombat Melee;
    private readonly WarlockTaintedBody Caster;
    private readonly WarlockDefilerBody Linker;
    private readonly PlayerMobile Owner;
    private Map Map;
    private Point3D Origin;
    private (uint X,uint Y) SourceOrigin;
    internal DemonNativeCombat(SkeletonGuardian body,PlayerMobile owner)
    {
        Owner=owner;
        if(body is WarlockGoatmanBody goat){Melee=new(goat,owner);}
        else if(body is WarlockTaintedBody tainted){Caster=tainted;TaintedFireBallCasting.ClaimClock(tainted);}
        else if(body is WarlockDefilerBody defiler){Linker=defiler;DefilerLinkCasting.ClaimClock(defiler);}
        else{throw new NotSupportedException("No source action binding for this demon");}
    }
    internal DemonNativeAction Current=>Melee?.Current is{} action?new(action.Mode,action.Contacts)
        :Caster!=null&&TaintedFireBallCasting.State(Caster)is{} cast?new(5,cast.Released?1:0)
        :Linker!=null&&DefilerLinkCasting.State(Linker)is{} link?new(8,link.Released?1:0):null;
    private SkeletonGuardian SpellBody=>Caster??(SkeletonGuardian)Linker;
    private bool Admit(Mobile target,int rank)
    {
        var body=SpellBody;
        if(target is not BaseCreature){throw new NotSupportedException("Demon spell recipient is not a qualified NPC");}
        if(body is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||rank<=0||Owner.NetState is not{Running:true}
            ||DeathMarkWorld.State(body)?.Mode!=1||GenericPetNativeControlStates.Read(body)!=0||CreatureHitRecovery.Remaining(body)>0){return false;}
        Map=body.Map;Origin=body.Location;var p=DeathMarkWorld.Path(body);SourceOrigin=(p.X,p.Y);return true;
    }
    internal GenericPetActionServices Bind(GenericPetActionServices outer,Func<int> frame,Func<DateTime> now)
    {
        if(Melee!=null){return Melee.Bind(outer,frame,now);}
        return Caster!=null?TaintedFireBallCasting.BindRegistered(Caster,outer,Admit,now):DefilerLinkCasting.BindRegistered(Linker,outer,Admit,now);
    }
    internal void Cancel(){Melee?.Cancel();if(Caster!=null){TaintedFireBallCasting.Cancel(Caster);}if(Linker!=null){DefilerLinkCasting.Cancel(Linker);}}
    internal void Advance(int frame,DateTime at)
    {
        if(Melee!=null){Melee.Advance(frame,at);return;}
        if(Current==null){return;}var body=SpellBody;var p=DeathMarkWorld.Path(body);
        if(body is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||body.Map!=Map||body.Location!=Origin
            ||(p.X,p.Y)!=SourceOrigin||DeathMarkWorld.State(body)?.Mode!=(Caster!=null?5:8)||GenericPetNativeControlStates.Read(body)!=0
            ||CreatureHitRecovery.Remaining(body)>0){Cancel();return;}
        if(Caster!=null)
        {
            TaintedFireBallCasting.Tick(at,true,Caster.Serial.Value);
            if(TaintedFireBallCasting.State(Caster)is{Fault:not null} failed){throw new NotSupportedException(failed.Fault);}
        }
        else
        {
            DefilerLinkCasting.Advance(Linker,at);
            if(DefilerLinkCasting.State(Linker)is{Fault:not null} failed){throw new NotSupportedException(failed.Fault);}
        }
    }
    public void Dispose(){Cancel();if(Caster!=null){TaintedFireBallCasting.ReleaseClock(Caster);}if(Linker!=null){DefilerLinkCasting.ReleaseClock(Linker);}}
}
