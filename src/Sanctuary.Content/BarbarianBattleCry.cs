// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianBattleCry
{
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.BattleCry,selected,point,now,(p,_,_,_)=>BarbarianShout.ValidCast(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.BattleCry) || !BarbarianShout.ValidCast(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.BattleCry);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        BarbarianShout.Cast(owner,values,TimedEffects.Now,CombatAbility.BattleCry);return true;
    }
    // Hit21/filter42883 has no boss/prime-evil or switch-AI restriction.
    // Source NPC/ISATT metadata is required. Unmapped monsters and native PvP
    // remain unsupported rather than borrowing Howl's unrelated fear immunity.
    internal static bool Supported(Mobile target)=>target is BaseCreature creature
        && PaladinConversion.SourceRule(creature) is{Npc:false,Attackable:true};
    internal static bool Hit(PlayerMobile owner,Mobile target,int rank,DateTime at)
    {
        if(rank is <1 or >255 || !Supported(target) || !ClassCombat.IsHostile(owner,target)
            || !owner.InLOS(target) || !Server.Spells.SpellHelper.CheckTown(target.Location,owner)){return false;}
        var old=NecromancerEffects.Curse(target);
        if(old?.Ability==CombatAbility.Attract || old is{Ability:CombatAbility.BattleCry} && old.Rank>rank){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.BattleCry,rank);
        // Current supported native monsters have no curse-resistance adapter.
        // Source zero resistance is explicit; nonzero cases remain unverified.
        int frames=SourceTerror.Duration((int)values.Eval("auralencalc"),0);
        var state=NecromancerEffects.Profiles.Find(target)??new();
        var curse=old is{Ability:CombatAbility.BattleCry} && old.Rank==rank
            ?old with{UntilUtc=at.AddMilliseconds(frames*40L)}
            :new NecromancerCurse(CombatAbility.BattleCry,owner.Serial.Value,rank,at.AddMilliseconds(frames*40L),owner.X,owner.Y);
        NecromancerEffects.Profiles.Store(target,state with{Curse=curse});
        target.FixedEffect(0x375A,10,15,0x481,0); // Identified UO curse marker placeholder.
        owner.DoHarmful(target);return true;
    }
}
