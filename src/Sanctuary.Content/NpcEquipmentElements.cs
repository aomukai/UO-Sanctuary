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
        if(item==null&&actor is not ISourceUnarmedNpc){throw new NotSupportedException("No supported original NPC equipment");}
        return WithBonuses(actor,item?.Elements()??new MissileSourceStats(),item?.Bonus("extra-fire")??0,item?.Bonus("extra-ltng")??0,item?.Bonus("extra-cold")??0,item?.Bonus("extra-mag")??0);
    }
    internal static NpcElementSnapshot WithBonuses(Server.Mobiles.BaseCreature actor,MissileSourceStats raw,int fireMastery,int lightningMastery,int coldMastery,int magicMastery)
    {
        var enchant=SorceressEffects.Enchantment(actor);
        (int Min,int Max) Aura(CombatAbility ability)
        {var aura=PaladinAuras.Effect(actor,ability);return aura==null?(0,0):(checked(aura.WeaponMin*256),checked(aura.WeaponMax*256));}
        var fire=Aura(CombatAbility.HolyFire);var cold=Aura(CombatAbility.HolyFreeze);var lightning=Aura(CombatAbility.HolyShock);
        var innateFire=actor is FireGolemBody golem?golem.InnateFire256:(Min:0,Max:0);
        raw=raw with
        {
            FireMin=checked(raw.FireMin+innateFire.Min+(enchant?.Min256??0)+fire.Min),FireMax=checked(raw.FireMax+innateFire.Max+(enchant?.Max256??0)+fire.Max),
            ColdMin=checked(raw.ColdMin+cold.Min),ColdMax=checked(raw.ColdMax+cold.Max),
            LightningMin=checked(raw.LightningMin+lightning.Min),LightningMax=checked(raw.LightningMax+lightning.Max)
        };
        return new(raw,fireMastery,lightningMastery,coldMastery,magicMastery);
    }
    internal static int Channel(int minimum,int maximum,int mastery,int initial,Func<int,int> limited,int sourceDamage=128)
    {
        if(sourceDamage is <1 or >255){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        int value=maximum>=8?SourceElementRoll.Roll256(minimum,maximum,mastery,mastery,initial,limited):Math.Max(initial,0);
        return sourceDamage==128?value:checked((int)((long)value*sourceDamage/128));
    }
    internal static GearDamage Roll(NpcElementSnapshot snapshot,Func<int,int> limited,int sourceDamage=128,GearDamage initial=default)
    {
        var r=snapshot.Stats;
        if(sourceDamage is <1 or >255){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        int Roll(int min,int max,int mastery,double previous)=>Channel(min,max,mastery,checked((int)(previous*256)),limited,sourceDamage);
        // Ordinary fill rolls fire,lightning,cold,magic in this order. It
        // applies the recipient's mastery to the combined accrued range once.
        int fire=Roll(r.FireMin,r.FireMax,snapshot.FireMastery,initial.Fire);
        int lightning=Roll(r.LightningMin,r.LightningMax,snapshot.LightningMastery,initial.Lightning);
        int cold=Roll(r.ColdMin,r.ColdMax,snapshot.ColdMastery,initial.Cold);
        int magic=Roll(r.MagicMin,r.MagicMax,snapshot.MagicMastery,initial.Magic);
        return new(0,fire/256.0,cold/256.0,lightning/256.0,0,ColdMs:checked(initial.ColdMs+(cold>0?checked((int)((long)r.ColdFrames*sourceDamage/128))*40:0)),Magic:magic/256.0);
    }
}
