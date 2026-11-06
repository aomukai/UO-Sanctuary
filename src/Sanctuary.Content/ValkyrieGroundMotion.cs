// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ValkyrieGroundMotionState(int WalkVelocityPercent,int RunVelocityPercent,SkeletonWorldState World)
{public string Profile{get;init;}="valkyrie-original-ground-motion-v1";}

// Saved actual source-mode ownership on the shared ground conductor. Rate
// bases are explicit captured inputs;modern initializer/run-list defaults and
// original item-event/contact delivery are not inferred by this admission.
internal static class ValkyrieGroundMotion
{
    internal const string Profile="valkyrie-original-ground-motion-v1";
    internal static ValkyrieGroundMotionState Copy(ValkyrieGroundMotionState state)=>state==null?null:state with{
        World=state.World with{Controller=state.World.Controller with{Motion=state.World.Controller.Motion with{
            Points=(Point2D[])state.World.Controller.Motion.Points.Clone(),SavedSteps=(Point2D[])state.World.Controller.Motion.SavedSteps.Clone()}}}};
    internal static void Validate(ValkyrieGroundMotionState state,uint owner)
    {
        if(state is not{Profile:Profile,World.Controller:not null}||state.World.Owner!=owner
            ||state.WalkVelocityPercent is <-10000 or >10000||state.RunVelocityPercent is <-10000 or >10000
            ||state.World.Controller.Profile!=Profile||state.World.Controller.Mode is not(1 or 2 or 15)
            ||state.World.Controller.Attempts!=0||state.World.Controller.Hits!=0)
        {throw new InvalidDataException("Invalid supplied Valkyrie ground motion context");}
        IronGolemWorld.Validate(state.World.Controller with{Profile="prepared-necropet-ground-fit-equipment-clock-v1"},run:true);
    }
    internal static void Activate(AmazonValkyrieBody body,int walkVelocityPercent,int runVelocityPercent,DateTime at)
    {
        if(body?.Prepared is not{GroundMotion:null,EquipmentDefenseProfile:ValkyrieEquipmentDefense.Profile} old
            ||body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner||owner.Serial.Value!=old.Owner
            ||body.Map!=owner.Map||body.Map==null||body.Map==Map.Internal||!body.CantWalk||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40))
        {throw new NotSupportedException("Valkyrie motion needs its original immobile recipient,world owner and UTC context");}
        RequireResources(body);
        ValkyrieEquipmentDefense.Require(body);var controller=new IronGolemWorldState(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),at.AddMilliseconds(40),Mode:1,NextThink:20){Profile=Profile};
        var state=new ValkyrieGroundMotionState(walkVelocityPercent,runVelocityPercent,new(old.Owner,controller));Validate(state,old.Owner);
        // Validate original transformed row/graph before changing admission.
        _=ValkyrieEquipmentAnimation.Plan(body,2,0,walkVelocityPercent);_=ValkyrieEquipmentAnimation.Plan(body,15,0,runVelocityPercent);
        try
        {
            body.RestorePrepared(old with{GroundMotion=state});body.Ground.Initialize(owner,at,()=>NpcEquipmentRandom.Require(body),adopt:true,initialThink:20);
            body.CantWalk=false;body.AIObject?.AITimer.Stop();
        }
        catch{IronGolems.Forget(body);body.RestorePrepared(old);body.CantWalk=true;throw;}
    }
    internal static int Mode(AmazonValkyrieBody body)
    {
        if(body?.Prepared?.GroundMotion is not{} state||body.CantWalk||body.Ground.State?.Controller is not{} controller)
        {throw new NotSupportedException("Moving Valkyrie lacks its source mode owner");}
        RequireResources(body);
        Validate(state with{World=body.Ground.State},body.Prepared.Owner);
        if(body.Map!=Map.Internal&&(controller.Map!=body.Map?.MapID||body.X!=(IronGolemWorld.Cell(controller).X+1)/2||body.Y!=(IronGolemWorld.Cell(controller).Y+1)/2))
        {throw new NotSupportedException("Valkyrie native pose changed before source controller repair");}
        return controller.Mode;
    }
    internal static (int Velocity,int Animation) Speed(AmazonValkyrieBody body,int bonus,bool run)
    {
        if(body.CantWalk){throw new NotSupportedException("Original Valkyrie ground motion is suspended by an external native walk flag");}
        RequireResources(body);
        var state=body.Prepared?.GroundMotion??throw new NotSupportedException("Missing original Valkyrie movement rates");
        int percent=checked((run?state.RunVelocityPercent:state.WalkVelocityPercent)+bonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlowTotal(body));
        var plan=ValkyrieEquipmentAnimation.Plan(body,run?15:2,0,percent);return(plan.Velocity,plan.Speed);
    }
    private static void RequireResources(AmazonValkyrieBody body)
    {
        if(GeneratedNpcLiveResources.State(body)is not{Valkyrie:not null} resources)
        {throw new NotSupportedException("Original Valkyrie movement requires its explicit source resource/regen context");}
        ValkyrieEquipmentResources.Require(body,resources);
    }
}
