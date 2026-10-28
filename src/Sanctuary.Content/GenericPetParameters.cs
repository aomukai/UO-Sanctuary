// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;
// Raw current initializer prefix before unspecified owner-iterator effects.
// The three source columns form one configuration,independent of difficulty.
internal sealed record GenericPetParameters(int LeashSquared,int WarpSquared,int CloseRaw,int WanderRaw,int SwarmRaw,ushort Flags,
    int GatherType,bool AllowSkillBonuses,int ChooseType,int ModeAttack,int ModeAiType,int ModeBonus,
    int MeleeMinSquared,int MeleeMaxSquared,int MeleeBonus,int RangedMinSquared,int RangedMaxSquared,int RangedBonus,int RandomBonus,int ProgressiveBonus)
{
    internal static GenericPetParameters Decode(IReadOnlyList<short> p)
    {
        if(p==null){return new(784,2500,60,0,0,0,0,false,0,16,0,0,0,225,0,25,625,0,0,0);}
        if(p.Count!=24){throw new ArgumentException("Expected eight three-column AI parameters",nameof(p));}
        static int Square(short value)=>(int)value*value;
        // Preserve signed raw fields, unsigned flag bits, independently squared
        // endpoints and the three explicit clamps. Do not sort ranges or clamp
        // fields merely because a label resembles a percentage or enum.
        return new(Square(p[0]),Square(p[1]),p[2],p[3],p[4],unchecked((ushort)p[5]),Math.Clamp((int)p[6],0,2),p[7]!=0,
            Math.Clamp((int)p[8],0,1),p[9],Math.Clamp((int)p[10],0,13),p[11],Square(p[12]),Square(p[13]),p[14],Square(p[15]),Square(p[16]),p[17],p[18],p[19]);
    }
}
internal sealed record GenericPetParameterSource(string Key,short[] Parameters);
internal static class GenericPetProfiles
{
    private static readonly Lazy<IReadOnlyDictionary<string,short[]>> Sources=new(()=>
    {
        var rows=BalanceCatalog.Read<GenericPetParameterSource>("generic-pet-parameters");
        if(rows.Length!=3||rows.Any(x=>string.IsNullOrWhiteSpace(x.Key)||x.Parameters?.Length!=24)||rows.Select(x=>x.Key).Distinct(StringComparer.Ordinal).Count()!=rows.Length)
        {throw new InvalidDataException("Invalid current GenericPet parameter profiles");}
        return rows.ToDictionary(x=>x.Key,x=>x.Parameters.ToArray(),StringComparer.Ordinal);
    });
    internal static GenericPetParameters Raw(string sourceKey)
    {if(sourceKey==null||!Sources.Value.TryGetValue(sourceKey,out var p)){throw new NotSupportedException("No verified GenericPet parameter profile");}return GenericPetParameters.Decode(p);}
    internal static GenericPetParameters Raw(CombatAbility ability)
    {
        if(ability is not(CombatAbility.SummonGoatman or CombatAbility.SummonTainted or CombatAbility.SummonDefiler))
        {throw new NotSupportedException("Unsupported GenericPet summon source");}
        return Raw(SkillMechanics.For(ability).Text("summon"));
    }
}
