// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record RingOfFireWorldData(string ReferenceVersion,string SourceSha256,int MissileCount,SummonSource[] Missiles);
internal sealed record RingOfFireMotionState(RingOfFireLaunchRequest Request,int Map,int Z,int Frame,double X,double Y,
    int VelocityFp8,int AccelerationCounter,SourceStepTrace Steps);
internal sealed record RingOfFireEmission(int Result,RingOfFireMotionState[] Motions);

// Prepared launch/getter binding and the existing Nova motion projection.
// Collision,damage dispatch and live fnum selection remain caller-owned;
// no harmless visual-only Ring is published as a learned skill.
internal static class RingOfFireMotion
{
    private static readonly Lazy<RingOfFireWorldData> Data=new(()=>BalanceCatalog.ReadObject<RingOfFireWorldData>("ring-of-fire-world"));
    private static MissileValues Missile(SkillValues values)
    {
        var data=Data.Value;var row=SkillMechanics.For(CombatAbility.RingOfFire);
        if(data.ReferenceVersion!="3.3.93847"||data.SourceSha256!=ItemSkillExecution.MissileSourceHash||data.MissileCount!=742
            ||data.Missiles.Length!=2||values.Ability!=CombatAbility.RingOfFire||values.Rank is <1 or >255
            ||row.SourceId!=394||row.Number("srvstfunc")!=0||row.Number("srvdofunc")!=154
            ||new[]{"srvmissilea","srvmissileb","srvmissilec"}.Any(k=>row.Text(k)!="ringoffire"))
        {throw new NotSupportedException("Unqualified Ring launch binding");}
        var m=new MissileValues(data.Missiles.Single(s=>s.Key=="ringoffire").Fields,values);
        if(m.Number("*ID")!=689||m.Number("pSrvDoFunc")!=1||m.Number("pSrvHitFunc")!=1||m.Number("pSrvDmgFunc")!=17
            ||m.Number("Vel")!=25||m.Number("MaxVel")!=25||m.Number("VelLev")!=0||m.Number("Accel")!=-1000||m.Number("Range")!=11
            ||m.Number("CollideType")!=3||m.Number("Size")!=2||m.Number("CanSlow")!=0||m.Number("InitSteps")!=1
            ||m.Number("NextHit")!=1||m.Number("NextDelay")!=11||m.Number("KnockBack")!=100||m.Number("sHitPar1")!=3
            ||m.Number("Pierce")!=1||m.Number("AlwaysExplode")!=1||m.Text("DmgCalc1")!="100+(fnum*par1)"||m.Number("Param1")!=5)
        {throw new NotSupportedException("Ring missile profile changed");}
        return m;
    }
    internal static RingOfFireEmission Prepare(PlayerMobile owner,SkillValues values,int context12c,Func<RingOfFireLaunchRequest,bool> allocate=null)
    {
        var missile=Missile(values);
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Server.Map.Internal
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Ring prepared owner context");}
        var motions=new List<RingOfFireMotionState>();
        var context=new RingOfFireLaunchContext(owner.Serial.Value,0,true,owner.X*2,owner.Y*2,394,values.Rank,1,Data.Value.MissileCount,context12c);
        int result=RingOfFireLaunch.Run(context,new(()=>{},()=>true,()=>689,_=>missile.Number("Vel"),field=>checked((int)values.Eval(field switch
        {0x198=>"calc3",0x194=>"calc2",_=>throw new NotSupportedException("Unknown Ring launch formula field")})),request=>
        {
            if(allocate!=null&&!allocate(request)){return false;}
            int velocity=SlowMissilePayload.VelocityFp8(missile.Number("Vel"),0,values.Rank,request.Flags,request.Velocity,false,100);
            motions.Add(new(request,owner.Map.MapID,owner.Z,0,owner.X,owner.Y,velocity,0,new([new(owner.X*2,owner.Y*2)])));return true;
        }));
        return new(result,motions.ToArray());
    }
    internal static RingOfFireMotionState Step(RingOfFireMotionState state)
    {
        if(state==null||state.Frame is <0 or >11||state.AccelerationCounter is <0 or >4||state.VelocityFp8<0
            ||state.Request is not{Missile:689,Flags:7,Skill:394}){throw new InvalidDataException("Invalid prepared Ring motion");}
        if(state.Frame==11){return state;}
        var d=MissileVectors.Direction(state.Request.TargetX,state.Request.TargetY);
        var step=MissileVectors.Accelerate(state.VelocityFp8,-1000,state.AccelerationCounter,25<<8,0,d.X,d.Y);
        double vx=step.X/131072.0,vy=step.Y/131072.0;
        return state with{Frame=state.Frame+1,X=state.X+vx,Y=state.Y+vy,VelocityFp8=step.Velocity,AccelerationCounter=step.Counter,
            Steps=SourceMissileSteps.Advance(state.X,state.Y,vx,vy,state.Steps)};
    }
    internal static void ScaleAtSuppliedFrame(SkillValues values,int fnum,Span<int> packet)
    {
        var missile=Missile(values);if(fnum is <0 or >11){throw new ArgumentOutOfRangeException(nameof(fnum));}
        MissileDamageScale.ScaleRawPacket(packet,checked((int)missile.EvalAtFrame("DmgCalc1",fnum)));
    }
}
