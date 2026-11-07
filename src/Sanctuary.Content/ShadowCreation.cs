// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Captured current helper0x4473c0. These services preserve its call boundaries;
// they do not assert the identity of the outer indirect skill dispatcher.
internal abstract class ShadowBirthServices
{
    internal abstract int LifePercentPerRank();
    internal abstract int MaximumLife256();
    internal abstract void UnitStat(int stat,int value);
    internal abstract bool AllocateList();
    internal abstract void PostList();
    internal abstract int StatCount{get;}
    internal abstract int AuraStat(int slot);
    internal abstract int PassiveStat(int slot);
    internal abstract int OwnerAura(int slot);
    internal abstract int OwnerPassive(int slot);
    internal abstract void ListStat(int stat,int value);
    internal abstract int PetModifier();
    internal abstract void AssignModifier(int modifier);
}

internal static class ShadowBirth
{
    internal static void Apply(int rank,ShadowBirthServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if(rank<1){return;}
        int percent=unchecked(services.LifePercentPerRank()*(rank-1));
        int before=services.MaximumLife256();
        int life=unchecked(before+SourcePercentage.Calculate(before,percent,100));
        services.UnitStat(7,life);services.UnitStat(6,life);
        if(!services.AllocateList()){return;}
        services.PostList();
        for(int i=0;i<6;i++)
        {int stat=services.AuraStat(i);if(stat>=0&&stat<services.StatCount){services.ListStat(stat,services.OwnerAura(i));}}
        for(int i=0;i<14;i++)
        {int stat=services.PassiveStat(i);if(stat>=0&&stat<services.StatCount){services.ListStat(stat,services.OwnerPassive(i));}}
        int modifier=services.PetModifier();
        if(modifier is>=1 and<=44){services.AssignModifier(modifier);}
    }
}

// Requested writes,not post-stat-writer HP or a live actor. In particular an
// overflowing source result is retained;native creation must qualify it.
internal sealed record ShadowCreationStats(CombatAbility Ability,int Rank,int SpawnMaximumLife256,
    int MaximumLifeWrite256,int CurrentLifeWrite256,bool ListAllocated,SummonStat[] Auras,SummonStat[] Passives,int? Modifier);

internal static class ShadowCreation
{
    private sealed class Plan(SkillValues owner,SkillValues pet,int life,bool allocated,
        Func<string,int> ownerStats,Func<string,int> petStats):ShadowBirthServices
    {
        private readonly SkillDefinition Definition=SkillMechanics.For(owner.Ability);
        internal int Maximum,Current;internal int? Modifier;
        internal readonly List<SummonStat> Auras=[],Passives=[];
        private bool Passive;
        internal override int LifePercentPerRank()=>owner.Param(1);
        internal override int MaximumLife256()=>life;
        internal override void UnitStat(int stat,int value)
        {if(stat==7){Maximum=value;}else if(stat==6){Current=value;}else{throw new NotSupportedException("Unexpected Shadow vital write");}}
        internal override bool AllocateList()=>allocated;
        internal override void PostList(){}
        internal override int StatCount=>EquipmentStatValues.Count;
        private int Id(string prefix,int slot)
        {
            string name=Definition.Text(prefix+(slot+1));
            return name=="0"?-1:EquipmentStatValues.Definition(name)?.Id
                ??throw new NotSupportedException("Unmapped Shadow birth stat: "+name);
        }
        internal override int AuraStat(int slot){Passive=false;return Id("aurastat",slot);}
        internal override int PassiveStat(int slot){Passive=true;return Id("passivestat",slot);}
        internal override int OwnerAura(int slot)=>checked((int)owner.StatEval("aurastatcalc"+(slot+1),ownerStats));
        internal override int OwnerPassive(int slot)=>checked((int)owner.StatEval("passivecalc"+(slot+1),ownerStats));
        internal override void ListStat(int stat,int value)
            =>(Passive?Passives:Auras).Add(new(EquipmentStatValues.Definition(stat).Name,value));
        internal override int PetModifier()=>checked((int)pet.StatEval("sumumod",petStats));
        internal override void AssignModifier(int modifier)=>Modifier=modifier;
    }
    internal static ShadowCreationStats Prepare(SkillValues owner,SkillValues pet,int spawnMaximumLife256,
        bool listAllocated,Func<string,int> ownerStats,Func<string,int> petStats)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(pet);
        ArgumentNullException.ThrowIfNull(ownerStats);ArgumentNullException.ThrowIfNull(petStats);
        if(owner.Ability is not(CombatAbility.ShadowWarrior or CombatAbility.ShadowMaster)
            ||pet.Ability!=owner.Ability||pet.Rank!=owner.Rank||owner.Rank is<1 or>255||spawnMaximumLife256<=0)
        {throw new NotSupportedException("Shadow birth requires matching explicit owner/pet skill contexts and initialized life");}
        var plan=new Plan(owner,pet,spawnMaximumLife256,listAllocated,ownerStats,petStats);
        ShadowBirth.Apply(owner.Rank,plan);
        return new(owner.Ability,owner.Rank,spawnMaximumLife256,plan.Maximum,plan.Current,listAllocated,
            plan.Auras.ToArray(),plan.Passives.ToArray(),plan.Modifier);
    }
}
