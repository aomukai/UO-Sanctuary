// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ReviveOffenseSnapshot(uint Actor,uint Target,string Source,int Level,int AttackSlot,
    NpcPhysicalStats Physical,NpcCriticalChances Critical,NpcElementSnapshot Elements,NpcPoisonSnapshot Poison,NpcElementPierce Pierce);

// An immutable offensive packet input, not a second native damage replay.
// Contact, mitigation, item events and persistent RNG binding remain explicit
// services of the eventual original-body swing consumer.
internal static class ReviveEquipmentOffense
{
    private sealed record Innate(string Mode,string Type);
    private sealed record SourceRow(string Source,int Critical,Innate[] Elements);
    private static readonly Lazy<Dictionary<string,SourceRow>> Sources=new(()=>
    {
        var rows=BalanceCatalog.Read<SourceRow>("revive-offense-sources");
        if(rows.Length!=751||rows.Any(r=>string.IsNullOrWhiteSpace(r.Source)||r.Critical is <0 or >255||r.Elements==null||r.Elements.Length!=3||r.Elements.Any(e=>e==null||e.Mode==null||e.Type==null))
            ||rows.Select(r=>r.Source).Distinct(StringComparer.Ordinal).Count()!=751){throw new InvalidDataException("Invalid pinned Revive offense metadata");}
        return rows.ToDictionary(r=>r.Source,StringComparer.Ordinal);
    });
    internal static ReviveOffenseSnapshot Capture(BaseCreature actor,Mobile target=null,int attackSlot=1)
    {
        if(attackSlot!=1){throw new NotSupportedException("Original attack slot needs its own offensive adapter: "+attackSlot);}
        if(actor is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile owner}
            ||ReviveNativeActivation.State(actor)is not{Setup.EquipmentLifeProfile:ReviveEquipmentCombat.Profile} state
            ||state.Owner!=owner.Serial.Value)
        {throw new NotSupportedException("No prepared original Revive offensive context");}
        return Build(actor,owner.Serial.Value,state.Setup.LifePlan.Source,(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,
            ReviveBeforeDeath.Decode(state.Original.BeforeDeathJson),ReviveEquipmentLife.Items(state),ReviveCombat.DamagePercent(actor),target,attackSlot);
    }
    internal static ReviveOffenseSnapshot Preflight(BaseCreature actor,PlayerMobile owner,ReviveNativeSetup setup,ReviveOriginalState original)
    {
        if(actor==null||actor.Serial.Value!=original.Actor||owner==null||setup.LifePlan==null||setup.CombatPlan==null)
        {throw new NotSupportedException("Original offense preflight lost its source/owner/setup");}
        var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var items=ReviveBeforeDeath.Equipment(before).OrderBy(s=>s.OriginalItem).Select(FrozenEquipmentEffects.Bind).ToArray();
        foreach(var item in items)
        {
            if(item.NumericProperties(setup.CombatPlan.Level).Any(s=>s.Value!=0&&s.Stat is 61 or 63 or 65))
            {throw new NotSupportedException("Original positive attack-drain maxima need their resource/event consumer");}
            _=item.Bonus("dmg-undead%",setup.CombatPlan.Level);_=item.Bonus("dmg-demon%",setup.CombatPlan.Level);
        }
        return Build(actor,owner.Serial.Value,setup.LifePlan.Source,setup.CombatPlan.Level,before,items,setup.CombatPlan.DamagePercent,null,1);
    }
    private static ReviveOffenseSnapshot Build(BaseCreature actor,uint owner,string source,int level,ReviveBeforeDeathState before,
        FrozenEquipmentContext[] items,int revivePercent,Mobile target,int attackSlot)
    {
        var profile=CreatureProfiles.For(actor);var attack=profile.Attack1;
        if(actor.PhysicalDamage!=100||actor.FireDamage!=0||actor.ColdDamage!=0||actor.EnergyDamage!=0||actor.PoisonDamage!=0||actor.ChaosDamage!=0||actor.DirectDamage!=0
            ||profile.ApplyStats&&(attack.Physical is{} physical&&physical!=100||attack.Fire is >0||attack.Cold is >0||attack.Lightning is >0||attack.Poison is >0||attack.Magic is >0))
        {throw new NotSupportedException("Original elemental body attack needs its explicit offensive adapter");}
        if(actor is EncounterCreature encounter&&new[]{EliteModifier.FireEnchanted,EliteModifier.ColdEnchanted,EliteModifier.LightningEnchanted,EliteModifier.ManaBurn,EliteModifier.SpectralHit}.Any(encounter.Has))
        {throw new NotSupportedException("Encounter elemental affix needs its original-body offensive adapter");}
        if(!Sources.Value.TryGetValue(source,out var sourceRow)){throw new NotSupportedException("Original has no pinned offensive metadata: "+source);}
        if(sourceRow.Elements.Any(e=>string.Equals(e.Mode,"A1",StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrEmpty(e.Type)))
        {throw new NotSupportedException("Source attack1 declares innate elemental damage: "+source);}
        int bodyMin=profile.ApplyStats?attack.MinDamage??before.Creature.DamageMinimum:before.Creature.DamageMinimum;
        int bodyMax=profile.ApplyStats?attack.MaxDamage??before.Creature.DamageMaximum:before.Creature.DamageMaximum;
        int nativePercent=0;
        if(actor is EncounterCreature strong&&strong.Inherits(EliteModifier.ExtraStrong))
        {
            if(strong.Grade is not(EncounterGrade.Ordinary or EncounterGrade.Minion)){throw new NotSupportedException("Unreviewed encounter grade damage modifiers");}
            bodyMin=strong.Basis.MinDamage;bodyMax=strong.Basis.MaxDamage;
            nativePercent=EncounterProfiles.ExtraStrong(MonsterVariants.Difficulty(strong),strong.Grade==EncounterGrade.Minion).DamagePercent;
        }
        var weapons=items.Where(item=>item.CombatBase(level).Weapon).ToArray();
        if(weapons.Length>1){throw new NotSupportedException("Multiple original weapons need explicit hand selection");}
        var weapon=weapons.SingleOrDefault();var basis=weapon?.CombatBase(level);
        if(basis!=null&&(ClassItemTypes.Is(basis.Type,"bow")||ClassItemTypes.Is(basis.Type,"xbow")))
        {throw new NotSupportedException("Original missile weapon needs its projectile offensive adapter");}
        int Bonus(string code){int total=0;foreach(var item in items){total=checked(total+item.Bonus(code,level));}return total;}
        bool two=basis!=null&&(basis.TwoHanded||basis.OneOrTwoHanded);
        if(!two&&(bodyMin<0||bodyMax<bodyMin)){throw new NotSupportedException("Original has no captured body damage range");}
        int min=two?0:bodyMin,max=two?0:bodyMax,normal=Bonus("normal-damage"),offWeaponPercent=0;
        foreach(var item in items)
        {
            var row=item.CombatAt(level);
            if(item==weapon){min=checked(min+(two?row.TwoMinimum:row.OneMinimum));max=checked(max+(two?row.TwoMaximum:row.OneMaximum));}
            else{min=checked(min+item.Bonus("dmg-min",level));max=checked(max+item.Bonus("dmg-max",level));offWeaponPercent=checked(offWeaponPercent+item.Bonus("dmg%",level));}
        }
        min=checked(min-normal);max=checked(max-normal);
        int percent=checked(nativePercent+revivePercent+CombatAuras.DamagePercent(actor)+NecromancerEffects.DamageStatPercent(actor)+offWeaponPercent);
        if(GemCombat.Undead(target)){percent=checked(percent+Bonus("dmg-undead%")+(basis!=null&&ClassItemTypes.Is(basis.Type,"blun")?50:0));}
        else if(NamedCombat.Demon(target)){percent=checked(percent+Bonus("dmg-demon%"));}
        var physicalStats=new NpcPhysicalStats(min,max,normal,percent,Bonus("str"),Bonus("dex"),basis!=null,basis?.StrengthCoefficient??0,basis?.DexterityCoefficient??0);
        var elements=new MissileSourceStats();var poison=new MissileSourceStats();
        foreach(var item in items)
        {
            var e=item.Elements(level);var p=item.Poison(level);
            elements=elements with{FireMin=checked(elements.FireMin+e.FireMin),FireMax=checked(elements.FireMax+e.FireMax),
                LightningMin=checked(elements.LightningMin+e.LightningMin),LightningMax=checked(elements.LightningMax+e.LightningMax),
                MagicMin=checked(elements.MagicMin+e.MagicMin),MagicMax=checked(elements.MagicMax+e.MagicMax),
                ColdMin=checked(elements.ColdMin+e.ColdMin),ColdMax=checked(elements.ColdMax+e.ColdMax),ColdFrames=checked(elements.ColdFrames+e.ColdFrames)};
            poison=poison with{PoisonMin=checked(poison.PoisonMin+p.PoisonMin),PoisonMax=checked(poison.PoisonMax+p.PoisonMax),
                PoisonFrames=checked(poison.PoisonFrames+p.PoisonFrames),PoisonCount=checked(poison.PoisonCount+p.PoisonCount)};
        }
        int critical;
        if(profile.ApplyStats&&attack.CriticalChance is{} authored){critical=authored;}
        else{critical=sourceRow.Critical;}
        if(critical is <0 or >255){throw new NotSupportedException("Original intrinsic critical chance is outside source range");}
        return new(actor.Serial.Value,target?.Serial.Value??0,source,level,attackSlot,physicalStats,new(0,0,Bonus("deadly"),critical),
            NpcEquipmentElements.WithBonuses(actor,elements,Bonus("extra-fire"),Bonus("extra-ltng"),Bonus("extra-cold"),Bonus("extra-mag")),
            new(actor.Serial.Value,owner,poison,Bonus("extra-pois"),Math.Max(0,Bonus("pierce-pois"))),
            new(Bonus("pierce-fire"),Bonus("pierce-cold"),Bonus("pierce-ltng"),Bonus("pierce-mag")));
    }
    internal static NpcWeaponRoll Roll(BaseCreature actor,Mobile target=null,int attackSlot=1)
    {
        // Resolve every offensive capability before initializing/advancing
        // the persisted stream. Swing/recipient/event services remain separate.
        var snapshot=Capture(actor,target,attackSlot);
        return Roll(snapshot,n=>NpcEquipmentRandom.Limited(actor,n),()=>NpcEquipmentRandom.Raw(actor));
    }
    internal static NpcWeaponRoll Roll(ReviveOffenseSnapshot snapshot,Func<int,int> limited,Func<uint> raw)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(raw);
        return NpcEquipmentDamage.RollCaptured(snapshot.Actor,snapshot.Target,snapshot.Physical,snapshot.Critical,()=>snapshot.Elements,()=>snapshot.Poison,limited,raw,defender:snapshot.Target!=0);
    }
}
