// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class TigerStrikes
{
    internal sealed record Pending(Serial Target,bool Basic,long EffectAt,CombatAbility Ability=CombatAbility.TigerStrike);
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.TigerStrike or CombatAbility.CobraStrike;
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
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>Begin(owner,CombatAbility.TigerStrike,selected,point,now);
    internal static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability) || !AssassinCombat.Available(owner,ability) || !ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id),requireMana:!basic,timing:Timing(owner))){return false;}
        var session=ClassCombat.For(owner);
        // Generic source start wrapper pays after successful start23. No hit or
        // damage snapshot is made here: do34 rolls both at the action event.
        if(!basic && !NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.TigerAttack=new(selected,basic,session.EffectAt,ability);return true;
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)=>Release(owner,CombatAbility.TigerStrike,selected,now);
    internal static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var pending=session.TigerAttack;
        if(pending==null || now<pending.EffectAt){return false;}session.TigerAttack=null;
        if(pending.Ability!=ability || pending.Target!=selected || !AssassinCombat.Available(owner,ability) || !ValidTarget(owner,selected)){return false;}
        var target=World.FindMobile(selected);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified weapon pose/sound placeholder.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(pending.Basic){BasicStrike(owner,target);CombatDamage.Finish(target,owner);return true;}
        var values=SkillMechanics.Values(owner,ability);
        ChargeContact(owner,target,values);return true;
    }
    internal static bool ChargeContact(PlayerMobile owner,Mobile target,SkillValues values)
    {
        // Unlike the ordinary finisher, charge builder uses only its own skill AR.
        if(!Successful(owner,target,(int)values.Formula("toht"))){return false;}
        double roll=ElementRoll();var raw=GearEffects.Damage(owner,roll,target,0,PhysicalRoll);
        var packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,raw);
        AssassinCharges.Gain(owner,values); // after allocation, before durability/health commit, even against immunity.
        GearEffects.Strike(owner,target,roll,Effects,preparedImpact:packet,sourceDrain:WeaponLeech.Capture(owner));CombatDamage.Finish(target,owner);return true;
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
        var states=AssassinCharges.Finisher(owner);int attack=states.Sum(x=>x.AttackPercent);
        if(!Successful(owner,target,attack)){return false;}
        var tiger=states.FirstOrDefault(x=>x.Ability==CombatAbility.TigerStrike);var cobra=states.FirstOrDefault(x=>x.Ability==CombatAbility.CobraStrike);
        int? ed=tiger==null?null:AssassinCharges.TigerPercent(tiger,SkillMechanics.Rank(owner,CombatAbility.TigerStrike));
        var drain=AssassinCharges.CobraDrain(cobra,SkillMechanics.Rank(owner,CombatAbility.CobraStrike));
        var fire=FistsFireRelease.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.FistsOfFire));
        var lightning=MartialLightning.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.ClawsOfThunder));
        double roll=ElementRoll();var raw=GearEffects.Damage(owner,roll,target,ed,PhysicalRoll,Utility.Random);
        var packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,raw,
            skillElements:fire==null && lightning==null?null:new GearDamage(0,(fire?.Inline256??0)/256.0,0,(lightning?.Inline256??0)/256.0,0,ColdMs:0),physicalToFirePercent:fire?.ConversionPercent??0);
        var center=target.Location;var map=target.Map;
        var policy=CommonMeleeSkills.Attack;
        GearEffects.Strike(owner,target,roll,Effects,preparedImpact:packet,sourceDrain:WeaponLeech.Capture(owner,drain),softHit:fire!=null || lightning!=null,
            beforeCommit:states.Length==0?null:_=>{foreach(var state in states){AssassinCharges.Consume(owner,state,policy.Number("prgchargesconsumed"));}return true;});
        FistsFireRelease.AfterParent(owner,center,map,fire,TimedEffects.Now);
        MartialLightning.AfterParent(owner,center,map,lightning,TimedEffects.Now);
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.TigerAttack=null;if(Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}}
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
