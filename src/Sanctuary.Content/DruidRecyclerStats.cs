// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record RecyclerBirth(CombatAbility Ability,int Rank,int Level,int Life256,int Defense,int Attack,
    int Regeneration256,int VineAttackRank,int RecyclerRank,int RequestedEquipmentLevel);
internal sealed record RecyclerSnapshot(RecyclerBirth Birth,int OwnerLevel);

// Shared do115/passive birth. Neither vine has Creeper's poison passive or
// beast-summon synergies; do not copy those channels into these actors.
internal static class DruidRecyclerStats
{
    internal static string Source(CombatAbility ability)=>ability switch
    {CombatAbility.CarrionVine=>"cycleoflife",CombatAbility.SolarCreeper=>"vinecreature",_=>throw new NotSupportedException("Not a recycler vine")};
    internal static string Talent(CombatAbility ability)=>ability switch
    {CombatAbility.CarrionVine=>"druid.carrionvine",CombatAbility.SolarCreeper=>"druid.solarcreeper",_=>throw new NotSupportedException("Not a recycler vine")};
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(values.Ability);bool solar=values.Ability==CombatAbility.SolarCreeper;
        if(values.Rank is <1 or >255||row.SourceId!=(solar?241:231)||row.Text("summon")!=Source(values.Ability)
            ||row.Number("srvdofunc")!=115||row.Text("pettype")!="vine"||values.Eval("petmax")!=1
            ||row.Text("sumskill1")!="Vine Attack"||row.Text("sumskill2")!=(solar?"VineCycler":"CorpseCycler")
            ||row.Text("passivestat1")!="0"||row.Text("aurastate")!="vine_beast"
            ||values.Mana256!=(solar?13+values.Rank:10)*256)
        {throw new NotSupportedException("Unsupported recycler vine route");}
    }
    internal static CreeperSpawn Spawn(CombatAbility ability,MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        string key=Source(ability);int F(string name)=>NecromancerSummons.Source(key,name,difficulty);
        if(NecromancerSummons.Source(key,"noRatio")!=1||NecromancerSummons.Source(key,"Align")!=1
            ||F("minHP")<=0||F("minHP")!=F("maxHP")||F("A1TH")!=0){throw new NotSupportedException("Unsupported recycler base spawn");}
        int life=checked(F("minHP")*256);
        return new(new(life,F("AC"),0),checked(life*NecromancerSummons.Source(key,"DamageRegen"))>>12);
    }
    internal static RecyclerBirth Birth(SkillValues values,MonsterDifficulty difficulty)=>Apply(values,Spawn(values.Ability,difficulty));
    internal static RecyclerBirth Apply(SkillValues values,CreeperSpawn spawn)
    {
        ArgumentNullException.ThrowIfNull(values);Route(values);
        if(spawn?.Stats is not{Life256:>0,Defense:>=0,Attack:>=0}||spawn.Regeneration256<0){throw new ArgumentException("Invalid supplied recycler spawn");}
        var basis=spawn.Stats;
        int life=checked(basis.Life256+SourcePercentage.Calculate(basis.Life256,checked((int)values.Eval("calc1")),100));
        return new(values.Ability,values.Rank,Math.Max(1,checked((int)values.Eval("calc2"))),life,basis.Defense,basis.Attack,
            spawn.Regeneration256,checked((int)values.Eval("sumsk1calc")),checked((int)values.Eval("sumsk2calc")),
            Math.Clamp(checked(3*values.Rank),1,checked((int)values.Formula("ulvl"))));
    }
    internal static RecyclerSnapshot Capture(PlayerMobile owner,CombatAbility ability)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Druid
            ||SkillMechanics.Rank(owner,ability)<1){throw new InvalidOperationException("Recycler needs a live Druid owner");}
        return new(Birth(SkillMechanics.Values(owner,ability),SummonDifficulties.For(owner.Map)),ClassStats.Level(owner));
    }
    internal static RecyclerSnapshot Rescale(RecyclerSnapshot snapshot,MonsterDifficulty difficulty)
    {
        if(snapshot?.Birth is not{} birth||snapshot.OwnerLevel is <1 or >99){throw new ArgumentException("Invalid recycler snapshot");}
        var row=SkillMechanics.For(birth.Ability);
        return snapshot with{Birth=Birth(new(row,key=>key==row.SourceKey?birth.Rank:0,_=>0,snapshot.OwnerLevel),difficulty)};
    }
}
