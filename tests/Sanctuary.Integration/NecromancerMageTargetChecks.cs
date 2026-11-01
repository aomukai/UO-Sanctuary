// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NecromancerMageTargetChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerMageTargetChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("necromancer-mage-targets-check","Ranged AI67 services on an actual learned Mage; world execution remains separate",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("necromancer-mage-targets-check: passed");}catch(Exception e){Log.Error(e,"necromancer-mage-targets-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureMageTargets";FixtureSpace.Corridor(p,7060,35,3700);
        void Train(string id){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior);}}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
        Train("necromancer.skeletalmage");var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);dead.Damage(100000,p);if(dead.Alive){dead.Kill();}
        var corpse=dead.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Actual Mage corpse");NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.RaiseSkeletalMage).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned Mage creation/payment");
        var body=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeletalMage).OfType<NecromancerMinion>().Single();body.AIObject?.AITimer.Stop();body.SuspendActions();body.Frozen=true;
        var a=PoisonDaggerChecks.Target(p);var b=PoisonDaggerChecks.Target(p);a.MoveToWorld(new(body.X+4,body.Y,body.Z),p.Map);b.MoveToWorld(new(body.X+8,body.Y,body.Z),p.Map);
        int ad=15,bd=19;bool primaryCombat=true,stopCommon=false;int commonTarget=-1,commonCombat=-1,moveKind=-1,primaryCalls=0;uint primary=a.Serial.Value;
        var requests=new List<NecromancerPetAiRequest>();var rolls=new Queue<int>();
        bool Eligible(Mobile t)=>t is{Alive:true,Deleted:false}&&(t==a||t==b)&&ClassCombat.IsHostile(p,t);
        int Distance(Mobile t)=>t==p?0:t==a?ad:t==b?bd:throw new InvalidOperationException("Unexpected ranged distance target");
        var query=NecromancerPetTargets.BindRanged(body,p,Eligible,Distance,search=>
        {Require(search==0,"Ranged primary did not receive its own query variant");primaryCalls++;return new((int)primary,primary==a.Serial.Value?ad:bd,primaryCombat);},
            requests.Add,()=>rolls.Count>0?rolls.Dequeue():throw new InvalidOperationException("Unexpected ranged RNG draw"),
            (target,combat,_,_)=>{commonTarget=target;commonCombat=combat?1:0;return stopCommon;},
            (kind,_,_,_)=>{moveKind=kind;return true;});
        int skill=338;Require(GenericPetMetadata.Table.Skill(skill)?.Name=="NecromageMissile","Pinned Mage NPC skill changed");
        void Think(params int[] draws)
        {
            requests.Clear();rolls.Clear();foreach(int draw in draws){rolls.Enqueue(draw);}commonTarget=commonCombat=-1;moveKind=-1;
            var result=NecromancerPetAi.Think(new(new(1,0,0),12,14,4,skill),query);
            Require(result.RememberedOwner==(int)p.Serial.Value&&rolls.Count==0,"Ranged owner memory or exact query/RNG flow");
            Require(requests.All(r=>r.Kind is not(NecromancerPetAiKind.Attack or NecromancerPetAiKind.Approach)),"Ranged Mage silently used melee attack/approach");
        }
        Static wall=null;
        try
        {
            int bolts=NecromancerMageMissiles.Snapshot().Bolts.Length;ClassCombat.For(p).Target=b.Serial;Think(0,0);
            Require(commonTarget==(int)a.Serial.Value&&commonCombat==1&&requests.Select(r=>r.Kind).SequenceEqual(new[]{NecromancerPetAiKind.DeleteThink,NecromancerPetAiKind.Skill})
                &&requests[^1] is{A:4,B:338}&&requests[^1].C==(int)a.Serial.Value,"Primary15 did not retain ranged skill/mode/target");
            ad=16;primaryCombat=false;Think(0,0);Require(commonTarget==(int)b.Serial.Value&&commonCombat==0&&requests[^1].Kind==NecromancerPetAiKind.Skill&&requests[^1].C==(int)b.Serial.Value,"Owner target19 replacement or primary combat flag changed");
            bd=20;Think(0);Require(commonTarget==0&&requests[^1].Kind==NecromancerPetAiKind.Wander,"Owner target20 boundary admitted");bd=19;
            wall=new Static(0x80);wall.MoveToWorld(new(body.X+6,body.Y,body.Z),p.Map);Require(query.MissileBarrier((int)b.Serial.Value),"Actual ranged missile barrier fixture missing");
            Think(0);Require(commonTarget==0&&requests[^1].Kind==NecromancerPetAiKind.Wander,"Blocked owner target admitted");wall.Delete();wall=null;
            ad=15;Think(0,80,75);Require(requests[^1].Kind==NecromancerPetAiKind.Reposition&&requests[^1].A==(int)a.Serial.Value&&requests[^1].B==3,"Ranged reposition request/roll boundary");
            Think(0,80,74);Require(requests[^1].Kind==NecromancerPetAiKind.Idle&&requests[^1].A==10,"Ranged idle request/roll boundary");
            stopCommon=true;Think(0);Require(requests.Count==1&&requests[0].Kind==NecromancerPetAiKind.DeleteThink,"Common movement did not suppress ranged action");stopCommon=false;
            primary=0;ClassCombat.For(p).Target=Serial.Zero;Think(0);Require(commonTarget==0&&requests[^1].Kind==NecromancerPetAiKind.Wander,"No-target ranged wander");
            bool refused=false;try{query.Primary(false,24);}catch(NotSupportedException){refused=true;}Require(refused,"Ranged binding silently accepted melee primary");
            Require(primaryCalls==8&&moveKind==-1&&NecromancerMageMissiles.Snapshot().Bolts.Length==bolts,"Query fixture executed a world action/projectile or missed a primary request");
            Log.Information("Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries,owner selection,real missile barrier,Skill338/A1,reposition/idle/common-move/wander and exact draws pass;automatic world and primary-search geometry remain separate");
        }
        finally{wall?.Delete();ClassCombat.For(p).Target=Serial.Zero;a.Delete();b.Delete();body.Delete();corpse.Delete();dead.Delete();p.Delete();}
    }
}
