// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal sealed record NpcElementSnapshot(MissileSourceStats Stats,int FireMastery,int LightningMastery,int ColdMastery,int MagicMastery);

internal static class NpcEquipmentElements
{
    internal static NpcElementSnapshot Capture(SkeletonGuardian actor)
    {
        var item=NpcEquipmentCombat.Context(actor);
        if(item==null&&actor is not RaisedSkeleton){throw new NotSupportedException("No supported original NPC equipment");}
        var raw=item?.Elements()??new MissileSourceStats();var enchant=SorceressEffects.Enchantment(actor);
        (int Min,int Max) Aura(CombatAbility ability)
        {var aura=PaladinAuras.Effect(actor,ability);return aura==null?(0,0):(checked(aura.WeaponMin*256),checked(aura.WeaponMax*256));}
        var fire=Aura(CombatAbility.HolyFire);var cold=Aura(CombatAbility.HolyFreeze);var lightning=Aura(CombatAbility.HolyShock);
        raw=raw with
        {
            FireMin=checked(raw.FireMin+(enchant?.Min256??0)+fire.Min),FireMax=checked(raw.FireMax+(enchant?.Max256??0)+fire.Max),
            ColdMin=checked(raw.ColdMin+cold.Min),ColdMax=checked(raw.ColdMax+cold.Max),
            LightningMin=checked(raw.LightningMin+lightning.Min),LightningMax=checked(raw.LightningMax+lightning.Max)
        };
        return new(raw,item?.Bonus("extra-fire")??0,item?.Bonus("extra-ltng")??0,item?.Bonus("extra-cold")??0,item?.Bonus("extra-mag")??0);
    }
    internal static GearDamage Roll(NpcElementSnapshot snapshot,Func<int,int> limited)
    {
        var r=snapshot.Stats;
        int Channel(int min,int max,int mastery)=>max>=8?SourceElementRoll.Roll256(min,max,mastery,mastery,0,limited):0;
        // Ordinary fill rolls fire,lightning,cold,magic in this order. It
        // applies the recipient's mastery to the combined accrued range once.
        int fire=Channel(r.FireMin,r.FireMax,snapshot.FireMastery);
        int lightning=Channel(r.LightningMin,r.LightningMax,snapshot.LightningMastery);
        int cold=Channel(r.ColdMin,r.ColdMax,snapshot.ColdMastery);
        int magic=Channel(r.MagicMin,r.MagicMax,snapshot.MagicMastery);
        return new(0,fire/256.0,cold/256.0,lightning/256.0,0,ColdMs:checked(r.ColdFrames*40),Magic:magic/256.0);
    }
}
