// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;
namespace Sanctuary.Integration;

public static class BonePrisonAimChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BonePrisonAimChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("bone-prison-aim-check",Check);
        foreach(string mode in new[]{"right","quick"})
        {
            string label="bone-prison-ground-"+mode;
            Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>BoneBarrierBirthChecks.Input(n,CombatAbility.BonePrison,mode=="quick",label,groundPrison:true)));
        }
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved ground acquisition and original barrier casting",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BoneBarrierSegment[] Bodies(PlayerMobile owner)
    {
        var rows=new System.Collections.Generic.List<BoneBarrierSegment>();
        foreach(var body in owner.Map.GetMobilesInRange<BoneBarrierSegment>(owner.Location,24)){if(body.SummonMaster==owner){rows.Add(body);}}
        return rows.ToArray();
    }
    private static void Clear(PlayerMobile owner){foreach(var body in Bodies(owner)){body.Delete();}}
    private static void Ready(PlayerMobile owner){Clear(owner);PoisonDaggerChecks.Ready(owner);ClassCombat.For(owner).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(owner,owner.ManaMax*256);}
    private static void Check()
    {
        var owner=ClassSkillChecks.Player();owner.Name="PoisonDaggerFixtureBonePrisonAim";owner.AddItem(new Backpack());FixtureSpace.Corridor(owner,7130,24,3550);
        var a=PoisonDaggerChecks.Target(owner);var b=PoisonDaggerChecks.Target(owner);var ally=new Skeleton{NoKillAwards=true,Frozen=true};
        try
        {
            a.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);b.MoveToWorld(new(owner.X+9,owner.Y+1,owner.Z),owner.Map);
            var tie=new Point3D(owner.X+9,owner.Y,owner.Z);
            Require(BoneBarrierCasts.AcquirePrison(owner,new(b.X,b.Y+1,b.Z))==b,"Ground Prison selected nearest caster instead of nearest cursor");
            Require(BoneBarrierCasts.AcquirePrison(owner,tie)==(a.Serial.Value<b.Serial.Value?a:b)&&PsychicHammers.Acquire(owner,tie)==null,"Explicit Prison serial tie or preserved Psychic Hammer tie refusal changed");
            b.Internalize();Require(BoneBarrierCasts.AcquirePrison(owner,new(a.X+2,a.Y,a.Z))==a,"Two-tile circle excluded its boundary");
            Require(BoneBarrierCasts.AcquirePrison(owner,new(a.X+3,a.Y,a.Z))==null&&BoneBarrierCasts.AcquirePrison(owner,new(a.X+2,a.Y+2,a.Z))==null,"Ground aim reached outside its circle");
            ally.MoveToWorld(tie,owner.Map);ally.AIObject.AITimer.Stop();Require(ally.SetControlMaster(owner),"Aim ally fixture ownership");
            Require(BoneBarrierCasts.AcquirePrison(owner,tie)==a,"Automatic acquisition chose a friendly creature");
            a.Blessed=true;Require(BoneBarrierCasts.AcquirePrison(owner,tie)==null,"Automatic acquisition chose an invulnerable target");a.Blessed=false;
            a.Location=new(a.X,a.Y,a.Z+20);Require(BoneBarrierCasts.AcquirePrison(owner,tie)==null,"Automatic acquisition ignored height");a.Location=new(a.X,a.Y,owner.Z);
            Require(BoneBarrierCasts.AcquirePrison(owner,new(owner.X+17,owner.Y,owner.Z))==null,"Ground cursor exceeded caster range");
            var town=new GuardedRegion("BonePrisonAimTown",owner.Map,1000,new Rectangle2D(owner.X-1,owner.Y-1,3,3));town.Register();
            try{Require(BoneBarrierCasts.AcquirePrison(owner,tie)==null,"Ground acquisition ignored town policy");}finally{town.Unregister();}
            Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.BonePrison),"Ground Prison right binding");
            Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,CombatAbility.BonePrison).Mana256;
            Require(ClassCombat.ContinueCast(owner,CombatAbility.BonePrison,null,tie,Core.TickCount),"Ground held cast refused valid nearby monster");
            var session=ClassCombat.For(owner);Require(session.PendingTarget==a.Serial&&NecromancerEffects.ManaBalance256(owner)==mana&&Bodies(owner).Length==0,"Ground acquisition paid,created or lost primary during windup");
            Require(ClassCombat.ContinueCast(owner,CombatAbility.BonePrison,null,new(a.X+1,a.Y+1,a.Z),Core.TickCount)&&session.PendingTarget==a.Serial,"Held empty-ground refresh erased acquired primary");
            a.X++;Require(BoneBarrierCasts.Point(owner,CombatAbility.BonePrison,session.PendingTarget,session.PendingPoint,out var center)&&center==a.Location,"Acquired primary did not retain ordinary target tracking");
            Require(ClassCombat.ResolveAction(owner,session.EffectAt)&&Bodies(owner).Length>0&&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Ground Prison did not release/pay exactly once");
            int paid=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.ResolveAction(owner,session.EffectAt)&&NecromancerEffects.ManaBalance256(owner)==paid,"Ground release replayed payment");
            Ready(owner);Require(NecromancerCombat.Begin(owner,CombatAbility.BonePrison,Serial.Zero,new(a.X+1,a.Y,a.Z),Core.TickCount),"Second acquired start failed");
            session=ClassCombat.For(owner);a.Delete();_=ClassCombat.ResolveAction(owner,session.EffectAt);
            Require(Bodies(owner).Length==0&&NecromancerEffects.ManaBalance256(owner)==mana,"Lost acquired primary silently retargeted or spent mana");
            Ready(owner);Require(!NecromancerCombat.Begin(owner,CombatAbility.BonePrison,Serial.Zero,owner.Location,Core.TickCount)&&Bodies(owner).Length==0&&NecromancerEffects.ManaBalance256(owner)==mana,"Empty ground cast without an eligible primary");
        }
        finally{Clear(owner);a.Delete();b.Delete();ally.Delete();owner.Delete();}
    }
}
