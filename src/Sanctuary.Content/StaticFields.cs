// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Shared player/NPC Static Field. Damage has no mastery, synergy, potion,
// weapon packet, ordinary hit roll or intrinsic missile critical component.
internal static class StaticFields
{
    internal static int FloorPercent(MonsterDifficulty difficulty)=>difficulty switch
    {MonsterDifficulty.Normal=>0,MonsterDifficulty.Nightmare=>33,MonsterDifficulty.Hell=>50,_=>throw new ArgumentOutOfRangeException(nameof(difficulty))};
    internal static int Raw256(int life256,int percent,int minimum256)
    {
        if(life256<0||percent is <0 or >100||minimum256<0){throw new ArgumentOutOfRangeException();}
        int whole=life256>>8;if(whole<1){return 0;}
        // The source callback shifts HP down before its percentage helper;
        // preserve fractional HP in the receiver rather than rounding it up.
        int amount=Math.Min(whole-1,SourcePercentage.Calculate(whole,percent,100));
        return Math.Max(checked(amount*256),minimum256);
    }
    internal static int Limit256(int life256,int maximum256,int floorPercent,int damage256)
    {
        if(life256<0||maximum256<0||floorPercent is <0 or >100||damage256<0){throw new ArgumentOutOfRangeException();}
        int floor=Math.Max(256,checked(SourcePercentage.Calculate(maximum256>>8,floorPercent,100)*256));
        return Math.Min(damage256,Math.Max(0,life256-floor));
    }
    internal static bool Cast(Mobile caster,PlayerMobile allegiance,SkillValues values)
    {
        if(caster is not{Alive:true,Deleted:false}||allegiance is not{Alive:true,Deleted:false}||caster.Map==null||caster.Map==Map.Internal
            ||caster.Map!=allegiance.Map||SpellHelper.IsTown(caster.Location,caster)||values.Ability!=CombatAbility.StaticField||values.Rank<1){return false;}
        var definition=SkillMechanics.For(CombatAbility.StaticField);
        if(definition.SourceId!=42||definition.Number("srvdofunc")!=20||definition.Text("EType")!="ltng")
        {throw new NotSupportedException("Unmapped Static Field route");}
        int floor=FloorPercent(SummonDifficulties.For(caster.Map));
        foreach(var target in SkillCombat.Targets(caster,allegiance,caster.Location,SkillGeometry.Tiles(values.Eval("aurarangecalc"))))
        {
            if(SpellHelper.IsTown(target.Location,target)||ClassCombat.Friendly(caster,target)){continue;}
            int life=SkillHealth.Balance256(target),maximum=SkillHealth.Maximum256(target);
            int raw=Raw256(life,checked((int)values.Eval("calc1")),checked((int)values.Eval("calc2")));
            if(Limit256(life,maximum,floor,raw)==0){continue;}
            caster.DoHarmful(target,true);
            // Native common mitigation is reused; legacy negative-resistance
            // precompensation and full modern Static event order remain open.
            var parts=SkillCombat.MitigatePrepared(caster,target,new(0,0,0,raw/256.0,0));
            int loss=Limit256(SkillHealth.Balance256(target),maximum,floor,NamedElements.To256(parts.Total));
            if(loss>0){SkillHealth.Damage(caster,target,loss);}
        }
        caster.FixedEffect(0x3818,10,10,0x482,0); // Identified Static Field placeholder.
        return true;
    }
}
