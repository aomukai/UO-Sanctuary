// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinZeal
{
    internal sealed class Series
    {
        internal Serial Target,Weapon;
        internal Map Map;
        internal Point3D Origin;
        internal int Remaining;
        internal long EffectAt;
    }
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static string AnimationCode(PlayerMobile owner)
    {
        var weapon=CombatTiming.Equipped(owner);
        if(weapon!=null && ClassGear.Base(weapon) is{} basis)
        {
            var source=NamedGear.Catalog.Bases.FirstOrDefault(x=>x.Code==basis.Code);
            string field=basis.OneOrTwoHanded?"2handedwclass":"wclass";
            if(source!=null && source.SourceFields.TryGetProperty(field,out var value) && value.ValueKind==System.Text.Json.JsonValueKind.String)
            {return value.GetString()?.ToUpperInvariant();}
        }
        return FamilyCode(CombatTiming.Weapon(weapon).Family,weapon?.Type==WeaponType.Staff);
    }
    internal static string FamilyCode(AttackFamily family,bool staff=false)=>family switch
        {
            AttackFamily.Unarmed=>"HTH",AttackFamily.Thrust=>"1HT",AttackFamily.Swing=>"1HS",
            AttackFamily.TwoHandedThrust=>"2HT",AttackFamily.TwoHandedSwing=>staff?"STF":"2HS",_=>null
        };
    internal static (int Frames,int Event) Animation(string code)=>code switch
    {
        "HTH"=>(14,7),"1HS"=>(15,7),"1HT"=>(17,8),"2HS"=>(18,8),"2HT"=>(20,8),"STF"=>(18,9),_=>(0,0)
    };
    internal static ActionTiming Timing(string code,int advance)
    {
        var row=Animation(code);advance=Math.Max(1,advance);
        // Original Paladin A1 frame bonus is zero. Zeal Param2=100 rolls back to zero.
        return new(Math.Max(2,(row.Frames*256+advance-1)/advance),Math.Max(1,(row.Event*256+advance-1)/advance));
    }
    internal static ActionTiming Timing(PlayerMobile owner)
    {
        var weapon=CombatTiming.Weapon(CombatTiming.Equipped(owner));int ias=CombatTiming.Ias(owner);
        int rate=Math.Clamp(100+120*ias/(120+ias)+PaladinOffense.AttackRate(owner)-weapon.Modifier-Math.Clamp(GearEffects.AttackSlow(owner),0,85),15,175);
        return Timing(AnimationCode(owner),256*rate/100);
    }
    internal static uint NextSerial(IEnumerable<uint> candidates,uint last)
    {
        uint first=uint.MaxValue,next=uint.MaxValue;
        foreach(uint id in candidates){first=Math.Min(first,id);if(id>last){next=Math.Min(next,id);}}
        return next!=uint.MaxValue?next:first!=uint.MaxValue?first:0;
    }
    private static List<Mobile> Candidates(PlayerMobile owner)
    {
        var targets=new List<Mobile>();
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,CombatTiming.Range(owner,CombatAbility.BasicAttack)))
        {if(PaladinVengeance.ValidTarget(owner,target.Serial)){targets.Add(target);}}
        return targets;
    }
    internal static Mobile Target(PlayerMobile owner,Serial selected,bool cycle=false)
    {
        if(!cycle && PaladinVengeance.ValidTarget(owner,selected)){return World.FindMobile(selected);}
        var candidates=Candidates(owner);uint id=NextSerial(candidates.Select(x=>x.Serial.Value),selected.Value);
        return candidates.FirstOrDefault(x=>x.Serial.Value==id);
    }
    public static bool ValidTarget(PlayerMobile owner,Serial selected)=>Animation(AnimationCode(owner)).Frames>0 && Target(owner,selected)!=null;
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!PaladinCombat.Available(owner,CombatAbility.Zeal)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Zeal);
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {
            // Approved Sanctuary deviation: pinned D2R stops; we use an ordinary attack.
            // Retain the Zeal binding/held input so a later affordable cycle resumes Zeal.
            if(!ValidTarget(owner,selected)){return false;}
            var target=Target(owner,selected);
            if(!ClassCombat.Perform(owner,CombatAbility.BasicAttack,target,target.Location,now)){return false;}
            var fallback=ClassCombat.For(owner);
            fallback.PendingLeft=fallback.HeldAbility==CombatAbility.Zeal && fallback.HeldAttack;
            fallback.Zeal=null;fallback.ZealPaid=false;fallback.ZealHits=0;
            return true;
        }
        if(!SkillCombat.Begin(owner,CombatAbility.Zeal,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id))){return false;}
        // Original successful srv-start consumes mana; rollback do-events do not pay again.
        var session=ClassCombat.For(owner);
        session.Zeal=null;
        session.ZealHits=(int)values.Eval("calc1");
        session.ZealPaid=NecromancerEffects.Pay(owner,values.Mana256);
        if(!session.ZealPaid){session.PendingAbility=CombatAbility.None;return false;}
        return true;
    }
    public static bool Locked(PlayerMobile owner,long now)
    {
        if(!owner.Alive || owner.Deleted || !ClassCombat.Enabled(owner) || !AlliedOverlap.Allowed(owner)){return false;}
        var session=ClassCombat.For(owner);
        return session.PendingAbility==CombatAbility.Zeal || session.Zeal!=null && now-session.NextAction<0;
    }
    private static bool Strike(PlayerMobile owner,Mobile target)
    {
        if(!PaladinVengeance.ValidTarget(owner,target?.Serial??Serial.Zero)){return false;}
        owner.Direction=owner.GetDirectionTo(target);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound placeholder.
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        var values=SkillMechanics.Values(owner,CombatAbility.Zeal);
        if(ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)==true)
        {GearEffects.Strike(owner,target,ElementRoll(),skillPhysicalPercent:(int)values.Eval("calc2"),physicalRoll:Roll);}
        return true; // A miss/block still consumes one attack from the series.
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);bool paid=session.ZealPaid;int hits=session.ZealHits;session.ZealPaid=false;session.ZealHits=0;
        if(!paid || !PaladinCombat.Available(owner,CombatAbility.Zeal)){return false;}
        var target=Target(owner,selected);
        session.Zeal=new(){Target=target?.Serial??selected,Weapon=CombatTiming.Equipped(owner)?.Serial??Serial.Zero,Map=owner.Map,Origin=owner.Location,Remaining=target==null?0:hits};
        // No replacement means no rollback; the already scheduled animation still finishes.
        if(target==null){return false;}
        return Advance(owner,session,now);
    }
    private static bool Advance(PlayerMobile owner,ClassCombat.Session session,long now)
    {
        var series=session.Zeal;var target=Target(owner,series.Target);
        if(target==null || !Strike(owner,target)){series.Remaining=0;return false;}
        --series.Remaining;
        if(series.Remaining>0 && owner is{Alive:true,Deleted:false} && Target(owner,target.Serial,true) is{} next)
        {
            series.Target=next.Serial;session.Timing=Timing(owner);series.EffectAt=now+session.Timing.EffectMs;
            session.NextAction=now+session.Timing.DurationMs;
            ClassCombat.SendAnimation(owner,session.Animation,session.Timing.DurationMs);
            owner.Animate(session.Animation,5,1,true,false,0);
        }
        else{series.Remaining=0;}
        ClassCombat.SendState(owner,session);return true;
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.Zeal;
        if(series==null){return false;}
        if(!owner.Alive || owner.Deleted || !ClassCombat.Enabled(owner) || !AlliedOverlap.Allowed(owner) || owner.Frozen || owner.Paralyzed
            || owner.Spell!=null || !PaladinCombat.Available(owner,CombatAbility.Zeal) || owner.Map!=series.Map || owner.Location!=series.Origin
            || (CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=series.Weapon)
        {session.Zeal=null;return false;}
        if(series.Remaining<=0){if(now-session.NextAction>=0){session.Zeal=null;}return false;}
        if(now-series.EffectAt<0){return false;}
        // One event per update; late ticks never deliver a burst of overdue weapon strikes.
        return Advance(owner,session,now);
    }
}
