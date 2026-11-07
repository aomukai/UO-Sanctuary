// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record ShadowResourceBirth(ShadowOwnerSnapshot Capture,ShadowInitialization Initialization,MonsterDifficulty Difficulty);
internal static class ShadowEquipmentResources
{
    internal static void Validate(ShadowResourceBirth s)
    {
        if(s?.Capture==null||s.Initialization==null||!Enum.IsDefined(s.Difficulty))
        {throw new InvalidDataException("Missing original Shadow resource birth");}
        s.Capture.Validate();ShadowInitializer.Validate(s.Initialization);
        if(s.Capture.Ability!=s.Initialization.Ability){throw new InvalidDataException("Shadow resource species changed");}
    }
    internal static EquipmentStatValue[] Basis(ShadowResourceBirth s)
    {Validate(s);return ShadowInitializer.Basis(s.Initialization,s.Capture.Level);}
    internal static void Require(AssassinShadowBody actor,GeneratedNpcResourceState s,MonsterDifficulty? preview)
    {
        var source=s.Shadow;Validate(source);var state=actor.Prepared;
        if(state is not{Equipment:not null}||actor.Map!=Map.Internal&&!ShadowEquipmentDefense.NumericWorld(actor)
            ||((IPersistentMinion)actor).CanActivate&&!ShadowGroundMotion.Active(actor)
            ||state.Capture!=source.Capture||state.Initialization!=source.Initialization||(preview??state.Difficulty)!=source.Difficulty
            ||s.DamageRegeneration!=NecromancerSummons.Source(actor.SourceMonsterId,"DamageRegen")||!s.Basis.SequenceEqual(Basis(source)))
        {throw new NotSupportedException("Shadow resources need their original captured parked numeric recipient");}
        _=ShadowLoadout.Originals(actor);
    }
    internal static GeneratedNpcStatGraph Numeric(AssassinShadowBody actor)
    {
        var state=actor.Prepared??throw new NotSupportedException("Missing original Shadow birth");_=ShadowLoadout.Originals(actor);
        return new(actor,ShadowInitializer.Basis(state.Initialization,state.Capture.Level),_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},
            activeOrder:state.Equipment.Attempts.Where(a=>a.Success).Select(a=>a.Item).ToArray(),
            beforeReplay:g=>ApplyBirth(g,new(state.Capture,state.Initialization,state.Difficulty)));
    }
    internal static ShadowCreationStats ApplyBirth(GeneratedNpcStatGraph graph,ShadowResourceBirth s)
        =>ShadowBirthGraph.Apply(s.Capture.Values(),s.Capture.Values(),graph,s.Capture.Accrued,
            name=>throw new NotSupportedException("Uncaptured Shadow modifier stat: "+name));
}
