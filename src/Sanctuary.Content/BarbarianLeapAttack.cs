// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Optional V0 flight extension. Ordinary Leap saves do not contain this field.
public sealed record LeapAttackFlight(uint Target,uint Weapon,string Code,int Advance,int ContactFrame,int EndFrame,
    int CheckRemaining,int CheckPeriod,int Frame=0,bool Released=false);
public static class BarbarianLeapAttack
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Func<int,int> AreaRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    private static IDisposable MeleeContext(PlayerMobile owner)=>CombatTiming.Equipped(owner) is{} weapon&&ClassGear.Definition(weapon)?.Category=="Throwing"
        ?WeaponHands.UseMelee(owner,weapon):null;
    internal static bool WeaponAllowed(PlayerMobile owner)
    {
        var w=CombatTiming.Equipped(owner);if(w==null){return true;}
        return !ClassGear.Broken(w)&&ClassGear.Base(w) is{} b&&ClassItemTypes.Is(b.Type,"mele")
            &&ClassGear.State(w) is{Identified:true,CraftedEquipment:false} s&&s.Runes.Length==0&&s.MaterialRolls.Length==0;
    }
    internal static bool Recipient(PlayerMobile owner,BaseCreature target)=>target is{Alive:true,Deleted:false}
        &&target.Map==owner.Map&&ClassCombat.IsHostile(owner,target)&&Math.Abs(target.Z-owner.Z)<16&&owner.InLOS(target)
        &&SpellHelper.CheckTown(owner.Location,owner)&&SpellHelper.CheckTown(target.Location,owner);
    internal static bool InRange(PlayerMobile owner,BaseCreature target)=>Recipient(owner,target)&&PlayerMeleeGeometry.Within(owner,target,1);
    internal static SourceAnimationState Plan(string code,int advance,int frame=0)=>PlayerWeaponSequences.Schedule(14,code,advance,0,frame);
    internal static ActionTiming Timing(PlayerMobile owner)
        =>Timing(PaladinZeal.AnimationCode(owner),CombatTiming.WeaponSequenceAdvance(owner,CombatAbility.LeapAttack));
    internal static ActionTiming Timing(string code,int advance)
    {
        if(code==null||PlayerWeaponSequences.Profile(14,code).Points==0){return new(0,0);}
        var p=Plan(code,advance);
        return new(p.Events.Single(e=>e.Type==2).Frame,p.Events.First(e=>e.Type==1).Frame);
    }
    internal static LeapAttackFlight Capture(PlayerMobile owner,uint selected,out int windup)
    {
        using var melee=MeleeContext(owner);
        string code=PaladinZeal.AnimationCode(owner);int advance=CombatTiming.WeaponSequenceAdvance(owner,CombatAbility.LeapAttack);
        var initial=Plan(code,advance).Events.Where(e=>e.Type==1).ToArray();windup=initial[0].Frame;
        var row=PlayerWeaponSequences.Profile(14,code);var state=new SourceAnimationState(0,advance,row.Frames*256,row.Rows.Select(r=>r[4]).ToArray(),[]);
        int period=SourceAnimationEvents.AtPoint(state,100,10).Events.First(e=>e.Type==1).Frame-100;
        var reset=SourceAnimationEvents.AtPoint(state,100,16);
        return new(selected,CombatTiming.Equipped(owner)?.Serial.Value??0,code,advance,
            reset.Events.First(e=>e.Type==1).Frame-100,reset.Events.Single(e=>e.Type==2).Frame-100,
            initial[1].Frame-windup,period);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>BarbarianLeap.BeginAttack(owner,selected,point,now);
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D requested,out Point3D destination)
    {
        destination=default;var map=owner.Map;
        if(selected!=Serial.Zero)
        {if(World.FindMobile(selected) is not BaseCreature target||!Recipient(owner,target)){return false;}requested=target.Location;}
        // Shared native sight/terrain boundary. Exact source rooms,footprints
        // and modern long-distance input remain explicit port acceptance work.
        if(!owner.InRange(requested,CombatProtocol.SightRange)||!LeapTerrain.Allowed(owner,map,owner.Location)
            ||!LeapTerrain.Allowed(owner,map,requested)){return false;}
        var origin=new LeapCell(owner.X*2,owner.Y*2);var aim=new LeapCell(requested.X*2,requested.Y*2);
        int distance=SkillGeometry.SourceDistance(aim.X-origin.X,aim.Y-origin.Y);if(distance==0){return false;}
        var candidate=new LeapCell(aim.X+2*(aim.X-origin.X)/distance,aim.Y+2*(aim.Y-origin.Y)/distance);
        var points=new Dictionary<LeapCell,Point3D?>();
        Point3D? Ground(LeapCell c)
        {
            if(!points.TryGetValue(c,out var p))
            {p=LeapTerrain.Ground(owner,map,c.X/2,c.Y/2,requested.Z,out var found)?found:null;points.Add(c,p);}return p;
        }
        // The old melee early return is superseded by released2.4 adjacent
        // leaps. The distinct beyond-target/free-cell search is retained.
        if(Ground(candidate)==null)
        {
            var free=SourceFreeCoordinates.Find(aim,c=>Ground(c)!=null);if(free==null){return false;}candidate=free.Value;
        }
        if(Ground(candidate) is not{} landing||!LeapTerrain.Flying(map,owner.Location,landing)){return false;}
        destination=landing;return true;
    }
    internal static BaseCreature FindTarget(PlayerMobile owner,uint selected)
    {
        if(World.FindMobile((Serial)selected) is BaseCreature saved&&Recipient(owner,saved)){return saved;}
        int range=PlayerMeleeGeometry.Reach(owner)+4;double radius=SkillGeometry.Tiles(range);
        var candidates=SkillCombat.NearbySnapshot<BaseCreature>(owner.Map,owner.Location,(int)Math.Ceiling(radius))
            .Where(t=>Recipient(owner,t)&&(long)(t.X-owner.X)*(t.X-owner.X)+(long)(t.Y-owner.Y)*(t.Y-owner.Y)<=radius*radius).ToArray();
        int count=0;var chosen=SourceGuidTargets.Find(new(0,0,range,0x20003,uint.MaxValue),
            (_,_,_,_)=>candidates.Select(t=>new SourceGuidTarget(unchecked((int)t.Serial.Value),t.Serial.Value)),ref count);
        return chosen==null?null:candidates.First(t=>t.Serial.Value==chosen.Id);
    }
    internal static LeapAttackFlight Land(PlayerMobile owner,LeapFlight flight)
    {
        var attack=flight.Attack;var values=SkillMechanics.Values(owner,CombatAbility.LeapAttack);
        // Approved Sanctuary adaptation: one physical spell hit per eligible
        // recipient,then the distinct later ordinary weapon contact. No Leap KB.
        foreach(var target in SkillCombat.Targets(owner,owner.Location,SkillGeometry.Tiles(7)))
        {BarbarianWarCry.PhysicalHit(owner,target,values.PhysicalDamage256(false),values.PhysicalDamage256(true),AreaRoll);}
        var targetAtLanding=FindTarget(owner,attack.Target);
        if(targetAtLanding==null){return attack with{Target=0,ContactFrame=0,EndFrame=4,Frame=0,Released=true};}
        var p=PlayerWeaponSequences.Profile(14,attack.Code);
        // Use a safe supplied game epoch; only relative event deadlines leave
        // this helper. Absolute point16 is not a relative16-frame rewind.
        var reset=SourceAnimationEvents.AtPoint(new(0,attack.Advance,p.Frames*256,p.Rows.Select(r=>r[4]).ToArray(),[]),100,16);
        return attack with{Target=targetAtLanding.Serial.Value,ContactFrame=reset.Events.First(e=>e.Type==1).Frame-100,
            EndFrame=reset.Events.Single(e=>e.Type==2).Frame-100,Frame=0,Released=false};
    }
    internal static void Strike(PlayerMobile owner,LeapAttackFlight attack)
    {
        using var melee=MeleeContext(owner);
        if((CombatTiming.Equipped(owner)?.Serial.Value??0)!=attack.Weapon||!WeaponAllowed(owner)
            ||owner.Frozen||owner.Paralyzed||HitRecovery.Active(owner,Core.TickCount)||SkillMechanics.Rank(owner,CombatAbility.LeapAttack)<1){return;}
        var target=FindTarget(owner,attack.Target);if(target==null){return;}
        try
        {
            var v=SkillMechanics.Values(owner,CombatAbility.LeapAttack);
            var packet=PlayerWeaponContact.PrepareOrdinary(owner,target,(int)v.Formula("toht"),(int)v.Eval("calc1"),
                ()=>InRange(owner,target),HitRoll,ElementRoll,PhysicalRoll,PoisonRoll,Effects,true);
            if((packet.Flags&SourceMeleeResult.Successful)!=0){packet=packet with{Flags=packet.Flags|8,SkillKnockback=true,HitFlags=0x20};}
            packet=packet with{RangeCheck=bonus=>Recipient(owner,target)&&PlayerMeleeGeometry.Within(owner,target,bonus)};
            owner.Direction=owner.GetDirectionTo(target);owner.DoHarmful(target,true);owner.PlaySound(0x238);
            var result=PlayerWeaponContact.Release(owner,target,packet,TimedEffects.Now,true);
            if(result.Exit==MonsterContactExit.Finished&&(result.Packet.ResultFlags&SourceMeleeResult.Successful)!=0)
            {CombatStuns.Clear(target);}
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);}
    }
    internal static bool ValidSaved(LeapAttackFlight a)=>a!=null&&(a.Target==0||((Serial)a.Target).IsMobile)
        &&(a.Weapon==0||((Serial)a.Weapon).IsItem)&&a.Advance is >=38 and <=448&&a.Code is "HTH"or"1HT"or"2HT"or"1HS"or"2HS"or"STF"or"1JS"or"1JT"or"1SS"or"1ST"or"HT1"or"HT2"
        &&a.ContactFrame>=0&&a.EndFrame>=1&&a.EndFrame<=10000&&a.Frame>=0&&a.Frame<=a.EndFrame
        &&a.CheckRemaining is >=0 and <=10000&&a.CheckPeriod is >=1 and <=10000;
}
