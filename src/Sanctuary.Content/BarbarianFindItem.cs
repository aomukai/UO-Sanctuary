// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class BarbarianFindItem
{
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<Random> LootRandom=()=>Random.Shared;
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var corpse=World.FindItem(selected) as Corpse;
        // Retains the current shared native corpse-access footprint. Modern
        // input/auto-approach and exact h2h/shift range remain acceptance work.
        return CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
            && CorpseSkillRules.Profiles.Find(corpse).Treasure is{} context && CreatureGold.HasTreasureClass(context.TreasureClass)
            && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(corpse.Location,owner);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!ValidTarget(owner,selected) || !SkillCombat.Begin(owner,CombatAbility.FindItem,selected,((Corpse)World.FindItem(selected)).Location,now,(p,_,id,_)=>ValidTarget(p,id))){return false;}
        // Source start34 pays on successful start. The later chance failure or
        // cancellation cannot refund this payment or debit it a second time.
        if(NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,CombatAbility.FindItem).Mana256)){ClassCombat.For(owner).FindItemTarget=selected;return true;}
        ClassCombat.Stop(owner);return false;
    }
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        var session=ClassCombat.For(owner);bool paid=selected!=Serial.Zero && session.FindItemTarget==selected;session.FindItemTarget=Serial.Zero;
        if(!paid){return false;}
        if(!BarbarianCombat.Available(owner,CombatAbility.FindItem) || !ValidTarget(owner,selected)){return false;}
        var corpse=(Corpse)World.FindItem(selected);var context=CorpseSkillRules.Profiles.Find(corpse).Treasure;
        if(!CorpseSkillRules.Reserve(corpse)){return false;}
        // Both success and failure consume the attempt without hiding the body
        // or touching its existing contents. Keep the current shared DM model
        // pending the separately recorded modern reference disagreement.
        int chance=(int)SkillMechanics.Values(owner,CombatAbility.FindItem).Eval("calc1");
        owner.PlaySound(0x1E5);Effects.SendLocationEffect(corpse.Location,corpse.Map,0x375A,5,10,0x481,0); // Identified cry/search placeholders.
        if(Math.Clamp(Roll(100),0,99)>=chance){return false;}
        _=Roll(100); // Source secondary nParam draw; reviewed wrapper ignores it.
        GearDrops.FindItem(owner,corpse,context,LootRandom());return true;
    }
}
