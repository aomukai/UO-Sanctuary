// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GrimWardWorldPulseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimWardWorldPulseChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Refuse(Action action)
    {try{action();throw new InvalidOperationException("Unsupported Ward pulse accepted");}catch(NotSupportedException){}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("grim-ward-world-pulse-check","Ward body periodic world recipient dispatch",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("grim-ward-world-pulse-check: passed");}catch(Exception e){Log.Error(e,"grim-ward-world-pulse-check: failed");}}));
    private static void Check()
    {
        int scenes=0;
        foreach(int kind in new[]{250,253,256})
        {
            var owner=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(owner,6200,26,3600);
            var origin=owner.Location;
            var body=new Static(0x1B7A){Name="Grim Ward body fixture (placeholder)",Movable=false};body.MoveToWorld(origin,owner.Map);
            var first=PoisonDaggerChecks.Target(owner);var arriving=PoisonDaggerChecks.Target(owner);
            var nearOwner=PoisonDaggerChecks.Target(owner);var upper=PoisonDaggerChecks.Target(owner);
            try
            {
                first.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),owner.Map);first.SetResistance(ResistanceType.Physical,100);
                arriving.MoveToWorld(new(origin.X+5,origin.Y,origin.Z),owner.Map);arriving.SetResistance(ResistanceType.Physical,0);
                upper.MoveToWorld(new(origin.X+1,origin.Y+1,origin.Z+16),owner.Map);
                nearOwner.MoveToWorld(new(origin.X+13,origin.Y,origin.Z),owner.Map);
                owner.MoveToWorld(new(origin.X+12,origin.Y,origin.Z),owner.Map);
                int formulas=0,hardReads=0,collisions=0,hard=0;
                SkillValues Context()
                {
                    formulas++;
                    return new(SkillMechanics.For(CombatAbility.GrimWard),k=>k=="Grim Ward"?3:k=="Find Potion"?hard+7:0,
                        k=>{if(k!="Find Potion"){return 0;}hardReads++;return hard;},80);
                }
                int Collision(){collisions++;return 17;}
                int mana=NecromancerEffects.ManaBalance256(owner),life=first.Hits;
                var pulse=GrimWardWorldPulse.Step(body,owner,kind,3,1000,1000,Context,Collision,TimedEffects.Now);
                Require(pulse is{CollisionResult:17,Dispatched:true,Candidates:1,Applied:1}&&formulas==1&&hardReads==1&&collisions==1,
                    "Initial body pulse did not evaluate once before collision or used owner-centered area");
                var curse=NecromancerEffects.Curse(first);
                Require(curse.Source==body.Serial.Value&&curse.PhysicalDelta==-4&&curse.Ward.FullPhysical==-20
                    &&NecromancerEffects.Curse(arriving)==null&&NecromancerEffects.Curse(nearOwner)==null&&NecromancerEffects.Curse(upper)==null,
                    "Body origin/radius/floor or missile creator changed");
                pulse=GrimWardWorldPulse.Step(body,owner,kind,3,1000,999,Context,Collision,TimedEffects.Now);
                Require(pulse is{CollisionResult:17,Dispatched:false,Candidates:0,Applied:0}&&formulas==1&&hardReads==1&&collisions==2
                    &&NecromancerEffects.Curse(first)==curse,"Nonperiod frame re-evaluated or refreshed the curse");
                NecromancerEffects.Profiles.Store(first,new(Curse:curse with{Ward=curse.Ward with{Velocity=-11}}));
                arriving.MoveToWorld(new(origin.X+2,origin.Y,origin.Z),owner.Map);hard=20;
                bool collisionAfterRecipients=false;
                pulse=GrimWardWorldPulse.Step(body,owner,kind,3,1000,994,Context,()=>
                {
                    collisions++;
                    collisionAfterRecipients=NecromancerEffects.Curse(first)?.PhysicalDelta==-24
                        &&NecromancerEffects.Curse(arriving)?.PhysicalDelta==-120;
                    return 23;
                },TimedEffects.Now);
                Require(pulse is{CollisionResult:23,Dispatched:true,Candidates:2,Applied:2}&&formulas==2&&hardReads==2
                    &&collisions==3&&collisionAfterRecipients,"Second pulse did not reuse a single evaluated packet before collision");
                Require(NecromancerEffects.Curse(first).Ward is{Velocity:-11,Attack:-27,Other:-27,FullPhysical:-120}
                    &&NecromancerEffects.Curse(arriving).Ward is{Velocity:-27,Attack:-27,Other:-27,FullPhysical:-120}
                    &&NecromancerEffects.Curse(first).Source==body.Serial.Value&&NecromancerEffects.Curse(arriving).Source==body.Serial.Value,
                    "Equal-rank primary retention or newly entered recipient used a different creator/context");
                Require(NecromancerEffects.Curse(nearOwner)==null&&NecromancerEffects.Curse(upper)==null
                    &&NecromancerEffects.ManaBalance256(owner)==mana&&first.Hits==life,"Pulse charged cast mana,damaged or selected an excluded recipient");
                Refuse(()=>GrimWardWorldPulse.Step(body,owner,kind,3,1000,1001,Context,Collision,TimedEffects.Now));
                Refuse(()=>GrimWardWorldPulse.Step(body,owner,kind,3,1000,0,Context,Collision,TimedEffects.Now));
                Refuse(()=>GrimWardWorldPulse.Step(body,owner,249,3,1000,994,Context,Collision,TimedEffects.Now));
                Refuse(()=>GrimWardWorldPulse.Step(body,owner,kind,4,1000,994,Context,Collision,TimedEffects.Now));
                Require(collisions==3,"Unsupported prepared pulse reached collision");
                scenes++;
            }
            finally
            {
                foreach(var target in new[]{first,arriving,nearOwner,upper}){NecromancerEffects.Profiles.Clear(target);target.Delete();}
                body.Delete();owner.Delete();
            }
        }
        Require(scenes==3&&!BarbarianCombat.Implemented(CombatAbility.GrimWard),"Pulse adapter enabled learned Ward");
        Log.Information("Ward world pulses:3body kinds,real area recipients,elapsed0/1/6,one explicit formula evaluation per pulse,retained/new primary deltas,creator/radius/floor and dispatch-before-collision pass;clock/context policy and learned cast remain open");
    }
}
