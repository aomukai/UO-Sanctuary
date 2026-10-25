// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record PreparedKickPacket(KickDamageInput Input,PreparedWeaponImpact Impact,PlayerWeaponDrain Drain,
    int? ColdFrames,int FreezeFrames,bool SoftHit);

// Prepared kick damage and Tail's distinct fire consumer. Learned action,
// charged hit/block, payment and charge consumption remain separate gates.
public static class DragonKickPackets
{
    internal static PreparedKickPacket Prepare(PlayerMobile owner,Mobile target,int skillDamagePercent,double elementRoll,
        Func<int,int> physicalRoll,Func<int,int> poisonRoll,Random effects)
    {
        if(owner==null || target==null){throw new ArgumentNullException();}
        var pair=WeaponHands.Pair(owner);
        using var selected=pair is{} weapons?WeaponHands.Use(owner,weapons.Primary):null;
        var weapon=CombatTiming.Equipped(owner);
        using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
        var states=AssassinCharges.Finisher(owner);
        var tiger=states.FirstOrDefault(x=>x.Ability==CombatAbility.TigerStrike);
        int enhanced=checked(skillDamagePercent+AssassinCharges.TigerPercent(tiger,SkillMechanics.Rank(owner,CombatAbility.TigerStrike)));
        var input=DragonKickDamage.Capture(owner,enhanced);
        int physical=DragonKickDamage.Roll256(input,physicalRoll);
        // The existing elemental collector is deterministic for physical damage
        // unless an explicit physical skill roll is requested. Discard that
        // weapon physical lane; it must never replace body/boot kick damage.
        var damage=GearEffects.Damage(owner,elementRoll,target,poisonRoll:poisonRoll) with{Physical=physical/256.0};
        var fire=FistsFireRelease.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.FistsOfFire));
        var thunder=MartialLightning.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.ClawsOfThunder));
        var ice=MartialCold.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.BladesOfIce));
        var extra=new GearDamage(0,(fire?.Inline256??0)/256.0,(ice?.Inline256??0)/256.0,(thunder?.Inline256??0)/256.0,0,ColdMs:0);
        // Source kick hit flags3 bypass all ordinary physical critical branches.
        // The selected weapon still supplies its typed stats and drain/procs.
        var impact=GearEffects.PrepareWeaponImpact(owner,target,elementRoll,effects,damage,extra,
            projectileCritical:false,physicalToFirePercent:fire?.ConversionPercent??0);
        var cobra=states.FirstOrDefault(x=>x.Ability==CombatAbility.CobraStrike);
        var drain=WeaponLeech.Capture(owner,AssassinCharges.CobraDrain(cobra,SkillMechanics.Rank(owner,CombatAbility.CobraStrike)));
        return new(input,impact,drain,ice?.Frames,ice?.Count==3?ice.Frames:0,fire!=null || thunder!=null || ice!=null);
    }
    internal static int TailFire256(int physical256,SkillValues values,int mastery)
    {
        if(physical256<0 || values.Ability!=CombatAbility.DragonTail || values.Rank is <1 or >255)
        {throw new NotSupportedException("Unmapped Dragon Tail fire packet");}
        // Do50 adds skill percentage and current mastery before one source
        // percentage operation, including its large-value early division.
        int percent=checked((int)values.Eval("calc1")+mastery);
        long intermediate=physical256<=1048576
            ?percent<=65536?(long)physical256*percent
                :100<=percent>>4?(long)physical256*(percent/100):(long)physical256*percent/100
            :100<=physical256>>4?(long)percent*(physical256/100):(long)physical256*percent/100;
        if(intermediate is >int.MaxValue or <int.MinValue)
        {throw new NotSupportedException("Legacy Tail percentage overflows; current executable behavior is unverified");}
        return Math.Max(0,SourcePercentage.Calculate(physical256,percent,100));
    }
    internal static int TailBurst(PlayerMobile owner,Map map,Point3D center,int copiedPhysical256,SkillValues values)
    {
        if(owner is not{Alive:true,Deleted:false} || map==null || map==Map.Internal || owner.Map!=map){return 0;}
        int fire=TailFire256(copiedPhysical256,values,NamedElements.MasteryPercent(owner,"fire"));
        int radius=(int)values.Eval("aurarangecalc");
        if(radius is <1 or >64){throw new NotSupportedException("Unmapped Dragon Tail radius");}
        if(SpellHelper.IsTown(center,owner)){return 0;}
        int loss=0;
        // Source default0x8583 adds hostile eligibility but no optional LOS
        // or NOAURA gate. Shared native creature/height/sector projection is
        // bounded separately from full executable/PvP/flagged-missile parity.
        foreach(var target in map.GetMobilesInRange<BaseCreature>(center,(radius+1)/2))
        {
            if(FistsFireRelease.InRadius(center,target,radius) && ClassCombat.IsHostile(owner,target)
                && !SpellHelper.IsTown(target.Location,target))
            {loss=checked(loss+SkillCombat.FireImpact(owner,target,fire));CombatDamage.Finish(target,owner);}
        }
        Effects.SendLocationEffect(center,map,0x36BD,10,10,0x489,0); // Identified fire-burst placeholder.
        return loss;
    }
}
