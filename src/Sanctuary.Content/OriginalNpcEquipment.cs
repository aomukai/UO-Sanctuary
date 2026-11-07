// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

// Species boundary for the existing original-item spell/proc host. This is
// not world admission: unfinished Shadow action/defense profiles stay parked.
internal static class OriginalNpcEquipment
{
    internal static bool WorldEnabled(SkeletonGuardian actor)=>actor switch
    {
        AmazonValkyrieBody=>true,
        AssassinShadowBody=>false,
        _=>false
    };
    internal static uint Owner(SkeletonGuardian actor)=>actor switch
    {
        AmazonValkyrieBody body=>body.Prepared?.Owner??0,
        AssassinShadowBody body=>body.Prepared?.Owner??0,
        _=>throw new NotSupportedException("Original NPC equipment needs its own species adapter")
    };
    internal static string Binding(SkeletonGuardian actor)=>actor switch
    {
        AmazonValkyrieBody body=>ValkyrieEquipmentWeapons.Binding(body).Identity,
        AssassinShadowBody body=>ShadowLoadout.Binding(body).Identity,
        _=>throw new NotSupportedException("Original NPC equipment needs its own original hand binding")
    };
    internal static int Level(SkeletonGuardian actor)=>actor switch
    {
        AmazonValkyrieBody body=>body.BirthStats.Level,
        AssassinShadowBody body=>body.Prepared.Capture.Level,
        _=>throw new NotSupportedException("Original NPC equipment needs its captured source level")
    };
    internal static GeneratedNpcStatGraph Graph(SkeletonGuardian actor)
    {
        if(actor is AmazonValkyrieBody valkyrie)
        {
            _=ValkyrieEquipmentDamage.Passives(valkyrie);return valkyrie.EquipmentNumbers;
        }
        if(actor is AssassinShadowBody shadow)
        {
            _=ShadowLoadout.Binding(shadow);
            var graph=shadow.EquipmentNumbers;
            if(graph?.Passives==null||GeneratedNpcLiveResources.State(shadow)?.Passives?.Book!=shadow.Prepared.Skills)
            {throw new NotSupportedException("Original Shadow spell context needs its actual posted passive/skill graph");}
            // These item spells have Sorceress synergies. Shadows' Assassin
            // entries cannot supply them; do not silently discard a future
            // intrinsic off-class skill if another initializer introduces one.
            if(shadow.OwnedSkills.Snapshot().Entries.Any(s=>s.Base>0&&s.Id!=0&&ShadowSkillBook.Metadata(s.Id).Class!=6))
            {throw new NotSupportedException("Original Shadow item spell needs its off-class intrinsic synergy adapter");}
            return graph;
        }
        throw new NotSupportedException("Original NPC equipment needs its own source graph");
    }
}
