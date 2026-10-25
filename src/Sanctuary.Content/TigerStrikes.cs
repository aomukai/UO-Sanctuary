// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class TigerStrikes
{
    internal sealed record Pending(Serial Target,bool Basic,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    // Reuse source weapon-class selection, including dual claws' HT1/HT2.
    internal static ActionTiming Timing(string code,int advance)
    {
        var (frames,eventFrame)=code switch{"HTH"=>(11,6),"HT1" or "HT2"=>(11,6),"1HS" or "1HT"=>(15,7),"2HS"=>(23,11),"2HT"=>(23,10),"STF"=>(19,9),_=>(0,0)};
        if(frames==0){throw new NotSupportedException("Unmapped Tiger Strike A1 weapon class: "+code);}advance=Math.Max(1,advance);
        return new((frames*256+advance-1)/advance,(eventFrame*256+advance-1)/advance);
    }
    internal static ActionTiming Timing(PlayerMobile owner)
    {
        string code=PaladinZeal.AnimationCode(owner);int baseSpeed=code is "HT1" or "HT2"?208:256;
        int advance=CombatTiming.FrameAdvance(CombatTiming.Weapon(CombatTiming.Equipped(owner)),CombatTiming.Ias(owner),GearEffects.AttackSlow(owner),PaladinOffense.AttackRate(owner)+AssassinBuffs.AttackRate(owner),baseSpeed:baseSpeed);
        return Timing(code,advance); // cached-lg-assassin-a1-v1; current timing acceptance open.
    }
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)=>PaladinVengeance.ValidTarget(owner,selected);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AssassinCombat.Available(owner,CombatAbility.TigerStrike) || !ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.TigerStrike);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        if(!SkillCombat.Begin(owner,CombatAbility.TigerStrike,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id),requireMana:!basic,timing:Timing(owner))){return false;}
        var session=ClassCombat.For(owner);
        // Generic source start wrapper pays after successful start23. No hit or
        // damage snapshot is made here: do34 rolls both at the action event.
        if(!basic && !NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.TigerAttack=new(selected,basic,session.EffectAt);return true;
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var pending=session.TigerAttack;
        if(pending==null || now<pending.EffectAt){return false;}session.TigerAttack=null;
        if(pending.Target!=selected || !AssassinCombat.Available(owner,CombatAbility.TigerStrike) || !ValidTarget(owner,selected)){return false;}
        var target=World.FindMobile(selected);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified weapon pose/sound placeholder.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(pending.Basic){BasicStrike(owner,target);CombatDamage.Finish(target,owner);return true;}
        var values=SkillMechanics.Values(owner,CombatAbility.TigerStrike);
        // Unlike the ordinary finisher, charge builder uses only its own skill AR.
        if(!Successful(owner,target,(int)values.Formula("toht"))){return true;}
        double roll=ElementRoll();var raw=GearEffects.Damage(owner,roll,target,0,PhysicalRoll);
        var packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,raw);
        AssassinCharges.Gain(owner,values); // after allocation, before durability/health commit, even against immunity.
        GearEffects.Strike(owner,target,roll,Effects,preparedImpact:packet);CombatDamage.Finish(target,owner);return true;
    }
    private static bool Successful(PlayerMobile owner,Mobile target,int ar)
    {
        if(ClassStats.CheckHit(owner,target,ar,HitRoll)!=true){return false;}
        // Source result flags exclude blocked contacts before progressive gain.
        // Existing mapped block supplier is shared with weapon missiles/shield.
        if(target is BaseCreature creature && SourceBlocking.TryBlock(creature,true,out bool blocked) && blocked){return false;}
        return true;
    }
    internal static bool BasicStrike(PlayerMobile owner,Mobile target)
    {
        var state=AssassinCharges.State(owner);int attack=state?.AttackPercent??0;
        if(!Successful(owner,target,attack)){return false;}
        int? ed=state==null?null:AssassinCharges.TigerPercent(state,SkillMechanics.Rank(owner,CombatAbility.TigerStrike));
        var policy=CommonMeleeSkills.Attack;
        GearEffects.Strike(owner,target,ElementRoll(),Effects,skillPhysicalPercent:ed,physicalRoll:PhysicalRoll,
            beforeCommit:state==null?null:_=>{AssassinCharges.Consume(owner,state,policy.Number("prgchargesconsumed"));return true;});
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.TigerAttack=null;if(session.PendingAbility==CombatAbility.TigerStrike){session.PendingAbility=CombatAbility.None;}}
}

// Common Attack is not a talent and must not take one of the240 talent identities.
internal static class CommonMeleeSkills
{
    internal static SkillDefinition Attack{get;private set;}
    internal static void Load()
    {
        var rows=BalanceCatalog.Read<SkillDefinition>("common-melee-skills");
        if(rows.Length!=1 || rows[0].SourceKey!="Attack" || rows[0].Number("finishing")!=1 || rows[0].Number("prgchargestocast")!=0 || rows[0].Number("prgchargesconsumed")!=1)
        {throw new InvalidOperationException("Unsupported common melee finisher policy");}Attack=rows[0];
    }
}
